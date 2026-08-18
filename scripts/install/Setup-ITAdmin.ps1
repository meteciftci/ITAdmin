#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs ITAdmin from a self-contained release package.

.DESCRIPTION
    This is the production first-install entrypoint shipped at the root of every
    ITAdmin-<version>-windows.zip package. It does not clone the source repository, does not require
    Git, and does not contact GitHub. The package already contains the exact prebuilt release tree
    produced from the annotated release tag by CI.

    The script verifies the release identity and the release-matched installer before executing it,
    provisions prerequisites, creates the IIS app-pool virtual account before any release ACL is
    applied, records the machine layout used by the LocalSystem services, writes a Host Agent
    configuration with in-app updates disabled by default, and then hands the local verified release
    to the canonical installer.

    Repository-backed in-app updates are an optional post-install capability. Enable them separately
    with Configure-ITAdminUpdates.ps1; first installation never depends on repository connectivity.
#>
[CmdletBinding()]
param(
    [string]$ReleaseDirectory = (Join-Path $PSScriptRoot "release"),

    [string]$DatabaseHost,
    [int]$DatabasePort = 5432,
    [string]$DatabaseName,
    [string]$DatabaseUser,
    [SecureString]$DatabasePassword,

    [string]$DirectoryName,
    [string]$DirectoryHost,
    [string]$DirectoryBaseDn,
    [string]$DirectoryUserSearchFilter = "(sAMAccountName={0})",
    [string]$DirectoryBindUser,
    [string]$DirectoryBindDomain,
    [SecureString]$DirectoryBindPassword,
    [string]$InitialAdministrator,

    [int]$HttpPort = 80,
    [string]$HttpHostHeader,

    [string]$SiteName = "ITAdmin",
    [string]$AppPoolName = "ITAdmin",
    [string]$ProgramFilesRoot = "$env:ProgramFiles\ITAdmin",
    [string]$ProgramDataRoot = "$env:ProgramData\ITAdmin",

    [switch]$WhatIfPreflightOnly,
    [switch]$PrerequisitesOnly,
    [switch]$AllowDowngrade,
    [switch]$Unattended
)

$ErrorActionPreference = "Stop"
$Script:StepNumber = 0
$Script:CallerParameters = @{}
foreach ($key in $PSBoundParameters.Keys) {
    $Script:CallerParameters[$key] = $PSBoundParameters[$key]
}

function Write-Step {
    param([string]$Message)
    $Script:StepNumber++
    Write-Host ""
    Write-Host ("[{0}] {1}" -f $Script:StepNumber, $Message) -ForegroundColor Cyan
}

function Write-Detail { param([string]$Message) Write-Host "    $Message" }
function Write-Ok { param([string]$Message) Write-Host "    OK  $Message" -ForegroundColor Green }

function Resolve-LocalRelease {
    param([Parameter(Mandatory = $true)][string]$Path)

    Write-Step "Verifying the local release package"

    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Container)) {
        throw "Release directory not found: $resolved"
    }

    $manifestPath = Join-Path $resolved "release.manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "This package is incomplete: release\release.manifest.json is missing."
    }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "The packaged release manifest is not valid JSON."
    }

    foreach ($required in @("source", "distribution", "components")) {
        if ($null -eq $manifest.PSObject.Properties[$required]) {
            throw "The packaged release manifest is missing '$required'."
        }
    }

    $version = "$($manifest.source.version)"
    $sourceCommit = "$($manifest.source.commit)"
    if ([string]::IsNullOrWhiteSpace($version) -or
        [string]::IsNullOrWhiteSpace($sourceCommit) -or
        $sourceCommit -notmatch '^[0-9a-fA-F]{40,64}$') {
        throw "The packaged release has an invalid source identity."
    }

    if ("$($manifest.distribution.version)" -ne $version -or
        "$($manifest.distribution.sourceCommit)" -ne $sourceCommit) {
        throw "The packaged distribution identity does not match its source release identity."
    }

    $componentProperty = $manifest.components.PSObject.Properties["deployment-tooling"]
    if ($null -eq $componentProperty -or "$($componentProperty.Value.kind)" -ne "DeploymentTooling") {
        throw "The packaged release does not declare release-matched deployment tooling."
    }

    $installer = Join-Path $resolved "deployment-tooling\Install-ITAdmin.ps1"
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
        throw "The packaged release is missing deployment-tooling\Install-ITAdmin.ps1."
    }

    $installerDigestProperty = $componentProperty.Value.integrity.files.PSObject.Properties["Install-ITAdmin.ps1"]
    if ($null -eq $installerDigestProperty) {
        throw "The release manifest does not declare the installer digest."
    }

    $expectedDigest = "$($installerDigestProperty.Value)".ToLowerInvariant()
    $actualDigest = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expectedDigest -ne $actualDigest) {
        throw "The release-matched installer failed SHA-256 verification and will not be executed."
    }

    Write-Detail "Version:       $version"
    Write-Detail "Source commit: $sourceCommit"
    Write-Ok "Package identity and release-matched installer verified"

    return [pscustomobject]@{
        Root = $resolved
        Version = $version
        SourceCommit = $sourceCommit
        Installer = $installer
    }
}

function Initialize-AppPoolIdentity {
    Write-Step "Preparing the IIS application identity"

    Import-Module WebAdministration -ErrorAction Stop
    $appPoolPath = "IIS:\AppPools\$AppPoolName"
    if (-not (Test-Path $appPoolPath)) {
        New-WebAppPool -Name $AppPoolName | Out-Null
        Write-Detail "Created app pool $AppPoolName."
    }

    $identity = "IIS AppPool\$AppPoolName"
    try {
        $account = New-Object System.Security.Principal.NTAccount($identity)
        $null = $account.Translate([System.Security.Principal.SecurityIdentifier])
    }
    catch [System.Security.Principal.IdentityNotMappedException] {
        throw "The IIS application pool '$AppPoolName' exists, but its virtual account '$identity' could not be resolved."
    }

    Write-Ok "Application pool virtual account is ready for release ACLs"
}

function Register-MachineLayout {
    Write-Step "Registering the machine installation layout"

    $registryPath = "HKLM:\SOFTWARE\ITAdmin"
    New-Item -Path $registryPath -Force | Out-Null
    New-ItemProperty -Path $registryPath -Name "ProgramDataRoot" -Value $ProgramDataRoot `
        -PropertyType String -Force | Out-Null

    $registered = (Get-ItemProperty -Path $registryPath -Name "ProgramDataRoot" -ErrorAction Stop).ProgramDataRoot
    if (-not [string]::Equals("$registered", $ProgramDataRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The machine ProgramDataRoot registration could not be verified."
    }

    Write-Ok "Host services will discover ProgramData at $ProgramDataRoot"
}

function Initialize-HostAgentSettings {
    Write-Step "Preparing Host Agent configuration"

    $configRoot = Join-Path $ProgramDataRoot "config"
    New-Item -ItemType Directory -Path $configRoot -Force | Out-Null
    $settingsPath = Join-Path $configRoot "hostagent.json"

    $repositoryUrl = "ssh://git@ssh.github.com:443/meteciftci/ITAdmin.git"
    $channel = 0
    $deployKeyDirectory = Join-Path $ProgramDataRoot "keys"
    $updatesEnabled = $false

    if (Test-Path -LiteralPath $settingsPath -PathType Leaf) {
        try {
            $existing = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace("$($existing.repositoryUrl)")) {
                $repositoryUrl = "$($existing.repositoryUrl)"
            }
            if ($null -ne $existing.PSObject.Properties["channel"]) {
                if ("$($existing.channel)" -eq "Preview" -or "$($existing.channel)" -eq "1") { $channel = 1 }
            }
            if (-not [string]::IsNullOrWhiteSpace("$($existing.deployKeyDirectory)")) {
                $deployKeyDirectory = "$($existing.deployKeyDirectory)"
            }
            if ($null -ne $existing.PSObject.Properties["updatesEnabled"]) {
                $updatesEnabled = [bool]$existing.updatesEnabled
            }
            Write-Detail "Preserved existing Host Agent update settings."
        }
        catch {
            Write-Detail "Existing Host Agent settings were unreadable and will be replaced with safe defaults."
        }
    }

    $settings = [ordered]@{
        schemaVersion = 1
        repositoryUrl = $repositoryUrl
        channel = $channel
        deployKeyDirectory = $deployKeyDirectory
        programFilesRoot = $ProgramFilesRoot
        programDataRoot = $ProgramDataRoot
        siteName = $SiteName
        appPoolName = $AppPoolName
        updatesEnabled = $updatesEnabled
    }

    $settings | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    Write-Ok $(if ($updatesEnabled) {
        "Host Agent settings preserved; repository-backed updates remain enabled"
    }
    else {
        "Host Agent configured; repository-backed updates are disabled by default"
    })
}

function Get-ForwardedInstallerParameters {
    $forward = @{}
    foreach ($key in $Script:CallerParameters.Keys) {
        if ($key -in @("ReleaseDirectory", "WhatIfPreflightOnly", "PrerequisitesOnly")) { continue }
        $forward[$key] = $Script:CallerParameters[$key]
    }
    return $forward
}

Write-Host ""
Write-Host "ITAdmin package setup" -ForegroundColor White
Write-Host "=====================" -ForegroundColor White

try {
    $release = Resolve-LocalRelease -Path $ReleaseDirectory
    $forward = Get-ForwardedInstallerParameters

    if ($WhatIfPreflightOnly.IsPresent) {
        Write-Step "Running non-mutating installation preflight"
        & $release.Installer -ReleaseDirectory $release.Root `
            -ExpectedVersion $release.Version -ExpectedSourceCommit $release.SourceCommit `
            -WhatIfPreflightOnly @forward
        exit $LASTEXITCODE
    }

    Write-Step "Confirming server prerequisites"
    & $release.Installer -PrerequisitesOnly -ProvisionPrerequisites `
        -ProgramFilesRoot $ProgramFilesRoot -ProgramDataRoot $ProgramDataRoot `
        -SiteName $SiteName -AppPoolName $AppPoolName `
        -Unattended:$Unattended.IsPresent
    if ($LASTEXITCODE -ne 0) {
        throw "Server prerequisite preparation did not complete successfully."
    }

    if ($PrerequisitesOnly.IsPresent) {
        Write-Ok "Prerequisites are ready. No application payload was staged or activated."
        exit 0
    }

    Initialize-AppPoolIdentity
    Register-MachineLayout
    Initialize-HostAgentSettings

    Write-Step "Installing the packaged release"
    & $release.Installer -ReleaseDirectory $release.Root `
        -ExpectedVersion $release.Version -ExpectedSourceCommit $release.SourceCommit @forward
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        try {
            Start-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
        }
        catch { }
    }

    exit $exitCode
}
catch {
    Write-Host ""
    Write-Host "    !!  $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "The packaged release was not activated by this setup run." -ForegroundColor Yellow
    exit 1
}
