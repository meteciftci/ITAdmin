#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [Parameter()]
    [string]$RuntimeRoot = "C:\ProgramData\ITAdmin",

    [Parameter()]
    [string]$AppPoolName = "ITAdmin",

    [Parameter()]
    [switch]$RestoreMachineEnvironment,

    [Parameter()]
    [switch]$RestartAppPool
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"


$Script:ITAdminBackupRedactedMarker = "[REDACTED]"

function Test-ITAdminAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-ITAdminRedactedSecretValue {
    param(
        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }

    return ($Value -eq $Script:ITAdminBackupRedactedMarker)
}

function Set-ITAdminMachineEnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    [Environment]::SetEnvironmentVariable($Name, $Value, "Machine")
}

function Restore-ITAdminDataProtectionKeys {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,

        [Parameter(Mandatory = $true)]
        [string]$TargetKeysPath
    )

    if (-not (Test-Path -LiteralPath $ArchivePath)) {
        Write-Warning "DataProtection key archive not found in backup: $ArchivePath"
        return
    }

    if (-not (Test-Path -LiteralPath $TargetKeysPath)) {
        New-Item -ItemType Directory -Path $TargetKeysPath -Force | Out-Null
    }

    $existingKeys = Get-ChildItem -LiteralPath $TargetKeysPath -File -ErrorAction SilentlyContinue
    if ($existingKeys.Count -gt 0) {
        Write-Warning "Existing DataProtection key files were found. They will be replaced by restored files."
        Remove-Item -LiteralPath (Join-Path $TargetKeysPath "*") -Force
    }

    Expand-Archive -Path $ArchivePath -DestinationPath $TargetKeysPath -Force
    Write-Host "Restored DataProtection keys to: $TargetKeysPath"
}

function Restore-ITAdminMachineEnvironment {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$MachineEnvironment
    )

    $restoredCount = 0
    $skippedCount = 0

    foreach ($entry in $MachineEnvironment.GetEnumerator()) {
        $name = [string]$entry.Key
        $value = [string]$entry.Value

        if (Test-ITAdminRedactedSecretValue -Value $value) {
            Write-Warning "Skipped redacted machine environment variable: $name"
            $skippedCount++
            continue
        }

        Set-ITAdminMachineEnvironmentVariable -Name $name -Value $value
        $restoredCount++
    }

    Write-Host "Restored machine environment variables: $restoredCount"
    if ($skippedCount -gt 0) {
        Write-Warning "Skipped $skippedCount redacted machine environment variables. Reconfigure secrets manually or rerun backup with -IncludeSecrets."
    }
}

if (-not (Test-ITAdminAdministrator)) {
    throw "This script must be run from an elevated PowerShell session."
}

if (-not (Test-Path -LiteralPath $BackupPath)) {
    throw "Backup file not found: $BackupPath"
}

$workingDirectory = Join-Path ([IO.Path]::GetTempPath()) ("itadmin-restore-{0}" -f ([Guid]::NewGuid().ToString("N")))
New-Item -ItemType Directory -Path $workingDirectory -Force | Out-Null

try {
    Expand-Archive -Path $BackupPath -DestinationPath $workingDirectory -Force

    $metadataPath = Join-Path $workingDirectory "runtime-config.json"
    if (-not (Test-Path -LiteralPath $metadataPath)) {
        throw "Backup metadata file not found: runtime-config.json"
    }

    $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json

    $keysPath = $RuntimeRoot
    if ($null -ne $metadata.dataProtectionKeysPath -and -not [string]::IsNullOrWhiteSpace([string]$metadata.dataProtectionKeysPath)) {
        $keysPath = [string]$metadata.dataProtectionKeysPath
    }
    else {
        $keysPath = Join-Path $RuntimeRoot "DataProtection-Keys"
    }

    $dataProtectionArchivePath = Join-Path $workingDirectory "data-protection-keys.zip"
    Restore-ITAdminDataProtectionKeys -ArchivePath $dataProtectionArchivePath -TargetKeysPath $keysPath

    if ($RestoreMachineEnvironment) {
        if ($null -eq $metadata.machineEnvironmentVariables) {
            Write-Warning "Backup does not contain machineEnvironmentVariables."
        }
        else {
            $machineEnvironment = @{}
            foreach ($property in $metadata.machineEnvironmentVariables.PSObject.Properties) {
                $machineEnvironment[$property.Name] = [string]$property.Value
            }

            Restore-ITAdminMachineEnvironment -MachineEnvironment $machineEnvironment
        }
    }
    else {
        Write-Host "Machine environment restore skipped. Use -RestoreMachineEnvironment to apply ITADMIN_* values from backup."
    }

    if ($RestartAppPool) {
        Import-Module WebAdministration -ErrorAction Stop
        if (Test-Path "IIS:\AppPools\$AppPoolName") {
            Restart-WebAppPool -Name $AppPoolName
            Write-Host "Restarted app pool: $AppPoolName"
        }
        else {
            Write-Warning "App pool not found for restart: $AppPoolName"
        }
    }

    Write-Host "Restore completed from: $BackupPath"
}
finally {
    if (Test-Path -LiteralPath $workingDirectory) {
        Remove-Item -LiteralPath $workingDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
