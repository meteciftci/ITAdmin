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
    [string]$BackupDirectory,

    [Parameter()]
    [switch]$IncludeSecrets
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptRoot "ITAdmin-Iis.Common.ps1")

$Script:ITAdminBackupRedactedMarker = "[REDACTED]"

function Test-ITAdminAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function ConvertTo-ITAdminBackupEnvironmentSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Snapshot,

        [Parameter(Mandatory = $true)]
        [bool]$IncludeSecrets
    )

    $output = @{}
    foreach ($entry in $Snapshot.GetEnumerator()) {
        if ($IncludeSecrets) {
            $output[$entry.Key] = [string]$entry.Value
            continue
        }

        $output[$entry.Key] = Format-ITAdminSecretValue -Name $entry.Key -Value $entry.Value
    }

    return $output
}

if (-not (Test-ITAdminAdministrator)) {
    throw "This script must be run from an elevated PowerShell session."
}

Import-Module WebAdministration -ErrorAction Stop

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    $BackupDirectory = Join-Path $RuntimeRoot "Backups"
}

if (-not (Test-Path -LiteralPath $BackupDirectory)) {
    New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$workingDirectory = Join-Path ([IO.Path]::GetTempPath()) ("itadmin-backup-{0}" -f ([Guid]::NewGuid().ToString("N")))
$archivePath = Join-Path $BackupDirectory ("itadmin-runtime-backup-{0}.zip" -f $timestamp)

New-Item -ItemType Directory -Path $workingDirectory -Force | Out-Null

try {
    $appPoolEnvironment = Get-ITAdminAppPoolEnvironmentSnapshot -PoolName $AppPoolName
    $machineEnvironment = Get-ITAdminMachineEnvironmentSnapshot

    $keysPathResult = Get-ITAdminEffectiveRuntimeVariable `
        -Name "ITADMIN_DataProtection__KeysPath" `
        -AppPoolEnvironment $appPoolEnvironment `
        -MachineEnvironment $machineEnvironment

    $keysPath = $keysPathResult.Value
    if ([string]::IsNullOrWhiteSpace($keysPath)) {
        $keysPath = Join-Path $RuntimeRoot "DataProtection-Keys"
    }

    $metadata = [ordered]@{
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        siteName = $SiteName
        appPoolName = $AppPoolName
        runtimeRoot = $RuntimeRoot
        dataProtectionKeysPath = $keysPath
        includeSecrets = [bool]$IncludeSecrets
        appPoolEnvironmentVariables = ConvertTo-ITAdminBackupEnvironmentSnapshot -Snapshot $appPoolEnvironment -IncludeSecrets ([bool]$IncludeSecrets)
        machineEnvironmentVariables = ConvertTo-ITAdminBackupEnvironmentSnapshot -Snapshot $machineEnvironment -IncludeSecrets ([bool]$IncludeSecrets)
    }

    $metadataPath = Join-Path $workingDirectory "runtime-config.json"
    $metadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $metadataPath -Encoding UTF8

    $dataProtectionArchivePath = Join-Path $workingDirectory "data-protection-keys.zip"
    if (Test-Path -LiteralPath $keysPath) {
        Compress-Archive -Path (Join-Path $keysPath "*") -DestinationPath $dataProtectionArchivePath -Force
    }
    else {
        Write-Warning "DataProtection keys path not found: $keysPath"
    }

    if ($PSCmdlet.ShouldProcess($archivePath, "Create runtime configuration backup")) {
        Compress-Archive -Path (Join-Path $workingDirectory "*") -DestinationPath $archivePath -Force
    }

    Write-Host "Backup created: $archivePath"
    Write-Host "IncludeSecrets: $($IncludeSecrets.IsPresent)"

    Write-ITAdminEnvironmentSnapshot -Title "Backed up app pool environment variables:" -Snapshot $appPoolEnvironment -MaskSecrets:(-not $IncludeSecrets)
    Write-Host ""
    Write-ITAdminEnvironmentSnapshot -Title "Backed up machine environment variables:" -Snapshot $machineEnvironment -MaskSecrets:(-not $IncludeSecrets)
}
finally {
    if (Test-Path -LiteralPath $workingDirectory) {
        Remove-Item -LiteralPath $workingDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
