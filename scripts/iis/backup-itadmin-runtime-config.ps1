#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter()]
    [string]$SiteName = "ITAdmin",

    [Parameter()]
    [string]$AppPoolName = "ITAdmin",

    [Parameter()]
    [string]$RuntimeRoot = "C:\ProgramData\ITAdmin",

    [Parameter()]
    [string]$OutputDirectory = "C:\ProgramData\ITAdmin\Backups",

    [Parameter()]
    [switch]$IncludeSecrets
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"


$Script:ITAdminBackupRedactedMarker = "[REDACTED]"
$Script:ITAdminSecretVariablePatterns = @(
    "ITADMIN_ConnectionStrings__DefaultConnection",
    "ITADMIN_Jwt__Key",
    "ITADMIN_Setup__SetupKeyHash"
)

function Test-ITAdminAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Format-ITAdminBackupSecretValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [bool]$IncludeSecrets
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    if (-not $IncludeSecrets) {
        foreach ($pattern in $Script:ITAdminSecretVariablePatterns) {
            if ($Name -eq $pattern) {
                return $Script:ITAdminBackupRedactedMarker
            }
        }

        if ($Name -match '(?i)(Password|Secret|Key|Hash|Token)') {
            return $Script:ITAdminBackupRedactedMarker
        }
    }

    return $Value
}

function Get-ITAdminMachineEnvironmentForBackup {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$IncludeSecrets
    )

    $prefix = "ITADMIN_"
    $values = @{}

    foreach ($entry in [Environment]::GetEnvironmentVariables("Machine").GetEnumerator()) {
        $name = [string]$entry.Key
        if (-not $name.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            continue
        }

        $values[$name] = Format-ITAdminBackupSecretValue `
            -Name $name `
            -Value ([string]$entry.Value) `
            -IncludeSecrets:$IncludeSecrets
    }

    return $values
}

function Get-ITAdminAppPoolEnvironmentForBackup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName
    )

    $values = @{}
    $filterPath = "system.applicationHost/applicationPools/add[@name='$PoolName']/environmentVariables/add"
    $items = Get-WebConfiguration -PSPath "MACHINE/WEBROOT/APPHOST" -Filter $filterPath -ErrorAction SilentlyContinue

    if ($null -eq $items) {
        return $values
    }

    foreach ($item in @($items)) {
        if ($null -ne $item.name) {
            $values[[string]$item.name] = [string]$item.value
        }
    }

    return $values
}

function Get-ITAdminSiteBindingsForBackup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Site
    )

    $bindings = @()
    $siteBindings = Get-WebBinding -Name $Site -ErrorAction SilentlyContinue
    if ($null -eq $siteBindings) {
        return $bindings
    }

    foreach ($binding in @($siteBindings)) {
        $bindings += [ordered]@{
            protocol = [string]$binding.protocol
            bindingInformation = [string]$binding.bindingInformation
        }
    }

    return $bindings
}

if (-not (Test-ITAdminAdministrator)) {
    throw "This script must be run from an elevated PowerShell session."
}

if ($IncludeSecrets) {
    Write-Warning "IncludeSecrets is enabled. The backup will contain live secrets. Store the backup in a secure location and restrict access."
}

Import-Module WebAdministration -ErrorAction Stop

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupFolderName = "itadmin-runtime-backup-$timestamp"
$backupWorkingDirectory = Join-Path $OutputDirectory $backupFolderName
$metadataPath = Join-Path $backupWorkingDirectory "runtime-config.json"
$dataProtectionKeysPath = Join-Path $RuntimeRoot "DataProtection-Keys"
$dataProtectionArchivePath = Join-Path $backupWorkingDirectory "data-protection-keys.zip"
$finalArchivePath = Join-Path $OutputDirectory "$backupFolderName.zip"

New-Item -ItemType Directory -Path $backupWorkingDirectory -Force | Out-Null

$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
$physicalPath = $null
$siteState = $null
if ($null -ne $site) {
    $physicalPath = [string]$site.PhysicalPath
    $siteState = [string]$site.State
}

$metadata = [ordered]@{
    version = 1
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    siteName = $SiteName
    appPoolName = $AppPoolName
    siteState = $siteState
    physicalPath = $physicalPath
    runtimeRoot = $RuntimeRoot
    hostName = $null
    bindings = Get-ITAdminSiteBindingsForBackup -Site $SiteName
    appPoolEnvironmentVariables = Get-ITAdminAppPoolEnvironmentForBackup -PoolName $AppPoolName
    machineEnvironmentVariables = Get-ITAdminMachineEnvironmentForBackup -IncludeSecrets:$IncludeSecrets
    dataProtectionKeysPath = $dataProtectionKeysPath
    includeSecrets = [bool]$IncludeSecrets
}

if ($metadata.bindings.Count -gt 0) {
    $firstBinding = $metadata.bindings[0].bindingInformation
    if ($firstBinding -match ':(?<host>[^:]+)$') {
        $metadata.hostName = $Matches["host"]
    }
}

$metadata | ConvertTo-Json -Depth 6 | Set-Content -Path $metadataPath -Encoding UTF8

if (Test-Path -LiteralPath $dataProtectionKeysPath) {
    $keyFiles = Get-ChildItem -LiteralPath $dataProtectionKeysPath -File -ErrorAction SilentlyContinue
    if ($keyFiles.Count -gt 0) {
        Compress-Archive -Path (Join-Path $dataProtectionKeysPath "*") -DestinationPath $dataProtectionArchivePath -Force
    }
    else {
        Write-Warning "DataProtection key path exists but contains no files: $dataProtectionKeysPath"
    }
}
else {
    Write-Warning "DataProtection key path not found: $dataProtectionKeysPath"
}

Compress-Archive -Path (Join-Path $backupWorkingDirectory "*") -DestinationPath $finalArchivePath -Force
Remove-Item -LiteralPath $backupWorkingDirectory -Recurse -Force

Write-Host "Backup created: $finalArchivePath"
Write-Host ("Secrets included: {0}" -f ([bool]$IncludeSecrets))
if (-not $IncludeSecrets) {
    Write-Host "Secret values were redacted in runtime-config.json."
}
