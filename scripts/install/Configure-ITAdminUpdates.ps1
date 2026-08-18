#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Enables or disables repository-backed in-app updates for an installed ITAdmin host.

.DESCRIPTION
    First installation is intentionally offline and does not require GitHub access. Run this script
    only when the server should be able to discover and apply future releases from the private
    repository.

    Enabling updates requires Git, OpenSSH, a read-only repository deploy key, and a host key that
    the operator has already verified. This script never runs ssh-keyscan and never invents host
    trust. It copies the supplied material into the machine-owned ITAdmin key store, verifies the
    repository using exactly that persisted identity, then enables updates in hostagent.json.

    Re-running this script preserves the installed Host Agent layout unless a corresponding value is
    explicitly supplied. Disabling updates changes only the update switch and restarts the service;
    it does not silently rewrite site names, custom roots, repository identity, or channel.
#>
[CmdletBinding()]
param(
    [string]$RepositoryUrl = "ssh://git@ssh.github.com:443/meteciftci/ITAdmin.git",
    [string]$DeployKeyPath,
    [string]$KnownHostsPath = (Join-Path $env:USERPROFILE ".ssh\known_hosts"),
    [ValidateSet("stable", "preview")]
    [string]$Channel = "stable",

    [string]$ProgramFilesRoot = "$env:ProgramFiles\ITAdmin",
    [string]$ProgramDataRoot = "$env:ProgramData\ITAdmin",
    [string]$SiteName = "ITAdmin",
    [string]$AppPoolName = "ITAdmin",

    [switch]$Disable
)

$ErrorActionPreference = "Stop"
$Script:CallerParameters = @{}
foreach ($key in $PSBoundParameters.Keys) {
    $Script:CallerParameters[$key] = $PSBoundParameters[$key]
}

function Write-Detail { param([string]$Message) Write-Host "    $Message" }
function Write-Ok { param([string]$Message) Write-Host "    OK  $Message" -ForegroundColor Green }

function Read-ExistingHostAgentSettings {
    $configRoot = Join-Path $ProgramDataRoot "config"
    $settingsPath = Join-Path $configRoot "hostagent.json"
    if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Existing Host Agent configuration is not valid JSON: $settingsPath"
    }
}

function Apply-ExistingHostAgentDefaults {
    param([psobject]$Existing)

    if ($null -eq $Existing) { return }

    if (-not $Script:CallerParameters.ContainsKey("RepositoryUrl") -and
        -not [string]::IsNullOrWhiteSpace("$($Existing.repositoryUrl)")) {
        $Script:RepositoryUrl = "$($Existing.repositoryUrl)"
    }
    if (-not $Script:CallerParameters.ContainsKey("Channel") -and
        ("$($Existing.channel)" -eq "Preview" -or "$($Existing.channel)" -eq "1")) {
        $Script:Channel = "preview"
    }
    if (-not $Script:CallerParameters.ContainsKey("ProgramFilesRoot") -and
        -not [string]::IsNullOrWhiteSpace("$($Existing.programFilesRoot)")) {
        $Script:ProgramFilesRoot = "$($Existing.programFilesRoot)"
    }
    if (-not $Script:CallerParameters.ContainsKey("ProgramDataRoot") -and
        -not [string]::IsNullOrWhiteSpace("$($Existing.programDataRoot)")) {
        $Script:ProgramDataRoot = "$($Existing.programDataRoot)"
    }
    if (-not $Script:CallerParameters.ContainsKey("SiteName") -and
        -not [string]::IsNullOrWhiteSpace("$($Existing.siteName)")) {
        $Script:SiteName = "$($Existing.siteName)"
    }
    if (-not $Script:CallerParameters.ContainsKey("AppPoolName") -and
        -not [string]::IsNullOrWhiteSpace("$($Existing.appPoolName)")) {
        $Script:AppPoolName = "$($Existing.appPoolName)"
    }
}

function Set-HostAgentSettings {
    param(
        [Parameter(Mandatory = $true)][bool]$Enabled,
        [Parameter(Mandatory = $true)][string]$KeyDirectory
    )

    $configRoot = Join-Path $ProgramDataRoot "config"
    New-Item -ItemType Directory -Path $configRoot -Force | Out-Null
    $settingsPath = Join-Path $configRoot "hostagent.json"

    $settings = [ordered]@{
        schemaVersion = 1
        repositoryUrl = $RepositoryUrl
        channel = $(if ($Channel -eq "preview") { 1 } else { 0 })
        deployKeyDirectory = $KeyDirectory
        programFilesRoot = $ProgramFilesRoot
        programDataRoot = $ProgramDataRoot
        siteName = $SiteName
        appPoolName = $AppPoolName
        updatesEnabled = $Enabled
    }

    $settings | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    Write-Ok $(if ($Enabled) { "Repository-backed updates enabled" } else { "Repository-backed updates disabled" })
}

function Restart-HostAgent {
    $service = Get-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        throw "ITAdminHostAgent is not installed. Install ITAdmin before configuring in-app updates."
    }

    Restart-Service -Name "ITAdminHostAgent" -Force -ErrorAction Stop
    $service = Get-Service -Name "ITAdminHostAgent"
    if ($service.Status -ne "Running") {
        throw "ITAdminHostAgent did not return to the Running state."
    }
    Write-Ok "ITAdmin Host Agent restarted"
}

function Invoke-IcaclsChecked {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & icacls @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "icacls failed with exit code $LASTEXITCODE while securing repository credentials."
    }
}

Write-Host ""
Write-Host "ITAdmin update configuration" -ForegroundColor White
Write-Host "============================"

$existingSettings = Read-ExistingHostAgentSettings
Apply-ExistingHostAgentDefaults -Existing $existingSettings

$keyDirectory = if ($null -ne $existingSettings -and
    -not [string]::IsNullOrWhiteSpace("$($existingSettings.deployKeyDirectory)")) {
    "$($existingSettings.deployKeyDirectory)"
}
else {
    Join-Path $ProgramDataRoot "keys"
}
$keyDirectory = [System.IO.Path]::GetFullPath($keyDirectory)

if ($Disable.IsPresent) {
    if ($null -eq $existingSettings) {
        throw "Host Agent configuration was not found. Install ITAdmin before disabling in-app updates."
    }
    Set-HostAgentSettings -Enabled $false -KeyDirectory $keyDirectory
    Restart-HostAgent
    exit 0
}

$git = Get-Command git -ErrorAction SilentlyContinue
if ($null -eq $git) {
    throw "Git for Windows is required only for repository-backed in-app updates. Install Git, then re-run this script."
}
$ssh = Get-Command ssh -ErrorAction SilentlyContinue
if ($null -eq $ssh) {
    throw "OpenSSH is required for repository-backed in-app updates."
}
$sshKeygen = Get-Command ssh-keygen -ErrorAction SilentlyContinue
if ($null -eq $sshKeygen) {
    throw "ssh-keygen is required to read the operator-verified known_hosts file."
}

if ([string]::IsNullOrWhiteSpace($DeployKeyPath)) {
    $candidates = @(
        (Join-Path $keyDirectory "deploy_key"),
        (Join-Path $env:USERPROFILE ".ssh\itadmin_deploy"),
        (Join-Path $env:USERPROFILE ".ssh\id_ed25519")
    )
    $DeployKeyPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($DeployKeyPath) -or -not (Test-Path -LiteralPath $DeployKeyPath -PathType Leaf)) {
    throw "No deploy key was found. Pass -DeployKeyPath with a read-only Deploy Key registered for the ITAdmin repository."
}
if (-not (Test-Path -LiteralPath $KnownHostsPath -PathType Leaf)) {
    throw "Verified known_hosts file not found: $KnownHostsPath"
}

try {
    $uri = [Uri]$RepositoryUrl
}
catch {
    throw "RepositoryUrl must be an absolute ssh:// URL."
}
if ($uri.Scheme -ne "ssh" -or [string]::IsNullOrWhiteSpace($uri.Host)) {
    throw "RepositoryUrl must use ssh:// so the machine deploy key is the only repository credential."
}

$hostLookup = if ($uri.Port -gt 0 -and $uri.Port -ne 22) { "[$($uri.Host)]:$($uri.Port)" } else { $uri.Host }
$entries = & ssh-keygen -F $hostLookup -f $KnownHostsPath 2>$null
$entryLines = @($entries | Where-Object { "$_" -notmatch '^\s*#' -and -not [string]::IsNullOrWhiteSpace("$_") })
if ($entryLines.Count -eq 0) {
    throw "No operator-verified host key for '$hostLookup' exists in $KnownHostsPath. Verify the host fingerprint first; this script will not trust an unverified host."
}

New-Item -ItemType Directory -Path $keyDirectory -Force | Out-Null
Invoke-IcaclsChecked -Arguments @($keyDirectory, "/inheritance:r")
Invoke-IcaclsChecked -Arguments @($keyDirectory, "/grant:r", "SYSTEM:(OI)(CI)F", "Administrators:(OI)(CI)F")

$machineKey = [System.IO.Path]::GetFullPath((Join-Path $keyDirectory "deploy_key"))
$sourceKey = (Resolve-Path -LiteralPath $DeployKeyPath).Path
if (-not [string]::Equals($sourceKey, $machineKey, [StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $sourceKey -Destination $machineKey -Force
}
$machineKnownHosts = [System.IO.Path]::GetFullPath((Join-Path $keyDirectory "known_hosts"))
Set-Content -LiteralPath $machineKnownHosts -Value $entryLines -Encoding ASCII

Invoke-IcaclsChecked -Arguments @($machineKey, "/inheritance:r")
Invoke-IcaclsChecked -Arguments @($machineKey, "/grant:r", "SYSTEM:F", "Administrators:F")
Invoke-IcaclsChecked -Arguments @($machineKnownHosts, "/inheritance:r")
Invoke-IcaclsChecked -Arguments @($machineKnownHosts, "/grant:r", "SYSTEM:F", "Administrators:F")
Write-Ok "Deploy key and verified host trust persisted for the machine"

$previousSshCommand = $env:GIT_SSH_COMMAND
$previousSshVariant = $env:GIT_SSH_VARIANT
$previousTerminalPrompt = $env:GIT_TERMINAL_PROMPT
$previousErrorActionPreference = $ErrorActionPreference
try {
    $env:GIT_SSH_COMMAND = "ssh -i `"$machineKey`" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=`"$machineKnownHosts`" -o GlobalKnownHostsFile=/dev/null"
    $env:GIT_SSH_VARIANT = "ssh"
    $env:GIT_TERMINAL_PROMPT = "0"

    # Windows PowerShell 5.1 converts redirected native stderr into error records. Git's exit code,
    # not progress text on stderr, is the authority for native command success.
    $ErrorActionPreference = "Continue"
    $output = & git ls-remote --tags --quiet $RepositoryUrl 2>&1
    $exitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
    if ($null -ne $previousSshCommand) { $env:GIT_SSH_COMMAND = $previousSshCommand } else { Remove-Item Env:\GIT_SSH_COMMAND -ErrorAction SilentlyContinue }
    if ($null -ne $previousSshVariant) { $env:GIT_SSH_VARIANT = $previousSshVariant } else { Remove-Item Env:\GIT_SSH_VARIANT -ErrorAction SilentlyContinue }
    if ($null -ne $previousTerminalPrompt) { $env:GIT_TERMINAL_PROMPT = $previousTerminalPrompt } else { Remove-Item Env:\GIT_TERMINAL_PROMPT -ErrorAction SilentlyContinue }
}

if ($exitCode -ne 0) {
    $text = ($output | ForEach-Object { "$_" }) -join "`n"
    throw "Repository access verification failed (git exit $exitCode). Git reported:`n$text"
}
Write-Ok "Repository access verified with the machine-owned read-only identity"

Set-HostAgentSettings -Enabled $true -KeyDirectory $keyDirectory
Restart-HostAgent
Write-Detail "Channel: $Channel"
Write-Detail "Repository: $RepositoryUrl"
