#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Canonical ITAdmin installer for Windows Server + IIS.

.DESCRIPTION
    The single deployment authority for ITAdmin. Fresh install, repair, and (in a later round)
    update all run through this script, so there is never more than one way a machine can be
    brought to a serving state.

    The script is environment-neutral: it contains no hostnames, domains, addresses, or
    credentials. Everything site-specific is supplied as a parameter or prompted for, and is
    stored in machine state under ProgramData - never in the release artifact.

    Sequence:
      preflight -> collect environment -> validate artifact -> stage -> configure machine
                -> configure database -> migrate -> configure IIS/HTTPS -> activate
                -> health check -> persist state

    Each step records its phase in the installation state file first, so an interrupted run is
    recognisable on the next invocation instead of leaving a machine that merely looks installed.

    Initial hosting is HTTP only. HTTPS, the public host name, certificate selection, and the
    HTTP-to-HTTPS redirect are deliberately NOT install-time gates - they are configured later from
    ITAdmin Settings and applied by the ITAdmin Host Agent. Requiring them here previously blocked
    otherwise-complete installations over a certificate that a different team had not issued yet.

    ITAdmin authenticates through LDAP, so the directory is not optional: a working bind and a
    directory-backed initial administrator are established before this script reports success.

.PARAMETER ReleaseDirectory
    The canonical input: a release tree (release.manifest.json + app\) already fetched from the
    repository's distribution ref by Bootstrap-ITAdmin.ps1, or staged by the Host Agent for an
    update. Verified against -ExpectedVersion and -ExpectedSourceCommit before anything is staged.

.PARAMETER ExpectedVersion
    Version of the annotated release tag this run is installing. The payload must agree.

.PARAMETER ExpectedSourceCommit
    Commit the release tag peels to. The payload must record having been built from it.

.PARAMETER ArtifactPath
    Retained offline/developer mode: path to itadmin-<version>.zip from
    scripts/release/build-release.zsh. Not the normal product installation path; the normal path is
    repository-driven via Bootstrap-ITAdmin.ps1.

.PARAMETER DatabaseHost
    PostgreSQL server host name or address. ITAdmin does not install PostgreSQL.

.PARAMETER DirectoryHost
    Directory host to bind to. Defaults to the AD domain this computer is joined to, which lets DC
    locator pick a controller instead of pinning one.

.PARAMETER DirectoryBaseDn
    Directory Base DN. Defaults to the naming context derived from the joined domain.

.PARAMETER InitialAdministrator
    UPN, sAMAccountName, or mail of the directory user who becomes the first ITAdmin administrator.
    Their password is never requested: only the bind account credential is used, for lookup.

.PARAMETER HttpPort
    Port for the initial HTTP binding. Defaults to 80.

.PARAMETER HttpHostHeader
    Optional host header for the HTTP binding. Omitted by default so the site answers on every name
    the machine already has.

.PARAMETER WhatIfPreflightOnly
    Run preflight and environment validation, then stop without changing the machine.

.PARAMETER ProvisionPrerequisites
    Explicitly install missing IIS role services and (when a verified Hosting Bundle installer is
    available) the ASP.NET Core Hosting Bundle. Default behaviour only reports gaps.

.PARAMETER PrerequisitesOnly
    Stop after prerequisite provisioning and a confirming re-preflight. Requires no release input.
    Implies -ProvisionPrerequisites.

.PARAMETER HostingBundlePath
    Path to an ASP.NET Core Hosting Bundle installer (dotnet-hosting-10.*.exe). Normally supplied by
    the bootstrap from the repository's prerequisite distribution ref; may be given explicitly for
    a fully offline install.

.PARAMETER HostingBundleSha256
    Expected SHA-256 of the Hosting Bundle installer. When omitted, a sidecar file
    "<installer>.sha256" next to HostingBundlePath is required.

.PARAMETER Unattended
    Never prompt. Every required value must be supplied as a parameter. Used by the Host Agent when
    it applies an update, where there is no console to answer a question.

.EXAMPLE
    # Normal product installation is repository-driven; this script is invoked by the bootstrap.
    .\Bootstrap-ITAdmin.ps1

.EXAMPLE
    # Offline/developer mode against a prebuilt artifact.
    .\Install-ITAdmin.ps1 -ArtifactPath .\itadmin-2.0.0.zip `
        -DatabaseHost db.contoso.com -DatabaseName itadmin -DatabaseUser itadmin_app
#>
[CmdletBinding()]
param(
    [string]$ReleaseDirectory,
    [string]$ExpectedVersion,
    [string]$ExpectedSourceCommit,

    [string]$ArtifactPath,

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
    [switch]$ProvisionPrerequisites,
    [switch]$PrerequisitesOnly,
    [string]$HostingBundlePath,
    [string]$HostingBundleSha256,
    [switch]$AllowDowngrade,
    [switch]$Unattended
)

if ($PrerequisitesOnly.IsPresent) {
    $ProvisionPrerequisites = $true
}

# Relative paths must be resolved against the caller's working directory BEFORE anything in this
# script changes location or hands a path to a child process. Resolving them later silently
# reinterprets ".\itadmin-2.0.0.zip" against whatever directory happened to be current, which
# produced a "file not found" for a file the operator was standing in.
function Resolve-CallerPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $Path
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).ProviderPath $Path))
}

$ArtifactPath = Resolve-CallerPath -Path $ArtifactPath
$ReleaseDirectory = Resolve-CallerPath -Path $ReleaseDirectory
$HostingBundlePath = Resolve-CallerPath -Path $HostingBundlePath

if (-not $PrerequisitesOnly.IsPresent -and
    [string]::IsNullOrWhiteSpace($ArtifactPath) -and
    [string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    throw "Supply -ReleaseDirectory (repository-driven, normal) or -ArtifactPath (offline mode), " +
          "unless -PrerequisitesOnly is specified."
}

$ErrorActionPreference = "Stop"

# --------------------------------------------------------------------------------------------
# Output
# --------------------------------------------------------------------------------------------

$Script:StepNumber = 0

function Write-Step {
    param([string]$Message)
    $Script:StepNumber++
    Write-Host ""
    Write-Host ("[{0}] {1}" -f $Script:StepNumber, $Message) -ForegroundColor Cyan
}

function Write-Detail {
    param([string]$Message)
    Write-Host "    $Message"
}

function Write-Ok {
    param([string]$Message)
    Write-Host "    OK  $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "    !!  $Message" -ForegroundColor Red
}

# --------------------------------------------------------------------------------------------
# Layout - mirrors ITAdmin.Deployment.DeploymentLayout
# --------------------------------------------------------------------------------------------

$Script:Layout = [pscustomobject]@{
    ProgramFilesRoot     = $ProgramFilesRoot
    ProgramDataRoot      = $ProgramDataRoot
    ReleasesRoot         = Join-Path $ProgramFilesRoot "releases"
    ConfigRoot           = Join-Path $ProgramDataRoot "config"
    SecretsRoot          = Join-Path $ProgramDataRoot "secrets"
    StateRoot            = Join-Path $ProgramDataRoot "state"
    DataProtectionRoot   = Join-Path $ProgramDataRoot "DataProtection-Keys"
    LogsRoot             = Join-Path $ProgramDataRoot "logs"
    BackupsRoot          = Join-Path $ProgramDataRoot "backups"
}

$Script:StatePath = Join-Path $Script:Layout.StateRoot "installation.json"
$Script:EnvironmentConfigPath = Join-Path $Script:Layout.ConfigRoot "environment.json"

# --------------------------------------------------------------------------------------------
# Installation state
# --------------------------------------------------------------------------------------------

function Get-InstallationState {
    if (-not (Test-Path -LiteralPath $Script:StatePath)) {
        return [pscustomobject]@{
            schemaVersion        = 1
            product              = "ITAdmin"
            phase                = "NotInstalled"
            activeVersion        = $null
            stagedVersion        = $null
            previousVersion      = $null
            lastMigrationApplied = $null
            migrationInFlight    = $false
            updatedAtUtc         = (Get-Date).ToUniversalTime().ToString("o")
            lastError            = $null
            currentOperation     = $null
            iisProvisionedByInstaller = $false
            readiness            = [pscustomobject]@{
                processHealthy            = $false
                setupCompleted            = $false
                directoryUsable           = $false
                administratorBootstrapped = $false
            }
        }
    }

    try {
        return Get-Content -LiteralPath $Script:StatePath -Raw | ConvertFrom-Json
    }
    catch {
        # Unreadable state is "unknown", never "fine". Refuse rather than act on a guess.
        throw "Installation state at $Script:StatePath is unreadable: $($_.Exception.Message). " +
              "Inspect or remove it before re-running."
    }
}

function Save-InstallationState {
    param([Parameter(Mandatory = $true)][psobject]$State)

    $State.updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    if (-not (Test-Path -LiteralPath $Script:Layout.StateRoot)) {
        New-Item -ItemType Directory -Path $Script:Layout.StateRoot -Force | Out-Null
    }

    $temporaryPath = "$Script:StatePath.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        $State | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
        Move-Item -LiteralPath $temporaryPath -Destination $Script:StatePath -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Set-CurrentUpdateOperationStage {
    param(
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][string]$Stage,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($null -eq $State.currentOperation -or "$($State.currentOperation.kind)" -ne "Update") {
        return
    }

    $State.currentOperation.stage = $Stage
    $State.currentOperation.message = $Message
    Save-InstallationState -State $State
}

function Set-Phase {
    param(
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][string]$Phase
    )

    # Phase is recorded BEFORE the work, so a crash mid-step is visible on the next run.
    $State.phase = $Phase
    Save-InstallationState -State $State
}

function Set-FailedPhase {
    param(
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][string]$Step,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $State.phase = "Failed"
    # Message only - this file is deliberately non-secret-bearing.
    $State.lastError = [pscustomobject]@{
        step         = $Step
        message      = $Message
        occurredAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }
    Save-InstallationState -State $State
}

function Get-InstallationIntent {
    param(
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][version]$Candidate
    )

    if ($State.migrationInFlight) { return "RecoverInterruptedMigration" }
    if ($State.phase -eq "AwaitingReboot") { return "ResumeAfterReboot" }
    if ($State.phase -in @("Failed", "Staging", "Configuring", "Activating", "ProvisioningPrerequisites")) {
        return "ResumeFailedInstall"
    }
    if ($State.phase -eq "NotInstalled" -or [string]::IsNullOrWhiteSpace($State.activeVersion)) { return "FreshInstall" }

    $active = $null
    if (-not [version]::TryParse(($State.activeVersion -split '-')[0], [ref]$active)) {
        return "ResumeFailedInstall"
    }

    if ($Candidate -eq $active) { return "SameVersionRepair" }
    if ($Candidate -gt $active) { return "Upgrade" }
    return "Downgrade"
}

# --------------------------------------------------------------------------------------------
# Prerequisites - IIS features + ASP.NET Core Hosting Bundle
# Authoritative feature names must stay aligned with ITAdmin.Deployment.IisPrerequisiteFeatures.
# Hosting Bundle shared-framework major must stay aligned with AspNetCoreHostingBundleRequirement / TFM.
# ANCM DLL file version is independent diagnostic metadata and is not compared to that major.
# --------------------------------------------------------------------------------------------

$Script:RequiredIisFeatures = @(
    [pscustomobject]@{ Name = "Web-Server";           Reason = "IIS Web Server role - ASP.NET Core is hosted out-of-process behind ANCM." },
    [pscustomobject]@{ Name = "Web-Default-Doc";      Reason = "Default document support for the SPA entry point under wwwroot." },
    [pscustomobject]@{ Name = "Web-Http-Errors";      Reason = "HTTP error responses for requests that never reach the ASP.NET Core process." },
    [pscustomobject]@{ Name = "Web-Static-Content";   Reason = "Static file serving for the published frontend assets under wwwroot." },
    [pscustomobject]@{ Name = "Web-Http-Logging";     Reason = "IIS request logging for operational diagnosis alongside application Serilog logs." },
    [pscustomobject]@{ Name = "Web-Stat-Compression"; Reason = "Static compression for frontend assets." },
    [pscustomobject]@{ Name = "Web-Filtering";        Reason = "Request filtering - baseline IIS request hardening." },
    [pscustomobject]@{ Name = "Web-Mgmt-Console";     Reason = "IIS Manager console for operator visibility of sites, bindings, and app pools." },
    [pscustomobject]@{ Name = "Web-Scripting-Tools";  Reason = "IIS management scripts - provides the WebAdministration PowerShell module the installer uses." }
)

$Script:HostingBundleRequirement = [pscustomobject]@{
    MajorVersion            = 10
    MinimumVersion          = [version]"10.0.0"
    TargetFramework         = "net10.0"
    DisplayName             = "Microsoft ASP.NET Core 10.0 Hosting Bundle"
    AncmRelativePath        = "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    AncmModuleName          = "AspNetCoreModuleV2"
    InstallerFileNamePattern = "dotnet-hosting-10.*.exe"
    SuccessRebootExitCode   = 3010
}

function Get-RequiredIisFeatureNames {
    return @($Script:RequiredIisFeatures | ForEach-Object { $_.Name })
}

function Test-WindowsRestartPending {
    $paths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"
    )
    foreach ($path in $paths) {
        if (Test-Path -LiteralPath $path) { return $true }
    }
    return $false
}

function Get-PrerequisiteDetection {
    $missingFeatures = New-Object System.Collections.Generic.List[string]
    $featureStates = @{}

    foreach ($feature in $Script:RequiredIisFeatures) {
        $state = Get-WindowsFeature -Name $feature.Name -ErrorAction SilentlyContinue
        $installed = ($null -ne $state -and $state.Installed)
        $featureStates[$feature.Name] = $installed
        if (-not $installed) {
            $missingFeatures.Add($feature.Name)
        }
    }

    $webAdmin = $null -ne (Get-Module -ListAvailable -Name WebAdministration)
    $iisInstalled = [bool]$featureStates["Web-Server"]

    $ancmPath = Join-Path $env:ProgramFiles $Script:HostingBundleRequirement.AncmRelativePath
    $ancmPresent = Test-Path -LiteralPath $ancmPath
    $ancmVersionText = $null
    if ($ancmPresent) {
        # Diagnostic only. ANCM file/product version (e.g. 20.x) is NOT the AspNetCore.App TFM major.
        $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ancmPath)
        $ancmVersionText = $versionInfo.FileVersion
    }

    $sharedFxRoot = Join-Path $env:ProgramFiles "dotnet\shared\Microsoft.AspNetCore.App"
    $sharedFxOk = $false
    $sharedFxVersionText = $null
    if (Test-Path -LiteralPath $sharedFxRoot) {
        foreach ($dir in (Get-ChildItem -LiteralPath $sharedFxRoot -Directory -ErrorAction SilentlyContinue)) {
            $fxVersion = $null
            if ([version]::TryParse($dir.Name, [ref]$fxVersion) -and
                $fxVersion.Major -eq $Script:HostingBundleRequirement.MajorVersion -and
                $fxVersion -ge $Script:HostingBundleRequirement.MinimumVersion) {
                $sharedFxOk = $true
                $sharedFxVersionText = $dir.Name
                break
            }
        }
    }

    $moduleRegistered = $false
    if ($webAdmin -and $iisInstalled) {
        try {
            Import-Module WebAdministration -ErrorAction Stop
            $module = Get-WebGlobalModule -Name $Script:HostingBundleRequirement.AncmModuleName -ErrorAction SilentlyContinue
            $moduleRegistered = ($null -ne $module)
        }
        catch {
            $moduleRegistered = $false
        }
    }

    # Shared-framework major tracks net10.0. ANCM file version is independent metadata.
    $hostingUsable = $ancmPresent -and $sharedFxOk
    if ($hostingUsable -and $webAdmin -and $iisInstalled -and -not $moduleRegistered) {
        $hostingUsable = $false
    }

    $restartPending = Test-WindowsRestartPending

    return [pscustomobject]@{
        MissingIisFeatures          = @($missingFeatures)
        FeatureStates               = $featureStates
        WebAdministrationAvailable  = $webAdmin
        IisInstalled                = $iisInstalled
        AncmPresent                 = $ancmPresent
        SharedFrameworkOk           = $sharedFxOk
        SharedFrameworkVersionText  = $sharedFxVersionText
        HostingBundlePresent        = ($ancmPresent -and $sharedFxOk)
        HostingBundleVersionOk      = $sharedFxOk
        HostingBundleVersionText    = $ancmVersionText
        AncmPath                    = $ancmPath
        AncmModuleRegistered        = $moduleRegistered
        HostingBundleUsable         = $hostingUsable
        RestartPending              = $restartPending
    }
}

function Write-PrerequisiteDetection {
    param([Parameter(Mandatory = $true)][psobject]$Detection)

    Write-Detail ("IIS features missing: {0}" -f $(if ($Detection.MissingIisFeatures.Count -eq 0) { "(none)" } else { $Detection.MissingIisFeatures -join ", " }))
    if ($Detection.WebAdministrationAvailable) {
        Write-Ok "WebAdministration module available"
    }
    else {
        Write-Detail "WebAdministration module unavailable"
    }

    # Build with concatenation. Do not use multi-arg -f inside .Add() elsewhere either:
    # inside method parentheses, commas bind as argument separators and truncate -f's arg list.
    $ancmLabel = if ([string]::IsNullOrWhiteSpace($Detection.HostingBundleVersionText)) {
        "ANCM present=$($Detection.AncmPresent)"
    }
    else {
        "ANCM $($Detection.HostingBundleVersionText)"
    }

    if ($Detection.HostingBundleUsable) {
        Write-Ok ("ASP.NET Core Hosting Bundle usable (" + $ancmLabel +
            ", sharedFramework=" + $(if ($Detection.SharedFrameworkVersionText) { $Detection.SharedFrameworkVersionText } else { $Detection.SharedFrameworkOk }) +
            ", moduleRegistered=" + $Detection.AncmModuleRegistered + ")")
    }
    elseif ($Detection.AncmPresent -or $Detection.SharedFrameworkOk) {
        Write-Detail ("Hosting Bundle present but not usable (" + $ancmLabel +
            ", sharedFrameworkOk=" + $Detection.SharedFrameworkOk +
            ", moduleRegistered=" + $Detection.AncmModuleRegistered + ")")
    }
    else {
        Write-Detail "ASP.NET Core Hosting Bundle missing or incomplete"
    }

    if ($Detection.RestartPending) {
        Write-Host "    WARN A Windows restart is pending." -ForegroundColor Yellow
    }
}

function Get-PrerequisiteBlockingProblems {
    param([Parameter(Mandatory = $true)][psobject]$Detection)

    $problems = New-Object System.Collections.Generic.List[string]

    if ($Detection.RestartPending) {
        $problems.Add("A Windows restart is pending from a previous change. Reboot, then re-run the installer.")
    }

    foreach ($name in $Detection.MissingIisFeatures) {
        $reason = ($Script:RequiredIisFeatures | Where-Object { $_.Name -eq $name } | Select-Object -First 1).Reason
        $problems.Add("IIS feature '$name' is not installed. $reason")
    }

    if (-not $Detection.WebAdministrationAvailable) {
        $problems.Add("The WebAdministration PowerShell module is unavailable; install Web-Scripting-Tools (IIS management scripts).")
    }

    if (-not $Detection.HostingBundleUsable) {
        # Concatenation only: never "$problems.Add((fmt) -f $a, $b)" - commas inside Add() are
        # method arguments in Windows PowerShell 5.1, which truncates the -f argument list and
        # throws FormatException ("Index ... argument list"), hiding the real diagnosis.
        if (-not $Detection.AncmPresent) {
            $problems.Add(
                $Script:HostingBundleRequirement.DisplayName +
                " is not installed (ASP.NET Core Module V2 was not found). " +
                "Provide an offline installer via -HostingBundlePath (or prerequisites\" +
                $Script:HostingBundleRequirement.InstallerFileNamePattern +
                ") and re-run with -ProvisionPrerequisites.")
        }
        elseif (-not $Detection.SharedFrameworkOk) {
            $problems.Add(
                $Script:HostingBundleRequirement.DisplayName +
                " is incomplete: Microsoft.AspNetCore.App " +
                $Script:HostingBundleRequirement.MajorVersion +
                ".x was not found under Program Files\dotnet\shared. " +
                "Install or repair the Hosting Bundle, then re-detect.")
        }
        elseif ($Detection.IisInstalled -and $Detection.WebAdministrationAvailable -and -not $Detection.AncmModuleRegistered) {
            $problems.Add(
                "ASP.NET Core Module '" + $Script:HostingBundleRequirement.AncmModuleName +
                "' is not registered with IIS. The Hosting Bundle must be installed or repaired after IIS.")
        }
        else {
            $ancmLabel = if ($Detection.HostingBundleVersionText) { $Detection.HostingBundleVersionText } else { "unknown" }
            $problems.Add(
                $Script:HostingBundleRequirement.DisplayName +
                " is present (ANCM " + $ancmLabel +
                ") but was not classified as usable. Re-run detection after verifying the shared framework and IIS module registration.")
        }
    }

    return $problems
}


function Restore-DistributionPrerequisite {
    <#
        Reassembles a runtime prerequisite that travelled INSIDE the distribution, and proves it is
        the file the release pinned before anything executes it.

        The ASP.NET Core Hosting Bundle is well over a Git host's per-object limit as a single file,
        so the publisher stores it as ordered chunks. Each chunk carries its own SHA-256 - useful for
        localising a fault - but the digest that authorises execution is the one taken over the
        REASSEMBLED file: individually valid chunks in the wrong order, or with one missing, would
        otherwise reconstruct into something nobody released.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Prerequisite,
        [Parameter(Mandatory = $true)][string]$DistributionRoot
    )

    $chunkRoot = Join-Path $DistributionRoot ($Prerequisite.componentPath -replace '/', '\')
    if (-not (Test-Path -LiteralPath $chunkRoot -PathType Container)) {
        throw "Prerequisite '$($Prerequisite.name)' declares component path '$($Prerequisite.componentPath)' " +
              "but that directory is missing from the distribution."
    }

    $staging = Join-Path $Script:Layout.ProgramDataRoot "prerequisites"
    if (-not (Test-Path -LiteralPath $staging)) {
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
    }

    $destination = Join-Path $staging $Prerequisite.fileName

    # A previously reassembled file is reused only when it still hashes correctly; a partial or
    # stale copy is replaced rather than trusted.
    if (Test-Path -LiteralPath $destination) {
        $existingHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($existingHash -eq $Prerequisite.sha256.ToLowerInvariant()) {
            Write-Detail "Prerequisite '$($Prerequisite.name)' already reassembled and verified."
            return $destination
        }
        Remove-Item -LiteralPath $destination -Force
    }

    $temporary = "$destination.reassembling"
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }

    Write-Detail "Reassembling $($Prerequisite.name) from $($Prerequisite.chunkDigests.Count) chunk(s)"

    $stream = [System.IO.File]::Create($temporary)
    try {
        for ($index = 0; $index -lt $Prerequisite.chunkDigests.Count; $index++) {
            $chunkName = "{0}.part{1:D4}" -f $Prerequisite.fileName, $index
            $chunkPath = Join-Path $chunkRoot $chunkName

            if (-not (Test-Path -LiteralPath $chunkPath)) {
                throw "Prerequisite '$($Prerequisite.name)': chunk $index ($chunkName) is missing."
            }

            $expected = ([string]$Prerequisite.chunkDigests[$index]).ToLowerInvariant()
            $actual = (Get-FileHash -LiteralPath $chunkPath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actual -ne $expected) {
                throw "Prerequisite '$($Prerequisite.name)': chunk $index digest does not match the manifest."
            }

            $bytes = [System.IO.File]::ReadAllBytes($chunkPath)
            $stream.Write($bytes, 0, $bytes.Length)
        }
    }
    finally {
        $stream.Dispose()
    }

    $reassembledSize = (Get-Item -LiteralPath $temporary).Length
    if ($reassembledSize -ne [int64]$Prerequisite.sizeBytes) {
        Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        throw "Prerequisite '$($Prerequisite.name)': reassembled size $reassembledSize does not match " +
              "the manifest size $($Prerequisite.sizeBytes)."
    }

    # The gate that authorises execution.
    $finalHash = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($finalHash -ne $Prerequisite.sha256.ToLowerInvariant()) {
        Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        throw "Prerequisite '$($Prerequisite.name)': the reassembled file does not match the digest the " +
              "release pinned. It will not be executed."
    }

    Move-Item -LiteralPath $temporary -Destination $destination
    Write-Ok "$($Prerequisite.name) $($Prerequisite.version) reassembled and verified (SHA-256)"

    return $destination
}

function Resolve-HostingBundleInstaller {
    <#
        Normal path: the Hosting Bundle came inside the distribution and is reassembled from it.
        -HostingBundlePath and a pre-staged copy remain as offline/enterprise recovery options, not
        as the product lifecycle.
    #>
    if (-not [string]::IsNullOrWhiteSpace($HostingBundlePath)) {
        if (-not (Test-Path -LiteralPath $HostingBundlePath)) {
            throw "Hosting Bundle installer not found: $HostingBundlePath"
        }
        Write-Detail "Using the explicitly supplied Hosting Bundle (offline mode)."
        return (Resolve-Path -LiteralPath $HostingBundlePath).Path
    }

    if ($null -ne $Script:DistributionPrerequisites) {
        foreach ($prerequisite in $Script:DistributionPrerequisites) {
            if ("$($prerequisite.fileName)" -like "dotnet-hosting-*.exe") {
                return Restore-DistributionPrerequisite -Prerequisite $prerequisite `
                    -DistributionRoot $Script:DistributionRoot
            }
        }
    }

    $searchRoots = New-Object System.Collections.Generic.List[string]
    $searchRoots.Add((Join-Path $PSScriptRoot "prerequisites"))
    # A prerequisite reassembled by an earlier run of this installer.
    $searchRoots.Add((Join-Path $Script:Layout.ProgramDataRoot "prerequisites"))

    foreach ($candidate in @($ArtifactPath, $ReleaseDirectory)) {
        if ([string]::IsNullOrWhiteSpace($candidate) -or -not (Test-Path -LiteralPath $candidate)) {
            continue
        }

        # Paths were canonicalized against the caller's working directory at start-up, so this is
        # safe even when the operator passed a relative path.
        $candidateDir = if (Test-Path -LiteralPath $candidate -PathType Container) {
            $candidate
        }
        else {
            Split-Path -Parent $candidate
        }

        if (-not [string]::IsNullOrWhiteSpace($candidateDir)) {
            $searchRoots.Add($candidateDir)
            $searchRoots.Add((Join-Path $candidateDir "prerequisites"))
        }
    }

    foreach ($root in $searchRoots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $match = Get-ChildItem -LiteralPath $root -Filter "dotnet-hosting-10.*.exe" -File -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            Select-Object -First 1
        if ($null -ne $match) {
            Write-Detail "Discovered Hosting Bundle installer: $($match.FullName)"
            return $match.FullName
        }
    }

    return $null
}

function Resolve-HostingBundleExpectedHash {
    param([Parameter(Mandatory = $true)][string]$InstallerPath)

    if (-not [string]::IsNullOrWhiteSpace($HostingBundleSha256)) {
        return ($HostingBundleSha256.Trim() -replace '\s', '').ToLowerInvariant()
    }

    # The distribution manifest is the authority when the prerequisite came through the release.
    if ($null -ne $Script:DistributionPrerequisites) {
        foreach ($prerequisite in $Script:DistributionPrerequisites) {
            if ("$($prerequisite.fileName)" -eq (Split-Path -Leaf $InstallerPath)) {
                return ([string]$prerequisite.sha256).ToLowerInvariant()
            }
        }
    }

    $sidecar = "$InstallerPath.sha256"
    if (-not (Test-Path -LiteralPath $sidecar)) {
        throw ("Hosting Bundle integrity sidecar missing: {0}. Provide -HostingBundleSha256 or create the sidecar " +
               "containing the lowercase hex SHA-256 of the installer. Arbitrary executables will not be run.") -f $sidecar
    }

    $raw = (Get-Content -LiteralPath $sidecar -Raw).Trim()
    # Accept "hash", "hash  filename", or "hash *filename"
    if ($raw -match '^([A-Fa-f0-9]{64})\b') {
        return $Matches[1].ToLowerInvariant()
    }

    throw "Hosting Bundle sidecar '$sidecar' does not contain a 64-character hex SHA-256."
}

function Install-RequiredIisFeatures {
    param(
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][string[]]$FeatureNames
    )

    $Script:CurrentStep = "ProvisionIisFeatures"
    Set-Phase -State $State -Phase "ProvisioningPrerequisites"
    Write-Step "Installing IIS Windows features"

    # Only the authoritative required set - never IncludeAllSubFeature.
    $toInstall = @($FeatureNames | Select-Object -Unique)
    Write-Detail ("Features: {0}" -f ($toInstall -join ", "))

    # Record BEFORE the change: if this run turns IIS on, the Default Web Site that appears is a
    # pristine artifact of our provisioning, and later binding-ownership decisions depend on knowing
    # that rather than guessing from a site name.
    if ($toInstall -contains "Web-Server") {
        $State | Add-Member -NotePropertyName "iisProvisionedByInstaller" -NotePropertyValue $true -Force
        Save-InstallationState -State $State
        Write-Detail "Recorded that IIS is being provisioned by this installation."
    }

    $result = Install-WindowsFeature -Name $toInstall -ErrorAction Stop
    $restartNeeded = $false
    if ($null -ne $result) {
        # RestartNeeded can be Yes / No / Maybe depending on ServerManager version.
        $restartValue = [string]$result.RestartNeeded
        if ($restartValue -match '^(Yes|True)$') {
            $restartNeeded = $true
        }
        if ($result.Success -eq $false) {
            throw "Install-WindowsFeature reported failure for: $($toInstall -join ', ')."
        }
    }

    if ($restartNeeded) {
        Set-Phase -State $State -Phase "AwaitingReboot"
        throw "IIS feature installation requires a reboot before the installer can continue. " +
              "Reboot this server, then re-run the same command. Phase is recorded as AwaitingReboot."
    }

    Write-Ok "IIS feature installation completed without an immediate reboot request"
}

function Install-AspNetCoreHostingBundle {
    param(
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][bool]$Repair
    )

    $Script:CurrentStep = "ProvisionHostingBundle"
    Set-Phase -State $State -Phase "ProvisioningPrerequisites"
    Write-Step $(if ($Repair) { "Repairing ASP.NET Core Hosting Bundle" } else { "Installing ASP.NET Core Hosting Bundle" })

    $installer = Resolve-HostingBundleInstaller
    if ([string]::IsNullOrWhiteSpace($installer)) {
        throw ("No offline Hosting Bundle installer was found. Download {0} on a machine with internet, " +
               "copy it to this server, place a SHA-256 sidecar beside it, and pass -HostingBundlePath " +
               "(or put it under scripts/install/prerequisites). This installer never downloads prerequisites.") -f `
               $Script:HostingBundleRequirement.DisplayName
    }

    # A prerequisite reassembled from the distribution was already verified against the digest the
    # release pinned, by the manifest, before this point. Re-deriving an expected hash from a sidecar
    # would be asking the operator for something the release already stated authoritatively.
    $expectedHash = Resolve-HostingBundleExpectedHash -InstallerPath $installer
    $actualHash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw ("Hosting Bundle installer integrity check failed.`n  path:     {0}`n  expected: {1}`n  actual:   {2}") -f `
            $installer, $expectedHash, $actualHash
    }
    Write-Ok "Hosting Bundle installer integrity verified (SHA-256)"

    $logPath = Join-Path $env:TEMP ("itadmin-hosting-bundle-{0}.log" -f (Get-Date -Format "yyyyMMddHHmmss"))
    $args = "/install /quiet /norestart /log `"$logPath`""
    if ($Repair) {
        $args = "/repair /quiet /norestart /log `"$logPath`""
    }

    Write-Detail "Running Hosting Bundle installer (log: $logPath)"
    # Do not echo full argument lines that could later include secrets; this installer has none.
    $process = Start-Process -FilePath $installer -ArgumentList $args -Wait -PassThru
    $exitCode = $process.ExitCode

    if ($exitCode -eq 0) {
        Write-Ok "Hosting Bundle installer completed successfully"
    }
    elseif ($exitCode -eq $Script:HostingBundleRequirement.SuccessRebootExitCode) {
        Set-Phase -State $State -Phase "AwaitingReboot"
        throw "Hosting Bundle installation succeeded but requires a reboot (exit 3010). " +
              "Reboot this server, then re-run the same command. Phase is recorded as AwaitingReboot."
    }
    else {
        throw "Hosting Bundle installer failed with exit code $exitCode. Inspect '$logPath' (secrets are not written to this log by the installer)."
    }

    # ANCM registration requires IIS services to pick up the module.
    Restart-IisAfterHostingBundle
}

function Restart-IisAfterHostingBundle {
    Write-Detail "Restarting IIS / WAS so ANCM registration is visible"
    try {
        $ErrorActionPreference = "Continue"
        & "$env:windir\system32\inetsrv\appcmd.exe" stop site /site.name:* 2>$null | Out-Null
        Restart-Service -Name W3SVC,WAS -Force -ErrorAction SilentlyContinue
        Start-Service -Name WAS -ErrorAction SilentlyContinue
        Start-Service -Name W3SVC -ErrorAction SilentlyContinue
    }
    finally {
        $ErrorActionPreference = "Stop"
    }
}

function Invoke-PrerequisiteProvisioning {
    param(
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][bool]$AllowProvision
    )

    Write-Step "Prerequisite detection"
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    Write-Detail "OS: $($os.Caption) ($($os.Version))"
    if ([version]$os.Version -lt [version]"10.0.20348") {
        throw "Windows Server 2022 or newer is required (found $($os.Version))."
    }
    if ($os.ProductType -eq 1) {
        throw "Client Windows is not supported. ITAdmin production installation requires Windows Server 2022 or 2025."
    }

    $maxCycles = 4
    for ($cycle = 1; $cycle -le $maxCycles; $cycle++) {
        $detection = Get-PrerequisiteDetection
        Write-PrerequisiteDetection -Detection $detection
        $problems = Get-PrerequisiteBlockingProblems -Detection $detection

        if ($problems.Count -eq 0) {
            Write-Ok "All required prerequisites confirmed"
            return $detection
        }

        if (-not $AllowProvision) {
            foreach ($problem in $problems) { Write-Fail $problem }
            throw ("Preflight failed with {0} blocking problem(s). No machine changes were made. " +
                   "Re-run with -ProvisionPrerequisites (and -HostingBundlePath when the Hosting Bundle is missing) " +
                   "to install required prerequisites.") -f $problems.Count
        }

        Write-Detail ("Provisioning cycle {0}/{1}" -f $cycle, $maxCycles)

        if ($detection.RestartPending) {
            Set-Phase -State $State -Phase "AwaitingReboot"
            foreach ($problem in $problems) { Write-Fail $problem }
            throw "A restart is pending. Reboot, then re-run. Phase is recorded as AwaitingReboot."
        }

        if ($detection.MissingIisFeatures.Count -gt 0) {
            Install-RequiredIisFeatures -State $State -FeatureNames $detection.MissingIisFeatures
            continue
        }

        if (-not $detection.WebAdministrationAvailable) {
            Install-RequiredIisFeatures -State $State -FeatureNames @("Web-Scripting-Tools", "Web-Mgmt-Console")
            continue
        }

        if (-not $detection.HostingBundleUsable) {
            $repair = $detection.AncmPresent -and $detection.SharedFrameworkOk -and -not $detection.AncmModuleRegistered
            Install-AspNetCoreHostingBundle -State $State -Repair:$repair
            continue
        }

        foreach ($problem in $problems) { Write-Fail $problem }
        throw "Prerequisites remain unsatisfied after provisioning attempts."
    }

    throw "Prerequisite provisioning exceeded $maxCycles detection cycles without reaching a ready state."
}

function Test-Preflight {
    param(
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][bool]$AllowProvision
    )

    Invoke-PrerequisiteProvisioning -State $State -AllowProvision $AllowProvision | Out-Null
}

# --------------------------------------------------------------------------------------------
# Artifact validation
# --------------------------------------------------------------------------------------------

function Read-ValidatedReleaseDirectory {
    <#
        The canonical input path: a release tree the bootstrap (or the Host Agent) already fetched
        from the repository's distribution ref.

        Verification here is identity as well as integrity. A distribution ref only proves the
        remote had something at that name; what makes it trustworthy is that its manifest agrees
        with the annotated tag we resolved - same version, same peeled source commit. Both are
        checked before a byte is staged, and a mismatch fails the run.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$RequiredVersion,
        [string]$RequiredSourceCommit
    )

    Write-Step "Validating acquired release"

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "Release directory not found: $Path"
    }

    $manifestPath = Join-Path $Path "release.manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "The acquired tree is not an ITAdmin release: release.manifest.json is missing."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-ManifestShape -Manifest $manifest

    $version = $null
    [void][version]::TryParse((($manifest.source.version) -split '-')[0], [ref]$version)

    if (-not [string]::IsNullOrWhiteSpace($RequiredVersion) -and
        $manifest.source.version -ne $RequiredVersion) {
        throw "Distribution declares source version '$($manifest.source.version)' but the requested " +
              "release is '$RequiredVersion'. The distribution ref does not carry the release it claims to."
    }

    if (-not [string]::IsNullOrWhiteSpace($RequiredSourceCommit)) {
        if (-not (Test-CommitsMatch -Left $manifest.source.commit -Right $RequiredSourceCommit)) {
            throw "Distribution was published for source commit '$($manifest.source.commit)' but the " +
                  "release tag peels to '$RequiredSourceCommit'. Refusing to install a payload that does " +
                  "not match its tag."
        }
        Write-Ok "Distribution source commit matches the annotated release tag"
    }

    Write-Detail "Release:      $($manifest.source.version) (tag $($manifest.source.tag))"
    Write-Detail "Source commit: $($manifest.source.commit)"
    Write-Detail "Built:        $($manifest.distribution.builtAtUtc)"
    Write-Detail "Migrations:   $($manifest.migrations.count) (latest $($manifest.migrations.latest))"

    Test-DistributionIsClosed -DistributionRoot $Path -Manifest $manifest

    # Every declared component, not just the payload. The Host Agent runs as LocalSystem and the
    # prerequisite chunks become an executable, so they get the same treatment as the application.
    $totalFiles = 0
    foreach ($component in $manifest.components.PSObject.Properties) {
        $componentRoot = Join-Path $Path ($component.Name -replace '/', '\')
        if (-not (Test-Path -LiteralPath $componentRoot -PathType Container)) {
            throw "Declared component '$($component.Name)' is missing from the distribution."
        }

        Test-ComponentIntegrity -ComponentRoot $componentRoot -Integrity $component.Value.integrity `
            -ComponentName "Component '$($component.Name)'"
        $totalFiles += [int]$component.Value.integrity.fileCount
    }

    $Script:DistributionRoot = $Path
    $Script:DistributionPrerequisites = @($manifest.prerequisites)

    $prerequisiteNote = if ($Script:DistributionPrerequisites.Count -gt 0) {
        ", $($Script:DistributionPrerequisites.Count) runtime prerequisite(s)"
    }
    else { "" }

    Write-Ok "Distribution verified ($($manifest.components.PSObject.Properties.Count) component(s), $totalFiles files$prerequisiteNote)"

    return [pscustomobject]@{
        # Nothing to clean up: this tree is owned by the caller that fetched it.
        ExtractRoot  = $null
        PayloadRoot  = (Join-Path $Path "app")
        ManifestPath = $manifestPath
        Manifest     = $manifest
        Version      = $version
        VersionText  = $manifest.source.version
        SourceRoot   = $Path
    }
}

function Test-CommitsMatch {
    <#
        Commit ids may legitimately differ in width between a manifest written by CI and a peel
        line read from a remote, so an abbreviation matches - but only from 7 characters up, so an
        empty or truncated value can never pass as a match.
    #>
    param([string]$Left, [string]$Right)

    if ([string]::IsNullOrWhiteSpace($Left) -or [string]::IsNullOrWhiteSpace($Right)) {
        return $false
    }

    $a = $Left.Trim().ToLowerInvariant()
    $b = $Right.Trim().ToLowerInvariant()

    if ($a.Length -lt 7 -or $b.Length -lt 7) { return $false }
    if ($a -notmatch '^[0-9a-f]+$' -or $b -notmatch '^[0-9a-f]+$') { return $false }

    $shortest = [Math]::Min($a.Length, $b.Length)
    return $a.Substring(0, $shortest) -eq $b.Substring(0, $shortest)
}

function Assert-ManifestShape {
    <#
        Mirrors ITAdmin.Deployment.ReleaseManifest.Validate (drift-tested).

        Source identity and distribution identity are checked against EACH OTHER here. The publisher
        records them independently, so a disagreement means it built one thing and labelled it
        another - which is exactly the case that must never reach a server.
    #>
    param([Parameter(Mandatory = $true)][psobject]$Manifest)

    if ($Manifest.schemaVersion -ne 2) {
        throw "Unsupported distribution manifest schemaVersion $($Manifest.schemaVersion); this installer supports 2."
    }
    if ($Manifest.product -ne "ITAdmin") {
        throw "Distribution product is '$($Manifest.product)', expected 'ITAdmin'."
    }

    $parsed = $null
    if (-not [version]::TryParse((($Manifest.source.version) -split '-')[0], [ref]$parsed)) {
        throw "Manifest source.version '$($Manifest.source.version)' is not a valid version."
    }

    if ($Manifest.source.version -ne $Manifest.distribution.version) {
        throw "Distribution version '$($Manifest.distribution.version)' does not match source release " +
              "version '$($Manifest.source.version)'."
    }

    if (-not (Test-CommitsMatch -Left $Manifest.source.commit -Right $Manifest.distribution.sourceCommit)) {
        throw "Distribution sourceCommit does not match the source release commit; the payload was not " +
              "built from the commit this release claims."
    }

    if ($null -eq $Manifest.components -or
        $null -eq $Manifest.components.PSObject.Properties["app"]) {
        throw "Distribution declares no 'app' component; there is nothing for IIS to serve."
    }
}

function Test-DistributionIsClosed {
    <#
        Every file in the distribution must belong to a declared component.

        Verifying only declared files would let an extra executable ride along in the tree,
        unverified and unmentioned, next to binaries this installer is about to run as SYSTEM.
        Git's own metadata is exempt: a fetched distribution is a working tree, and .git is never
        staged - the installer copies declared components explicitly, not the directory wholesale.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$DistributionRoot,
        [Parameter(Mandatory = $true)][psobject]$Manifest
    )

    $declared = @($Manifest.components.PSObject.Properties.Name | ForEach-Object { ($_ -replace '/', '\') + '\' })
    $problems = New-Object System.Collections.Generic.List[string]

    $rootLength = (Resolve-Path -LiteralPath $DistributionRoot).Path.Length
    foreach ($file in (Get-ChildItem -LiteralPath $DistributionRoot -Recurse -File -Force)) {
        $relative = $file.FullName.Substring($rootLength).TrimStart('\')

        if ($relative -eq "release.manifest.json") { continue }
        if ($relative -like ".git\*") { continue }

        $covered = $false
        foreach ($prefix in $declared) {
            if ($relative.StartsWith($prefix, [StringComparison]::Ordinal)) { $covered = $true; break }
        }

        if (-not $covered) {
            $problems.Add("undeclared content: $($relative -replace '\\', '/')")
            if ($problems.Count -ge 20) { break }
        }
    }

    if ($problems.Count -gt 0) {
        foreach ($problem in $problems) { Write-Fail $problem }
        throw "The distribution carries content its manifest does not declare. Nothing undeclared is " +
              "installed; refusing to continue."
    }
}

function Expand-AndValidateArtifact {
    <#
        Retained offline/developer mode. Kept because a fully air-gapped site still needs a way in,
        and because it is how a developer tests a build without publishing a release. It is
        explicitly NOT the product installation path.
    #>
    param([Parameter(Mandatory = $true)][string]$Path)

    Write-Step "Validating release artifact (offline mode)"

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Release artifact not found: $Path"
    }

    $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("itadmin-artifact-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($Path, $extractRoot)

    $manifestPath = Join-Path $extractRoot "release.manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Artifact is not an ITAdmin release: release.manifest.json is missing."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-ManifestShape -Manifest $manifest

    $version = $null
    [void][version]::TryParse((($manifest.source.version) -split '-')[0], [ref]$version)

    Write-Detail "Version:    $($manifest.source.version)"
    Write-Detail "Commit:     $($manifest.source.commit)"
    Write-Detail "Built:      $($manifest.distribution.builtAtUtc)"
    Write-Detail "Migrations: $($manifest.migrations.count) (latest $($manifest.migrations.latest))"

    Test-DistributionIsClosed -DistributionRoot $extractRoot -Manifest $manifest

    foreach ($component in $manifest.components.PSObject.Properties) {
        $componentRoot = Join-Path $extractRoot ($component.Name -replace '/', '\')
        if (-not (Test-Path -LiteralPath $componentRoot -PathType Container)) {
            throw "Declared component '$($component.Name)' is missing from the artifact."
        }

        Test-ComponentIntegrity -ComponentRoot $componentRoot -Integrity $component.Value.integrity `
            -ComponentName "Component '$($component.Name)'"
    }

    $Script:DistributionRoot = $extractRoot
    $Script:DistributionPrerequisites = @($manifest.prerequisites)

    Write-Ok "Artifact verified ($($manifest.components.PSObject.Properties.Count) component(s))"

    return [pscustomobject]@{
        ExtractRoot  = $extractRoot
        PayloadRoot  = (Join-Path $extractRoot "app")
        ManifestPath = $manifestPath
        Manifest     = $manifest
        Version      = $version
        VersionText  = $manifest.source.version
        SourceRoot   = $extractRoot
    }
}

function Test-ComponentIntegrity {
    <#
        Per-file SHA-256 over one component of a release, against the digests the build recorded.

        An UNEXPECTED file is a failure, not a curiosity: a release directory is meant to be an
        exact reproduction of the build output, and a stray file in it means something other than
        the build wrote there.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ComponentRoot,
        [Parameter(Mandatory = $true)][psobject]$Integrity,
        [Parameter(Mandatory = $true)][string]$ComponentName
    )

    if ($Integrity.algorithm -ne "SHA-256") {
        throw "Unsupported integrity algorithm '$($Integrity.algorithm)'."
    }

    $expected = @{}
    foreach ($property in $Integrity.files.PSObject.Properties) {
        $expected[$property.Name] = $property.Value
    }

    if ($expected.Count -ne $Integrity.fileCount) {
        throw "Manifest fileCount is $($Integrity.fileCount) but $($expected.Count) digests are present."
    }

    $problems = New-Object System.Collections.Generic.List[string]

    foreach ($relativePath in $expected.Keys) {
        # Reject anything that could escape the component root before touching the filesystem.
        if ($relativePath -match '(^/)|(\\)|(:)|(^\.\.)|(/\.\./)|(/\.\.$)') {
            $problems.Add("unsafe manifest path: $relativePath")
            continue
        }

        $fullPath = Join-Path $ComponentRoot ($relativePath -replace '/', '\')
        if (-not (Test-Path -LiteralPath $fullPath)) {
            $problems.Add("missing: $relativePath")
            continue
        }

        $actual = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected[$relativePath]) {
            $problems.Add("altered: $relativePath")
        }
    }

    $actualFiles = Get-ChildItem -LiteralPath $ComponentRoot -Recurse -File -Force
    foreach ($file in $actualFiles) {
        $relative = $file.FullName.Substring($ComponentRoot.Length).TrimStart('\') -replace '\\', '/'
        if (-not $expected.ContainsKey($relative)) {
            $problems.Add("unexpected: $relative")
        }
    }

    if ($problems.Count -gt 0) {
        foreach ($problem in $problems | Select-Object -First 20) { Write-Fail $problem }
        throw "$ComponentName integrity verification failed with $($problems.Count) problem(s). It will not be installed."
    }
}

# --------------------------------------------------------------------------------------------
# Environment configuration
# --------------------------------------------------------------------------------------------

function Resolve-EnvironmentConfig {
    <#
        Collects the minimum an installation needs to host ITAdmin over HTTP.

        Two deliberate omissions compared with the previous contract: no application FQDN, and no
        certificate. Neither is needed to bring the product to a usable first-login state, and
        demanding them turned "install ITAdmin" into "install ITAdmin, once DNS and the PKI team are
        ready". Both are configured later from ITAdmin Settings and applied by the Host Agent.
    #>
    Write-Step "Resolving environment configuration"

    $existing = $null
    if (Test-Path -LiteralPath $Script:EnvironmentConfigPath) {
        # Machine config survives release changes; reuse it so a repair/update does not re-prompt.
        $existing = Get-Content -LiteralPath $Script:EnvironmentConfigPath -Raw | ConvertFrom-Json
        Write-Detail "Existing environment configuration found; supplied parameters override it."
    }

    $dbHost = Resolve-RequiredValue -Supplied $DatabaseHost `
        -Existing $(if ($existing) { $existing.database.host }) `
        -Prompt "PostgreSQL host" -Name "DatabaseHost"

    $dbName = Resolve-RequiredValue -Supplied $DatabaseName `
        -Existing $(if ($existing) { $existing.database.name }) `
        -Prompt "PostgreSQL database name" -Name "DatabaseName"

    $dbUser = Resolve-RequiredValue -Supplied $DatabaseUser `
        -Existing $(if ($existing) { $existing.database.username }) `
        -Prompt "PostgreSQL user" -Name "DatabaseUser"

    $dbPort = if ($PSBoundParameters.ContainsKey('DatabasePort')) { $DatabasePort }
              elseif ($existing) { [int]$existing.database.port }
              else { $DatabasePort }

    # A previously configured public host name and certificate are preserved verbatim: an update
    # must not silently drop HTTPS that an administrator turned on after the first install.
    $preservedFqdn = if ($existing) { $existing.applicationFqdn } else { $null }
    $preservedHttps = if ($existing -and $existing.web -and $existing.web.https) {
        [pscustomobject]@{
            enabled               = [bool]$existing.web.https.enabled
            port                  = [int]$existing.web.https.port
            certificateThumbprint = $existing.web.https.certificateThumbprint
            redirectHttpToHttps   = [bool]$existing.web.https.redirectHttpToHttps
        }
    }
    else {
        [pscustomobject]@{
            enabled               = $false
            port                  = 443
            certificateThumbprint = $null
            redirectHttpToHttps   = $false
        }
    }

    $hostHeader = if ($PSBoundParameters.ContainsKey('HttpHostHeader')) { $HttpHostHeader }
                  elseif ($existing -and $existing.web) { $existing.web.httpHostHeader }
                  else { $null }

    $config = [pscustomobject]@{
        schemaVersion   = 2
        applicationFqdn = $preservedFqdn
        web             = [pscustomobject]@{
            httpPort       = $HttpPort
            httpHostHeader = $(if ([string]::IsNullOrWhiteSpace($hostHeader)) { $null } else { $hostHeader })
            https          = $preservedHttps
        }
        database        = [pscustomobject]@{
            host     = $dbHost
            port     = $dbPort
            name     = $dbName
            username = $dbUser
            sslMode  = "Prefer"
        }
        iis             = [pscustomobject]@{
            siteName    = $SiteName
            appPoolName = $AppPoolName
        }
    }

    Assert-EnvironmentConfigValid -Config $config
    Write-Ok "Environment configuration resolved (initial hosting: HTTP)"
    return $config
}

function Resolve-RequiredValue {
    <#
        Supplied parameter wins, then previously stored machine configuration, then a prompt.
        In -Unattended mode there is no console to answer, so a missing value fails immediately
        rather than blocking a service-initiated update forever on a hidden prompt.
    #>
    param(
        [string]$Supplied,
        [string]$Existing,
        [Parameter(Mandatory = $true)][string]$Prompt,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if (-not [string]::IsNullOrWhiteSpace($Supplied)) { return $Supplied }
    if (-not [string]::IsNullOrWhiteSpace($Existing)) { return $Existing }

    if ($Unattended.IsPresent) {
        throw "-$Name is required in unattended mode."
    }

    $value = Read-Host $Prompt
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "$Prompt is required."
    }

    return $value
}

function Read-RequiredSecret {
    param(
        [SecureString]$Supplied,
        [Parameter(Mandatory = $true)][string]$Prompt,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -ne $Supplied) { return $Supplied }

    if ($Unattended.IsPresent) {
        throw "-$Name is required in unattended mode."
    }

    # -AsSecureString so the value never appears on screen, in the console history, or in a
    # transcript.
    $secure = Read-Host -AsSecureString $Prompt
    if ($null -eq $secure -or $secure.Length -eq 0) {
        throw "$Prompt is required."
    }

    return $secure
}

function Assert-EnvironmentConfigValid {
    param([Parameter(Mandatory = $true)][psobject]$Config)

    $errors = New-Object System.Collections.Generic.List[string]

    if ([string]::IsNullOrWhiteSpace($Config.database.host) -or
        [Uri]::CheckHostName($Config.database.host) -eq [UriHostNameType]::Unknown) {
        $errors.Add("database.host must be a valid host name or address.")
    }
    if ($Config.database.port -lt 1 -or $Config.database.port -gt 65535) {
        $errors.Add("database.port must be between 1 and 65535.")
    }
    if ([string]::IsNullOrWhiteSpace($Config.database.name)) { $errors.Add("database.name is required.") }
    if ([string]::IsNullOrWhiteSpace($Config.database.username)) { $errors.Add("database.username is required.") }
    if ($Config.web.httpPort -lt 1 -or $Config.web.httpPort -gt 65535) {
        $errors.Add("web.httpPort must be between 1 and 65535.")
    }
    if (-not [string]::IsNullOrWhiteSpace($Config.web.httpHostHeader) -and
        [Uri]::CheckHostName($Config.web.httpHostHeader) -eq [UriHostNameType]::Unknown) {
        $errors.Add("web.httpHostHeader, when set, must be a valid host name.")
    }

    # HTTPS is never configured by an initial install, but a preserved configuration is still
    # validated so an update cannot carry a half-configured binding forward.
    if ($Config.web.https.enabled) {
        if ([string]::IsNullOrWhiteSpace($Config.web.https.certificateThumbprint)) {
            $errors.Add("web.https.certificateThumbprint is required when HTTPS is enabled.")
        }
        if ([string]::IsNullOrWhiteSpace($Config.applicationFqdn)) {
            $errors.Add("applicationFqdn is required before HTTPS can be enabled.")
        }
    }

    if ($errors.Count -gt 0) {
        foreach ($problem in $errors) { Write-Fail $problem }
        throw "Environment configuration is invalid. No machine changes were made."
    }
}

function Get-LocalAccessUrls {
    <#
        The URLs an operator should actually try, built from what this machine reports about
        itself. Nothing here is a product default: an ITAdmin installed anywhere prints the names
        that host already has.
    #>
    param([Parameter(Mandatory = $true)][psobject]$Config)

    $names = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($Config.web.httpHostHeader)) {
        # A host header binding answers on exactly that name; printing anything else sends the
        # operator to a URL that returns 404.
        $names.Add($Config.web.httpHostHeader)
    }
    else {
        try {
            $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop
            if ($computerSystem.PartOfDomain -and -not [string]::IsNullOrWhiteSpace($computerSystem.Domain)) {
                $names.Add(("{0}.{1}" -f $computerSystem.Name, $computerSystem.Domain))
            }
            $names.Add($computerSystem.Name)
        }
        catch {
            Write-Verbose "Machine name discovery failed: $($_.Exception.Message)"
        }

        try {
            $addresses = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop |
                Where-Object { $_.IPAddress -ne "127.0.0.1" -and $_.PrefixOrigin -ne "WellKnown" }
            foreach ($address in $addresses) { $names.Add($address.IPAddress) }
        }
        catch {
            Write-Verbose "Address discovery failed: $($_.Exception.Message)"
        }
    }

    if ($names.Count -eq 0) { $names.Add("localhost") }

    $port = $Config.web.httpPort
    $urls = New-Object System.Collections.Generic.List[string]
    foreach ($name in $names) {
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        $authority = if ($port -eq 80) { $name } else { "${name}:${port}" }
        $url = "http://$authority/"
        if (-not $urls.Contains($url)) { $urls.Add($url) }
    }

    return $urls
}

# --------------------------------------------------------------------------------------------
# Machine configuration
# --------------------------------------------------------------------------------------------

$Script:CurrentStep = "Start"

# Established during machine configuration; consumed by the directory bootstrap step. Held in a
# script variable rather than passed around so it is never written to a file, a log, or a summary.
$Script:SetupKey = $null
$Script:LastMigrationApplied = $null

# Prerequisites declared by the distribution being installed, and the tree they live in. Set once
# the release has been verified, so nothing reads them before their digests have been checked.
$Script:DistributionPrerequisites = $null
$Script:DistributionRoot = $null

# The four independent conditions that together mean "somebody can log in". Each is set only when
# it has actually been proven, and the run refuses to record Installed unless all four hold.
$Script:Readiness = [pscustomobject]@{
    processHealthy            = $false
    setupCompleted            = $false
    directoryUsable           = $false
    administratorBootstrapped = $false
}

function New-MachineDirectories {
    $Script:CurrentStep = "CreateDirectories"
    Write-Step "Creating filesystem layout"

    foreach ($directory in @(
            $Script:Layout.ProgramFilesRoot, $Script:Layout.ReleasesRoot,
            $Script:Layout.ProgramDataRoot, $Script:Layout.ConfigRoot, $Script:Layout.SecretsRoot,
            $Script:Layout.StateRoot, $Script:Layout.DataProtectionRoot,
            $Script:Layout.LogsRoot, $Script:Layout.BackupsRoot)) {
        if (-not (Test-Path -LiteralPath $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            Write-Detail "created $directory"
        }
    }

    Write-Ok "Layout ready"
}

function Publish-StagedRelease {
    <#
        Stages into a temporary directory, verifies it, and only then moves it into place under a
        version-named directory. A half-written release is therefore never visible at the path IIS
        could be pointed at.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Artifact,
        [Parameter(Mandatory = $true)][psobject]$State
    )

    $Script:CurrentStep = "Stage"
    Set-Phase -State $State -Phase "Staging"
    Write-Step "Staging release $($Artifact.VersionText)"

    $releaseRoot = Join-Path $Script:Layout.ReleasesRoot $Artifact.VersionText
    $payloadRoot = Join-Path $releaseRoot "app"

    # Guard: never let a crafted version string turn staging into a write outside the releases root.
    $resolvedReleases = [System.IO.Path]::GetFullPath($Script:Layout.ReleasesRoot).TrimEnd('\')
    $resolvedRelease = [System.IO.Path]::GetFullPath($releaseRoot).TrimEnd('\')
    if (-not $resolvedRelease.StartsWith($resolvedReleases + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to stage outside the releases root: $releaseRoot"
    }

    if (Test-Path -LiteralPath $releaseRoot) {
        # Same-version rerun: replace the release payload only. Machine state lives in ProgramData
        # and is untouched by this.
        Write-Detail "Replacing existing release directory for $($Artifact.VersionText)."
        Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    }

    $stagingRoot = "$releaseRoot.staging"
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
    # Copy the manifest and the application payload only. A distribution fetched from a ref also
    # carries Git's own .git directory, which must never be staged into Program Files - and the
    # Host Agent and prerequisite components belong outside the release directory entirely.
    Copy-Item -LiteralPath $Artifact.ManifestPath -Destination (Join-Path $stagingRoot "release.manifest.json") -Force
    Copy-Item -LiteralPath $Artifact.PayloadRoot -Destination (Join-Path $stagingRoot "app") -Recurse -Force

    Write-Detail "Re-verifying integrity of the staged copy"
    Test-ComponentIntegrity -ComponentRoot (Join-Path $stagingRoot "app") `
        -Integrity $Artifact.Manifest.components.app.integrity -ComponentName "Staged application payload"

    Move-Item -LiteralPath $stagingRoot -Destination $releaseRoot

    # Release files are read+execute for the app pool; the app never writes into its own release.
    $identity = "IIS AppPool\$($AppPoolName)"
    & icacls $releaseRoot /grant "${identity}:(OI)(CI)RX" /T | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply ACLs to $releaseRoot (icacls exit $LASTEXITCODE)."
    }

    $State.stagedVersion = $Artifact.VersionText
    Set-Phase -State $State -Phase "Staged"
    Write-Ok "Staged and verified at $releaseRoot"

    return [pscustomobject]@{ ReleaseRoot = $releaseRoot; PayloadRoot = $payloadRoot }
}

function Save-EnvironmentConfig {
    param([Parameter(Mandatory = $true)][psobject]$Config)

    $Script:CurrentStep = "SaveEnvironmentConfig"
    $Config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Script:EnvironmentConfigPath -Encoding UTF8
    Write-Detail "Environment configuration written to $Script:EnvironmentConfigPath"
}

function Resolve-DatabaseConnectionString {
    <#
        Builds the Npgsql connection string. The password is taken from -DatabasePassword or
        prompted for, is never written to the config file or the installation state, and is stored
        only in the DPAPI-protected machine secret store under ProgramData.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][psobject]$State
    )

    $Script:CurrentStep = "ResolveDatabase"
    Write-Step "Configuring database connection"

    $existingSecrets = Read-MachineSecrets
    if ($null -eq $DatabasePassword -and $null -ne $existingSecrets -and
        -not [string]::IsNullOrWhiteSpace($existingSecrets.connectionString)) {
        Write-Detail "Reusing the previously configured database connection from the machine secret store."
        return $existingSecrets.connectionString
    }

    $secure = $DatabasePassword
    if ($null -eq $secure) {
        $secure = Read-Host -AsSecureString "PostgreSQL password for '$($Config.database.username)'"
    }

    $plain = ConvertFrom-SecureStringToPlainText -Secure $secure
    if ([string]::IsNullOrWhiteSpace($plain)) {
        throw "A database password is required."
    }

    $builder = "Host=$($Config.database.host);Port=$($Config.database.port);" +
               "Database=$($Config.database.name);Username=$($Config.database.username);" +
               "Password=$plain;SSL Mode=$($Config.database.sslMode);Trust Server Certificate=true"

    Write-Ok "Database target: $($Config.database.host):$($Config.database.port)/$($Config.database.name)"
    return $builder
}

function ConvertFrom-SecureStringToPlainText {
    param([Parameter(Mandatory = $true)][SecureString]$Secure)

    $pointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function New-RandomSecret {
    param([int]$ByteCount = 48)

    $bytes = New-Object byte[] $ByteCount
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }

    return [Convert]::ToBase64String($bytes)
}

function Get-MachineSecretsPath {
    return (Join-Path $Script:Layout.SecretsRoot "runtime.secrets.dpapi")
}

function Read-MachineSecrets {
    $path = Get-MachineSecretsPath
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    Add-Type -AssemblyName System.Security -ErrorAction SilentlyContinue
    $protected = [System.IO.File]::ReadAllBytes($path)
    if ($null -eq $protected -or $protected.Length -eq 0) {
        return $null
    }

    try {
        $plainBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
            $protected, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
        $json = [System.Text.Encoding]::UTF8.GetString($plainBytes)
        return $json | ConvertFrom-Json
    }
    catch {
        throw "Machine secret store at $path could not be decrypted. " +
              "LocalMachine DPAPI secrets do not travel to another host. Re-enter secrets or restore this machine's DPAPI state."
    }
}

function Get-SetupKeyHash {
    <#
        sha256:<base64url> - the form the application's Setup:SetupKeyHash configuration expects.
        Only the hash ever reaches application configuration; the plaintext key stays in the
        DPAPI store for the installer's directory-bootstrap step.
    #>
    param([Parameter(Mandatory = $true)][string]$SetupKey)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($SetupKey))
    }
    finally {
        $sha256.Dispose()
    }

    return "sha256:" + ([Convert]::ToBase64String($hashBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'))
}

function Save-MachineSecrets {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$JwtKey,
        [Parameter(Mandatory = $true)][string]$SetupKey
    )

    if (-not (Test-Path -LiteralPath $Script:Layout.SecretsRoot)) {
        New-Item -ItemType Directory -Path $Script:Layout.SecretsRoot -Force | Out-Null
    }

    $payload = [pscustomobject]@{
        schemaVersion    = 1
        connectionString = $ConnectionString
        jwtKey           = $JwtKey
        setupKey         = $SetupKey
        setupKeyHash     = (Get-SetupKeyHash -SetupKey $SetupKey)
    }

    $json = $payload | ConvertTo-Json -Compress
    $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($json)

    Add-Type -AssemblyName System.Security -ErrorAction SilentlyContinue
    $protected = [System.Security.Cryptography.ProtectedData]::Protect(
        $plainBytes, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)

    $path = Get-MachineSecretsPath
    [System.IO.File]::WriteAllBytes($path, $protected)

    # Tighten ACLs: SYSTEM + Administrators full, app pool read. No inheritance from ProgramData.
    $identity = "IIS AppPool\$AppPoolName"
    & icacls $Script:Layout.SecretsRoot /inheritance:r | Out-Null
    & icacls $Script:Layout.SecretsRoot /grant:r "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null
    & icacls $Script:Layout.SecretsRoot /grant:r "${identity}:(OI)(CI)R" | Out-Null
    & icacls $path /inheritance:r | Out-Null
    & icacls $path /grant:r "SYSTEM:F" "Administrators:F" "${identity}:R" | Out-Null

    Write-Detail "Machine secrets written to DPAPI-protected store under $($Script:Layout.SecretsRoot)"
}

function Get-AppPoolEnvironmentVariable {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        return $null
    }

    $collection = Get-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "environmentVariables" -ErrorAction SilentlyContinue
    if ($null -eq $collection -or $null -eq $collection.Collection) {
        return $null
    }

    foreach ($entry in $collection.Collection) {
        if ($entry.name -eq $Name) { return $entry.value }
    }

    return $null
}

function Remove-AppPoolEnvironmentVariable {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        return
    }

    $existing = Get-AppPoolEnvironmentVariable -Name $Name
    if ($null -eq $existing) {
        return
    }

    Remove-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" `
        -Filter "system.applicationHost/applicationPools/add[@name='$AppPoolName']/environmentVariables" `
        -Name "." -AtElement @{ name = $Name } -ErrorAction SilentlyContinue
}

function Set-MachineConfiguration {
    <#
        Runtime secrets (DB password, JWT signing key) are persisted only in the ProgramData
        DPAPI-LocalMachine secret store. IIS App Pool environment variables carry non-secret
        coordinates and the secrets-root pointer so the application can find the store.

        Plaintext secrets are intentionally NOT written into applicationHost.config.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][psobject]$State
    )

    $Script:CurrentStep = "ConfigureMachine"
    Set-Phase -State $State -Phase "Configuring"
    Write-Step "Configuring application runtime"

    Initialize-AppPool

    # Issuer/audience are the product's own identity, not the host's URL. Deriving them from an
    # FQDN would mean that configuring a public host name later silently invalidates every issued
    # token, which is a surprising outcome for what is presented as a hosting setting.
    $issuerAudience = "ITAdmin"

    # Preserve existing key material across reinstalls: regenerating the JWT key would invalidate
    # every live session, and regenerating the setup key would orphan an in-flight first-run setup.
    $existingSecrets = Read-MachineSecrets

    $jwtKey = $null
    if ($null -ne $existingSecrets -and -not [string]::IsNullOrWhiteSpace($existingSecrets.jwtKey)) {
        $jwtKey = $existingSecrets.jwtKey
        Write-Detail "Preserved the existing JWT signing key from the machine secret store."
    }
    else {
        $jwtKey = New-RandomSecret
        Write-Detail "Generated a JWT signing key (48 bytes, CSPRNG)."
    }

    $setupKey = $null
    if ($null -ne $existingSecrets -and
        $existingSecrets.PSObject.Properties.Match('setupKey').Count -gt 0 -and
        -not [string]::IsNullOrWhiteSpace($existingSecrets.setupKey)) {
        $setupKey = $existingSecrets.setupKey
        Write-Detail "Preserved the existing first-run setup key."
    }
    else {
        $setupKey = New-RandomSecret
        Write-Detail "Generated a first-run setup key (48 bytes, CSPRNG)."
    }

    $Script:SetupKey = $setupKey

    Save-MachineSecrets -ConnectionString $ConnectionString -JwtKey $jwtKey -SetupKey $setupKey

    # Non-secret app pool environment only. Strip any legacy plaintext secret entries left by
    # older installer layouts so applicationHost.config does not keep carrying them.
    foreach ($legacySecret in @(
            "ITADMIN_ConnectionStrings__DefaultConnection",
            "ITADMIN_Jwt__Key")) {
        Remove-AppPoolEnvironmentVariable -Name $legacySecret
    }

    $variables = @{
        "ASPNETCORE_ENVIRONMENT"                  = "Production"
        "ITADMIN_Secrets__Root"                   = $Script:Layout.SecretsRoot
        "ITADMIN_Jwt__Issuer"                     = $issuerAudience
        "ITADMIN_Jwt__Audience"                   = $issuerAudience
        "ITADMIN_DataProtection__ApplicationName" = "ITAdmin"
        "ITADMIN_DataProtection__KeysPath"        = $Script:Layout.DataProtectionRoot
    }

    Set-AppPoolEnvironmentVariables -Variables $variables

    # The app pool identity must be able to write the DataProtection key ring and logs; losing the
    # key ring would make already-encrypted database values unreadable.
    $identity = "IIS AppPool\$AppPoolName"
    foreach ($writable in @($Script:Layout.DataProtectionRoot, $Script:Layout.LogsRoot)) {
        & icacls $writable /grant "${identity}:(OI)(CI)M" /T | Out-Null
    }
    & icacls $Script:Layout.ConfigRoot /grant "${identity}:(OI)(CI)R" /T | Out-Null

    Write-Ok "Runtime configuration applied (secrets in ProgramData DPAPI store; $($variables.Count) non-secret app pool variables)"
}

function Initialize-AppPool {
    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
        Write-Detail "Created app pool $AppPoolName"
    }

    # No managed runtime: ASP.NET Core runs out-of-process behind the ASP.NET Core Module.
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "managedRuntimeVersion" -Value ""
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "startMode" -Value "AlwaysRunning"
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "processModel.idleTimeout" -Value ([TimeSpan]::Zero)
}

function Set-AppPoolEnvironmentVariables {
    param([Parameter(Mandatory = $true)][hashtable]$Variables)

    $poolPath = "IIS:\AppPools\$AppPoolName"
    foreach ($name in $Variables.Keys) {
        $existing = Get-AppPoolEnvironmentVariable -Name $name
        if ($null -ne $existing) {
            Remove-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" `
                -Filter "system.applicationHost/applicationPools/add[@name='$AppPoolName']/environmentVariables" `
                -Name "." -AtElement @{ name = $name } -ErrorAction SilentlyContinue
        }

        Add-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" `
            -Filter "system.applicationHost/applicationPools/add[@name='$AppPoolName']/environmentVariables" `
            -Name "." -Value @{ name = $name; value = $Variables[$name] }
    }
}

function Invoke-DatabaseMigration {
    <#
        Runs the release's own migration mode. No EF CLI and no psql on this server: the published
        application carries EF Core and the compiled migrations, and uses the same connection
        configuration the site will run with.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$PayloadRoot,
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][psobject]$Artifact,
        [Parameter(Mandatory = $true)][psobject]$State
    )

    $Script:CurrentStep = "Migrate"
    Write-Step "Applying database migrations"

    $executable = Join-Path $PayloadRoot "ITAdmin.Api.exe"
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "Migration host not found in the release payload: $executable"
    }

    # Recorded before the attempt: if this run dies mid-migration, the next run must know the
    # schema may be partially migrated rather than silently retrying.
    $State.migrationInFlight = $true
    Set-CurrentUpdateOperationStage -State $State -Stage "Migrating" `
        -Message "Database migrations are being applied."
    Set-Phase -State $State -Phase "Migrating"

    $previousConnection = $env:ITADMIN_ConnectionStrings__DefaultConnection
    $previousJwt = $env:ITADMIN_Jwt__Key
    $previousSecretsRoot = $env:ITADMIN_Secrets__Root
    try {
        # Prefer the durable machine secret store; process env is only a break-glass override.
        $env:ITADMIN_Secrets__Root = $Script:Layout.SecretsRoot
        # Clear any leftover process-level secret overrides so migration reads the store.
        Remove-Item Env:\ITADMIN_ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
        Remove-Item Env:\ITADMIN_Jwt__Key -ErrorAction SilentlyContinue

        $output = & $executable --migrate 2>&1
        $exitCode = $LASTEXITCODE

        foreach ($line in $output) {
            if ("$line" -match 'currentMigration=(.+)$') { $State.lastMigrationApplied = $Matches[1].Trim() }
            if ("$line" -match '^(Applying|No pending|Migration completed|  \d{14}_)') { Write-Detail "$line" }
        }

        if ($exitCode -ne 0) {
            throw "Database migration failed (exit $exitCode). See the output above."
        }
    }
    finally {
        if ($null -ne $previousConnection) {
            $env:ITADMIN_ConnectionStrings__DefaultConnection = $previousConnection
        }
        else {
            Remove-Item Env:\ITADMIN_ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
        }
        if ($null -ne $previousJwt) {
            $env:ITADMIN_Jwt__Key = $previousJwt
        }
        else {
            Remove-Item Env:\ITADMIN_Jwt__Key -ErrorAction SilentlyContinue
        }
        if ($null -ne $previousSecretsRoot) {
            $env:ITADMIN_Secrets__Root = $previousSecretsRoot
        }
        else {
            Remove-Item Env:\ITADMIN_Secrets__Root -ErrorAction SilentlyContinue
        }
    }

    $State.migrationInFlight = $false
    Save-InstallationState -State $State
    $Script:LastMigrationApplied = $State.lastMigrationApplied
    Write-Ok "Schema at $($State.lastMigrationApplied)"
}

function Get-ExistingWebSiteBindings {
    <#
        Every site currently on this IIS instance, with its bindings, in the shape
        ITAdmin.Deployment.WebBindingOwnership reasons about.

        Read BEFORE any mutation. Deciding what to do about a port-80 conflict requires knowing what
        is actually there, and discovering it afterwards - as an opaque "site failed to start" - is
        exactly the failure mode this replaces.
    #>
    $sites = New-Object System.Collections.Generic.List[object]

    foreach ($site in (Get-Website -ErrorAction SilentlyContinue)) {
        $bindings = New-Object System.Collections.Generic.List[object]

        foreach ($binding in @($site.Bindings.Collection)) {
            # bindingInformation is "address:port:hostheader", e.g. "*:80:" or "*:80:app.example.com".
            $parts = "$($binding.bindingInformation)" -split ':', 3
            if ($parts.Count -lt 2) { continue }

            $port = 0
            if (-not [int]::TryParse($parts[1], [ref]$port)) { continue }

            $bindings.Add([pscustomobject]@{
                Protocol   = "$($binding.protocol)".ToLowerInvariant()
                Port       = $port
                HostHeader = $(if ($parts.Count -ge 3) { $parts[2] } else { "" })
            })
        }

        # An application below the site root means somebody has deployed to this site. That is the
        # difference between "IIS made this" and "an operator adopted this".
        $applicationCount = 0
        try {
            $applicationCount = @(Get-WebApplication -Site $site.Name -ErrorAction SilentlyContinue).Count
        }
        catch {
            Write-Verbose "Could not enumerate applications for site '$($site.Name)': $($_.Exception.Message)"
        }

        $sites.Add([pscustomobject]@{
            Name                      = $site.Name
            State                     = "$($site.State)"
            Bindings                  = $bindings.ToArray()
            HasApplicationsBeyondRoot = ($applicationCount -gt 0)
        })
    }

    return $sites.ToArray()
}

function Test-BindingConflicts {
    <#
        Mirrors ITAdmin.Deployment.WebBindingSpecification.Conflicts (drift-tested).

        IIS keys a binding on protocol + address + port + host header, so two sites can share a port
        when their host headers differ. An empty host header is a wildcard and collides with
        everything on that port - which is precisely why the Default Web Site's "*:80:" is in the way.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Left,
        [Parameter(Mandatory = $true)][psobject]$Right
    )

    if ($Left.Protocol -ne $Right.Protocol) { return $false }
    if ([int]$Left.Port -ne [int]$Right.Port) { return $false }

    $leftHost = "$($Left.HostHeader)".Trim()
    $rightHost = "$($Right.HostHeader)".Trim()

    if ($leftHost.Length -eq 0 -or $rightHost.Length -eq 0) { return $true }

    return $leftHost -eq $rightHost
}

function Resolve-HttpBindingOwnership {
    <#
        Decides whether ITAdmin may take the HTTP binding it wants.

        "Something already owns port 80" covers two completely different situations, and the safe
        action in one is destructive in the other:

          A. THIS installer just turned IIS on. The Default Web Site is a pristine artifact of that
             provisioning - nobody has ever deployed to it - so standing it down is reasonable.
          B. IIS already existed. Every site on it belongs to somebody, INCLUDING one named
             "Default Web Site" that may be quietly serving something important.

        The branch is chosen from recorded provisioning history, never from a site name. A name is a
        guess, and this is not a decision worth guessing at.

        Mirrors ITAdmin.Deployment.WebBindingOwnership.Decide (drift-tested).
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][psobject]$State
    )

    $Script:CurrentStep = "ResolveBindingOwnership"
    Write-Step "Determining HTTP binding ownership"

    $requested = [pscustomobject]@{
        Protocol   = "http"
        Port       = [int]$Config.web.httpPort
        HostHeader = $(if ([string]::IsNullOrWhiteSpace($Config.web.httpHostHeader)) { "" } else { $Config.web.httpHostHeader })
    }

    $requestedText = "http  *:$($requested.Port):$($requested.HostHeader)"
    Write-Detail "Requested: $requestedText"

    $sites = Get-ExistingWebSiteBindings
    $itAdminSiteName = $Config.iis.siteName

    $conflicts = New-Object System.Collections.Generic.List[object]
    foreach ($site in $sites) {
        if ($site.Name -eq $itAdminSiteName) { continue }
        foreach ($binding in $site.Bindings) {
            if (Test-BindingConflicts -Left $binding -Right $requested) {
                $conflicts.Add([pscustomobject]@{ SiteName = $site.Name; Binding = $binding })
            }
        }
    }

    # Case C first, so a rerun never mistakes ITAdmin's own site for an external conflict and never
    # adds a duplicate binding.
    $itAdminSite = @($sites | Where-Object { $_.Name -eq $itAdminSiteName }) | Select-Object -First 1
    $alreadyOwned = $false
    if ($null -ne $itAdminSite) {
        foreach ($binding in $itAdminSite.Bindings) {
            if ($binding.Protocol -eq $requested.Protocol -and
                [int]$binding.Port -eq $requested.Port -and
                "$($binding.HostHeader)".Trim() -eq "$($requested.HostHeader)".Trim()) {
                $alreadyOwned = $true
                break
            }
        }
    }

    if ($alreadyOwned -and $conflicts.Count -eq 0) {
        Write-Ok "ITAdmin already owns $requestedText; reconciliation is a no-op"
        return [pscustomobject]@{ Action = "AlreadyOwned"; Requested = $requested; Conflicts = @() }
    }

    if ($conflicts.Count -eq 0) {
        Write-Ok "$requestedText is unused; ITAdmin will bind it"
        return [pscustomobject]@{ Action = "Claim"; Requested = $requested; Conflicts = @() }
    }

    # Case A: only when we RECORDED provisioning IIS, and only for a Default Web Site still in its
    # as-created state. A Default Web Site that has gained a binding or an application is one an
    # operator has adopted, and taking it down would remove a real workload.
    $iisProvisionedByUs = $false
    if ($State.PSObject.Properties.Match('iisProvisionedByInstaller').Count -gt 0) {
        $iisProvisionedByUs = [bool]$State.iisProvisionedByInstaller
    }

    $allPristineDefault = $true
    foreach ($conflict in $conflicts) {
        $site = @($sites | Where-Object { $_.Name -eq $conflict.SiteName }) | Select-Object -First 1
        $isPristine = ($null -ne $site) -and
                      ($site.Name -eq "Default Web Site") -and
                      ($site.Bindings.Count -eq 1) -and
                      ($site.Bindings[0].Protocol -eq "http") -and
                      ([int]$site.Bindings[0].Port -eq 80) -and
                      ([string]::IsNullOrEmpty("$($site.Bindings[0].HostHeader)")) -and
                      (-not $site.HasApplicationsBeyondRoot)
        if (-not $isPristine) { $allPristineDefault = $false; break }
    }

    if ($iisProvisionedByUs -and $allPristineDefault) {
        Write-Detail "IIS was provisioned by this installation and 'Default Web Site' is still as-created."
        return [pscustomobject]@{
            Action    = "StandDownPristineDefaultSite"
            Requested = $requested
            Conflicts = $conflicts.ToArray()
        }
    }

    # Case B: somebody else's site. Fail preflight with a diagnosis, before any machine change.
    Write-Fail "The HTTP binding ITAdmin requested is already owned by another site on this server."
    Write-Host ""
    Write-Host "  Requested by ITAdmin:" -ForegroundColor Yellow
    Write-Host "    $requestedText"
    Write-Host ""
    Write-Host "  Already bound:" -ForegroundColor Yellow
    foreach ($conflict in $conflicts) {
        Write-Host "    site '$($conflict.SiteName)'  ->  http  *:$($conflict.Binding.Port):$($conflict.Binding.HostHeader)"
    }
    Write-Host ""
    Write-Host "  ITAdmin will not stop, rebind, or remove a site it did not create. Choose one:" -ForegroundColor Yellow
    Write-Host "    1. Free the port deliberately - stop or rebind the site above, then re-run."
    Write-Host "    2. Give ITAdmin a different port explicitly, e.g. -HttpPort 8080."
    Write-Host "    3. Give ITAdmin its own host name on the same port, e.g. -HttpHostHeader itadmin.example.com"
    Write-Host "       (requires DNS pointing that name at this server)."
    if (-not $iisProvisionedByUs) {
        Write-Host ""
        Write-Host "  IIS was already installed before ITAdmin, so every site on it is assumed to be" -ForegroundColor Yellow
        Write-Host "  operator-owned - including one named 'Default Web Site'." -ForegroundColor Yellow
    }
    Write-Host ""

    throw "HTTP binding conflict on port $($requested.Port). No machine changes were made."
}

function Set-IisConfiguration {
    <#
        Establishes the initial HTTP binding and nothing else.

        No certificate is looked up, no HTTPS binding is created, and no host header is required.
        Omitting the host header is deliberate: the site then answers on the machine's short name,
        its FQDN, and its addresses, so an administrator can reach ITAdmin immediately without
        anyone having decided yet what the canonical public name will be.

        Binding ownership was already resolved by Resolve-HttpBindingOwnership, which fails before
        any machine change when the port belongs to somebody else. By the time this runs, the only
        remaining question is whether to stand down a pristine installer-provisioned Default Web Site.

        HTTPS, the public host name, and the redirect are added later through ITAdmin Settings,
        applied by the ITAdmin Host Agent. This function preserves an already-configured HTTPS
        binding on a rerun but never creates one.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][string]$PayloadRoot,
        [Parameter(Mandatory = $true)][psobject]$State,
        [Parameter(Mandatory = $true)][psobject]$Ownership
    )

    $Script:CurrentStep = "ConfigureIis"
    Write-Step "Configuring IIS site and HTTP binding"

    $hostHeader = if ([string]::IsNullOrWhiteSpace($Config.web.httpHostHeader)) { "" } else { $Config.web.httpHostHeader }

    if ($Ownership.Action -eq "StandDownPristineDefaultSite") {
        # Stopped and disabled, NOT deleted. Leaving it present keeps the change trivially
        # reversible by an administrator who later decides they wanted it.
        foreach ($conflict in $Ownership.Conflicts) {
            Write-Detail "Stopping and disabling pristine '$($conflict.SiteName)' to free port $($Ownership.Requested.Port)"
            Stop-Website -Name $conflict.SiteName -ErrorAction SilentlyContinue
            Set-ItemProperty -Path "IIS:\Sites\$($conflict.SiteName)" -Name "serverAutoStart" -Value $false -ErrorAction SilentlyContinue
        }
        Write-Ok "Freed $($Ownership.Requested.Port) from the as-created Default Web Site (stopped, not removed)"
    }

    if (-not (Get-Website -Name $Config.iis.siteName -ErrorAction SilentlyContinue)) {
        New-Website -Name $Config.iis.siteName -PhysicalPath $PayloadRoot `
            -ApplicationPool $Config.iis.appPoolName -Port $Config.web.httpPort `
            -HostHeader $hostHeader -Force | Out-Null
        Write-Detail "Created site $($Config.iis.siteName)"
    }

    Set-ItemProperty -Path "IIS:\Sites\$($Config.iis.siteName)" -Name "applicationPool" -Value $Config.iis.appPoolName

    # Idempotent: New-WebBinding is called only when this exact binding is absent, so a repair run
    # never produces a duplicate.
    $httpBinding = Get-WebBinding -Name $Config.iis.siteName -Protocol "http" `
        -Port $Config.web.httpPort -HostHeader $hostHeader -ErrorAction SilentlyContinue
    if ($null -eq $httpBinding) {
        New-WebBinding -Name $Config.iis.siteName -Protocol "http" -Port $Config.web.httpPort `
            -HostHeader $hostHeader | Out-Null
        Write-Detail "Created HTTP binding on port $($Config.web.httpPort)"
    }
    else {
        Write-Detail "HTTP binding already present; left unchanged."
    }

    if ($Config.web.https.enabled) {
        Write-Detail "An HTTPS binding is configured for this site; it is left untouched by the installer."
    }

    Write-Ok "HTTP binding ready (HTTPS and public host name are configured later from ITAdmin Settings)"
}

function Enable-Release {
    <#
        Activation points the IIS site's physicalPath at the verified release payload and recycles.

        A physicalPath switch is used rather than a junction that gets repointed. On Windows this is
        an applicationHost.config change: it is atomic from IIS's perspective, it survives reboots,
        it is visible to an operator in IIS Manager (which release is live is a plain property), and
        rollback is the same operation against the previous directory. Repointing a junction under a
        running worker process instead relies on directory-handle and file-change-notification
        behaviour that IIS caches, which is exactly the kind of ambiguity an activation step should
        not have.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][psobject]$Artifact,
        [Parameter(Mandatory = $true)][psobject]$State
    )

    $Script:CurrentStep = "Activate"
    Set-Phase -State $State -Phase "Activating"
    Write-Step "Activating release $($Artifact.VersionText)"

    $payloadRoot = Join-Path (Join-Path $Script:Layout.ReleasesRoot $Artifact.VersionText) "app"

    Set-ItemProperty -Path "IIS:\Sites\$($Config.iis.siteName)" -Name "physicalPath" -Value $payloadRoot
    Write-Detail "Site physical path -> $payloadRoot"

    if ((Get-WebAppPoolState -Name $Config.iis.appPoolName).Value -ne "Started") {
        Start-WebAppPool -Name $Config.iis.appPoolName
    }
    else {
        Restart-WebAppPool -Name $Config.iis.appPoolName
    }

    Start-Website -Name $Config.iis.siteName -ErrorAction SilentlyContinue

    Test-Health -Config $Config

    Assert-InstallationIsUsable

    $State.previousVersion = $State.activeVersion
    $State.activeVersion = $Artifact.VersionText
    $State.stagedVersion = $null
    $State.lastError = $null
    $State | Add-Member -NotePropertyName "readiness" -NotePropertyValue $Script:Readiness -Force
    Set-Phase -State $State -Phase "Installed"
}

function Test-Health {
    <#
        Fail-closed, and deliberately stricter than "the process answered".

        First-install success is FOUR independent things, all of which must hold:
          1. process/IIS health   - the site answers /health
          2. application setup    - the app reports first-run setup as COMPLETE
          3. Primary Directory    - a directory was configured and its bind validated
          4. initial administrator - a directory-backed administrator exists

        A worker process returning HTTP 200 satisfies only (1). An installation where the site
        serves but nobody can log in is not installed; recording it as Installed would be a lie the
        operator discovers at the login screen, after the installer has already reported success and
        exited. So (2) is checked here against the application's own setup status, and (3)/(4) are
        established earlier by the directory bootstrap and carried in $Script:Readiness.
    #>
    param([Parameter(Mandatory = $true)][psobject]$Config)

    $Script:CurrentStep = "HealthCheck"
    Write-Detail "Health check"

    # Always checked over HTTP against a locally-resolvable name. Health-checking the public HTTPS
    # name would make the installer depend on external DNS and a trusted certificate chain from the
    # server itself - two things that have nothing to do with whether the application started.
    $hostName = if ([string]::IsNullOrWhiteSpace($Config.web.httpHostHeader)) {
        $env:COMPUTERNAME
    }
    else {
        $Config.web.httpHostHeader
    }
    $baseUrl = "http://${hostName}:$($Config.web.httpPort)"

    $attempts = 10
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -eq 200) {
                Write-Ok "Health endpoint returned 200"
                $Script:Readiness.processHealthy = $true

                # The application's own answer to "can anyone actually use this yet".
                $setup = Invoke-WebRequest -Uri "$baseUrl/api/setup/status" -UseBasicParsing -TimeoutSec 15
                if ($setup.StatusCode -ne 200) {
                    throw "Setup status endpoint returned $($setup.StatusCode)."
                }

                $setupStatus = $setup.Content | ConvertFrom-Json
                if ($setupStatus.isSetupRequired) {
                    Show-FailureDiagnostics
                    throw "The application is serving but still reports first-run setup as INCOMPLETE, " +
                          "so no one can log in. The directory bootstrap did not take effect. This " +
                          "machine is NOT marked as installed."
                }

                $Script:Readiness.setupCompleted = $true
                Write-Ok "Application reports first-run setup complete"
                return
            }
        }
        catch {
            if ($attempt -eq $attempts) {
                Write-Fail "Last health check error: $($_.Exception.Message)"
                Show-FailureDiagnostics
                throw "Health check failed against $baseUrl/health after $attempts attempts. " +
                      "The release is staged and IIS is pointed at it, but it is not serving. " +
                      "This machine is NOT marked as installed."
            }
        }

        Start-Sleep -Seconds 3
    }
}

function Assert-InstallationIsUsable {
    <#
        The last gate before this machine is recorded as Installed.

        Every earlier step reports its own failure, but they run in sequence and a future change
        could reorder or skip one. This asserts the end state directly: unless all four readiness
        conditions were proven, the run fails rather than writing a phase that claims more than
        happened. "Installed" must mean "an administrator can log in".
    #>
    $missing = New-Object System.Collections.Generic.List[string]

    if (-not $Script:Readiness.processHealthy) {
        $missing.Add("the site is not answering its health endpoint")
    }
    if (-not $Script:Readiness.directoryUsable) {
        $missing.Add("no Primary Directory has been validated")
    }
    if (-not $Script:Readiness.administratorBootstrapped) {
        $missing.Add("no directory-backed administrator has been created")
    }
    if (-not $Script:Readiness.setupCompleted) {
        $missing.Add("the application still reports first-run setup as incomplete")
    }

    if ($missing.Count -gt 0) {
        foreach ($problem in $missing) { Write-Fail $problem }
        throw "The installation cannot be recorded as successful because " +
              ($missing -join "; ") + ". ITAdmin authenticates through LDAP, so a serving process " +
              "that nobody can log into is not an installed product."
    }

    Write-Ok "Readiness confirmed: serving, setup complete, directory usable, administrator bootstrapped"
}

function Show-FailureDiagnostics {
    $logDirectory = Join-Path $Script:Layout.LogsRoot "*"
    $recent = Get-ChildItem -Path $logDirectory -Filter "*.log" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -ne $recent) {
        Write-Host "    --- last 20 log lines ($($recent.Name)) ---" -ForegroundColor Yellow
        Get-Content -LiteralPath $recent.FullName -Tail 20 | ForEach-Object { Write-Host "    $_" }
    }
}

# --------------------------------------------------------------------------------------------
# Primary Directory and the initial administrator
#
# ITAdmin authenticates every user through LDAP. An installation with no directory configuration
# and no directory-backed administrator cannot be logged into, so this is an installation step,
# not post-install configuration.
#
# Nothing here reproduces the application's authorization rules. Role seeding, permission grants,
# the portal-user representation of a directory identity, and the "setup is complete" marker all
# live in the application's own setup service - the same one the web setup wizard drives. This
# script gathers input, proves the directory answers, and calls it.
# --------------------------------------------------------------------------------------------

function Get-JoinedDomainInfo {
    <#
        AD identity of this computer. Discovery only, never a baked-in default: an operator should
        not have to type values Windows already knows.
    #>
    try {
        $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop
        if ($computerSystem.PartOfDomain -and -not [string]::IsNullOrWhiteSpace($computerSystem.Domain)) {
            $domain = $computerSystem.Domain
            $baseDn = (($domain -split '\.' | Where-Object { $_ } | ForEach-Object { "DC=$_" }) -join ',')
            return [pscustomobject]@{ Domain = $domain; BaseDn = $baseDn }
        }
    }
    catch {
        Write-Verbose "Domain discovery failed: $($_.Exception.Message)"
    }

    return $null
}

function Resolve-DirectoryConfiguration {
    <#
        The minimum needed to authenticate: where the directory is, what to search, and an account
        allowed to read it.

        Host defaults to the AD DNS domain rather than a specific controller, so DC locator handles
        failover instead of the installation pinning one server that may later be decommissioned.
        Base DN is derived from the same domain. Both remain overridable for forests where
        discovery is not the right answer.
    #>
    Write-Step "Resolving Primary Directory configuration"

    $discovered = Get-JoinedDomainInfo

    $directoryHost = $DirectoryHost
    if ([string]::IsNullOrWhiteSpace($directoryHost)) {
        if ($null -ne $discovered) {
            $directoryHost = $discovered.Domain
            Write-Detail "Discovered directory host from domain membership: $directoryHost"
        }
        else {
            $directoryHost = Resolve-RequiredValue -Supplied $null -Existing $null `
                -Prompt "Directory host (AD domain or domain controller)" -Name "DirectoryHost"
        }
    }

    $baseDn = $DirectoryBaseDn
    if ([string]::IsNullOrWhiteSpace($baseDn)) {
        if ($null -ne $discovered) {
            $baseDn = $discovered.BaseDn
            Write-Detail "Derived Base DN: $baseDn"
        }
        else {
            $baseDn = Resolve-RequiredValue -Supplied $null -Existing $null `
                -Prompt "Directory Base DN" -Name "DirectoryBaseDn"
        }
    }

    $bindUser = Resolve-RequiredValue -Supplied $DirectoryBindUser -Existing $null `
        -Prompt "Directory bind account (service account that can read the directory)" -Name "DirectoryBindUser"

    $bindPassword = Read-RequiredSecret -Supplied $DirectoryBindPassword `
        -Prompt "Password for '$bindUser'" -Name "DirectoryBindPassword"

    $administrator = Resolve-RequiredValue -Supplied $InitialAdministrator -Existing $null `
        -Prompt "Initial ITAdmin administrator (directory user: UPN, sAMAccountName, or email)" `
        -Name "InitialAdministrator"

    $directoryName = if (-not [string]::IsNullOrWhiteSpace($DirectoryName)) { $DirectoryName } else { $directoryHost }

    Write-Ok "Directory: $directoryHost (Base DN $baseDn), bind account '$bindUser'"

    return [pscustomobject]@{
        Name            = $directoryName
        Host            = $directoryHost
        BaseDn          = $baseDn
        SearchFilter    = $DirectoryUserSearchFilter
        BindUser        = $bindUser
        BindDomain      = $DirectoryBindDomain
        BindPassword    = $bindPassword
        Administrator   = $administrator
    }
}

function Invoke-DirectoryBootstrap {
    <#
        Drives the application's own setup service to validate the bind, resolve the administrator
        in the directory, and grant them the canonical administrator role.

        Input goes through a file, not the command line: a process command line is readable by every
        user on the machine, and this input carries the bind password and the setup key. The file is
        written with an ACL restricted to SYSTEM and Administrators, and is deleted in a finally
        block whether the step succeeds or fails.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$PayloadRoot,
        [Parameter(Mandatory = $true)][psobject]$Directory,
        [Parameter(Mandatory = $true)][psobject]$State
    )

    $Script:CurrentStep = "BootstrapDirectory"
    Write-Step "Establishing Primary Directory and the initial administrator"

    $executable = Join-Path $PayloadRoot "ITAdmin.Api.exe"
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "Directory bootstrap host not found in the release payload: $executable"
    }

    if ([string]::IsNullOrWhiteSpace($Script:SetupKey)) {
        throw "The first-run setup key was not established; machine configuration must run first."
    }

    $inputPath = Join-Path $Script:Layout.StateRoot ("directory-bootstrap-{0}.json" -f [guid]::NewGuid().ToString("N"))

    try {
        $payload = [pscustomobject]@{
            setupKey                = $Script:SetupKey
            directoryName           = $Directory.Name
            host                    = $Directory.Host
            baseDn                  = $Directory.BaseDn
            userSearchFilter        = $Directory.SearchFilter
            bindUserName            = $Directory.BindUser
            bindUserDomain          = $Directory.BindDomain
            bindPassword            = (ConvertFrom-SecureStringToPlainText -Secure $Directory.BindPassword)
            administratorIdentifier = $Directory.Administrator
        }

        $payload | ConvertTo-Json -Compress | Set-Content -LiteralPath $inputPath -Encoding UTF8
        & icacls $inputPath /inheritance:r | Out-Null
        & icacls $inputPath /grant:r "SYSTEM:F" "Administrators:F" | Out-Null

        $previousSecretsRoot = $env:ITADMIN_Secrets__Root
        try {
            $env:ITADMIN_Secrets__Root = $Script:Layout.SecretsRoot
            $output = & $executable --bootstrap-directory --input $inputPath 2>&1
            $exitCode = $LASTEXITCODE
        }
        finally {
            if ($null -ne $previousSecretsRoot) {
                $env:ITADMIN_Secrets__Root = $previousSecretsRoot
            }
            else {
                Remove-Item Env:\ITADMIN_Secrets__Root -ErrorAction SilentlyContinue
            }
        }

        # Exit 3 means the directory itself said no - a wrong credential, an unreachable controller,
        # an administrator who could not be resolved. Those are input problems the operator can fix
        # right now, so the diagnosis is surfaced verbatim rather than buried in a generic failure.
        if ($exitCode -eq 3) {
            foreach ($line in $output) { Write-Fail "$line" }
            throw "The directory rejected the supplied configuration. Correct the values and re-run; " +
                  "no administrator was created."
        }

        if ($exitCode -ne 0) {
            foreach ($line in $output) { Write-Fail "$line" }
            throw "Directory bootstrap failed (exit $exitCode)."
        }

        $resultLine = @($output | Where-Object { "$_" -match '^\s*\{' } | Select-Object -Last 1)
        $result = $null
        if ($resultLine.Count -gt 0) {
            $result = "$($resultLine[0])" | ConvertFrom-Json
        }

        if ($null -ne $result -and $result.status -eq "AlreadyBootstrapped") {
            # An idempotent rerun. The directory and administrator exist from an earlier run, which
            # is exactly as much proof of usability as having created them now.
            $Script:Readiness.directoryUsable = $true
            $Script:Readiness.administratorBootstrapped = $true
            Write-Ok "Directory configuration and the initial administrator already exist; nothing was changed."
            return $null
        }

        $Script:Readiness.directoryUsable = $true
        $Script:Readiness.administratorBootstrapped = $true

        $administratorName = if ($null -ne $result) { $result.administratorUserName } else { $Directory.Administrator }
        Write-Ok "Initial administrator '$administratorName' resolved from the directory and granted access"
        return $administratorName
    }
    finally {
        if (Test-Path -LiteralPath $inputPath) {
            Remove-Item -LiteralPath $inputPath -Force -ErrorAction SilentlyContinue
        }
    }
}

# --------------------------------------------------------------------------------------------
# Host Agent
# --------------------------------------------------------------------------------------------

function Install-HostAgent {
    <#
        Installs the privileged half of ITAdmin as a Windows service.

        It is registered from Program Files, not from the release payload, and runs as LocalSystem.
        The application pool identity is deliberately given no rights over its directory: the whole
        point of a separate service is that a flaw in request handling cannot reach the operations
        that update releases and rewrite IIS configuration.

        Every production release carries this component as part of the closed manifest set.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][psobject]$Artifact
    )

    $Script:CurrentStep = "InstallHostAgent"
    Write-Step "Installing the ITAdmin Host Agent"

    $agentRoot = Join-Path $Script:Layout.ProgramFilesRoot `
        "hostagent\releases\$($Artifact.VersionText)"
    $agentExecutable = Join-Path $agentRoot "ITAdmin.HostAgent.exe"
    $agentSource = Join-Path $Artifact.SourceRoot "hostagent"

    # Versioned service directories let the coordinator switch ImagePath only after the target
    # release is healthy; a running service never overwrites its own binaries.
    if (Test-Path -LiteralPath $agentSource -PathType Container) {
        # Already verified as a declared component during distribution verification. A hostagent
        # directory with no matching component would have failed the closed-set check.
        if ($null -eq $Artifact.Manifest.components.PSObject.Properties["hostagent"]) {
            throw "The distribution carries a hostagent directory that the manifest does not declare. " +
                  "Refusing to install unverified privileged binaries."
        }

        # Stop before replacing binaries; a running service holds its own files open.
        $running = Get-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
        if ($null -ne $running -and $running.Status -eq "Running") {
            Stop-Service -Name "ITAdminHostAgent" -Force -ErrorAction SilentlyContinue
        }

        if (-not (Test-Path -LiteralPath $agentRoot)) {
            New-Item -ItemType Directory -Path $agentRoot -Force | Out-Null
        }

        Copy-Item -Path (Join-Path $agentSource "*") -Destination $agentRoot -Recurse -Force
        Write-Detail "Host Agent binaries installed to $agentRoot"
    }

    if (-not (Test-Path -LiteralPath $agentExecutable)) {
        throw "This production release does not contain the required Host Agent executable."
    }

    # Program Files is administrator-writable only by default; the explicit deny keeps a future
    # inherited grant from quietly handing the app pool access to the privileged binaries.
    $identity = "IIS AppPool\$AppPoolName"
    & icacls $agentRoot /deny "${identity}:(OI)(CI)(F)" | Out-Null

    $existing = Get-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
    if ($null -eq $existing) {
        & sc.exe create "ITAdminHostAgent" binPath= "`"$agentExecutable`"" start= auto `
            DisplayName= "ITAdmin Host Agent" obj= "LocalSystem" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not register the ITAdmin Host Agent service (sc.exe exit $LASTEXITCODE)."
        }
        & sc.exe description "ITAdminHostAgent" `
            "Performs privileged ITAdmin host operations (release updates and IIS binding reconciliation) over a local ACL'd named pipe." | Out-Null
        Write-Detail "Registered service ITAdminHostAgent"
    }
    else {
        & sc.exe config "ITAdminHostAgent" binPath= "`"$agentExecutable`"" start= auto | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not update the ITAdmin Host Agent service image path (sc.exe exit $LASTEXITCODE)."
        }
        Write-Detail "Service ITAdminHostAgent updated to $($Artifact.VersionText)."
    }

    Start-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
    $service = Get-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue

    if ($null -ne $service -and $service.Status -eq "Running") {
        Write-Ok "ITAdmin Host Agent is running"
        return $true
    }

    Write-Host "    WARN The ITAdmin Host Agent is registered but not running." -ForegroundColor Yellow
    return $false
}

function Write-InstallationSummary {
    <#
        What an operator needs, and nothing they must not have. Locations and protection models are
        printed; secret values never are. There is no operational reason for a person to know the
        JWT signing key or the setup key, so printing them would only create a copy of them in a
        console buffer, a transcript, and probably a ticket.
    #>
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][psobject]$Artifact,
        [psobject]$Directory,
        [string]$AdministratorName,
        [bool]$HostAgentRunning
    )

    $urls = Get-LocalAccessUrls -Config $Config

    Write-Host ""
    Write-Host "ITAdmin installation completed successfully." -ForegroundColor Green
    Write-Host ""
    Write-Host "  Version           $($Artifact.VersionText)"
    Write-Host "  Source commit     $($Artifact.Manifest.source.commit)"
    Write-Host ""
    Write-Host "  Web (HTTP only)"
    foreach ($url in $urls) {
        Write-Host "    $url"
    }
    Write-Host ""
    Write-Host "  Database          $($Config.database.host):$($Config.database.port)/$($Config.database.name)"
    Write-Host "  Schema            $(if ($null -ne $Script:LastMigrationApplied) { $Script:LastMigrationApplied } else { 'current' })"
    Write-Host ""
    Write-Host "  Primary Directory"
    if ($null -ne $Directory) {
        Write-Host "    Host            $($Directory.Host)"
        Write-Host "    Base DN         $($Directory.BaseDn)"
        Write-Host "    Bind            verified"
        Write-Host "    Administrator   $(if ($AdministratorName) { $AdministratorName } else { '(existing)' })  (LDAP-backed)"
    }
    else {
        Write-Host "    Existing configuration and administrator re-validated"
    }
    Write-Host ""
    Write-Host "  IIS"
    Write-Host "    Site            $($Config.iis.siteName)"
    Write-Host "    App pool        $($Config.iis.appPoolName)"
    Write-Host "    HTTP binding    port $($Config.web.httpPort)$(if ($Config.web.httpHostHeader) { ", host header $($Config.web.httpHostHeader)" })"
    Write-Host "    Health          Healthy"
    Write-Host ""
    Write-Host "  Host Agent        $(if ($HostAgentRunning) { 'Running' } else { 'Not running' })"
    Write-Host ""
    Write-Host "  Configuration     $Script:EnvironmentConfigPath"
    Write-Host "  Secrets           $($Script:Layout.SecretsRoot)"
    Write-Host "                    Windows DPAPI (LocalMachine), ACL: SYSTEM + Administrators full, app pool read"
    Write-Host "  Data Protection   $($Script:Layout.DataProtectionRoot)"
    Write-Host "  State             $Script:StatePath"
    Write-Host "  Logs              $($Script:Layout.LogsRoot)"
    Write-Host "  Release           $($Script:Layout.ReleasesRoot)\$($Artifact.VersionText)"
    Write-Host ""
    Write-Host "  Next"
    Write-Host "    Open $($urls[0]) and sign in with the initial administrator's directory credentials."
    Write-Host "    HTTPS, certificates, the public host name, and the HTTP-to-HTTPS redirect are"
    Write-Host "    configured later from ITAdmin Settings."
    Write-Host ""

    # Server Core has no interactive shell/browser. On Desktop Experience this is a convenience
    # only; a browser failure must never turn a successful installation into a failed one.
    if (-not $Unattended.IsPresent -and [Environment]::UserInteractive) {
        try { Start-Process $urls[0] -ErrorAction SilentlyContinue } catch { }
    }
}

function Install-UpdateCoordinator {
    param([Parameter(Mandatory = $true)][psobject]$Artifact)

    $Script:CurrentStep = "InstallUpdateCoordinator"
    Write-Step "Installing the ITAdmin Update Coordinator"

    $source = Join-Path $Artifact.SourceRoot "update-coordinator"
    $target = Join-Path $Script:Layout.ProgramFilesRoot `
        "update-coordinator\releases\$($Artifact.VersionText)"
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "This production release does not contain the required Update Coordinator component."
    }

    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force
    $executable = Join-Path $target "ITAdmin.UpdateCoordinator.exe"
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "The Update Coordinator executable is missing from the verified component."
    }

    $identity = "IIS AppPool\$AppPoolName"
    & icacls $target /deny "${identity}:(OI)(CI)(F)" | Out-Null
    $existing = Get-Service -Name "ITAdminUpdateCoordinator" -ErrorAction SilentlyContinue
    if ($null -eq $existing) {
        & sc.exe create "ITAdminUpdateCoordinator" binPath= "`"$executable`"" start= demand `
            DisplayName= "ITAdmin Update Coordinator" obj= "LocalSystem" | Out-Null
    }
    else {
        & sc.exe config "ITAdminUpdateCoordinator" binPath= "`"$executable`"" start= demand | Out-Null
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Could not register the ITAdmin Update Coordinator service (sc.exe exit $LASTEXITCODE)."
    }
    Write-Ok "ITAdmin Update Coordinator is registered as a demand-start LocalSystem service"
}

# --------------------------------------------------------------------------------------------
# Main
# --------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "ITAdmin installer" -ForegroundColor White
Write-Host "=================" -ForegroundColor White

$state = Get-InstallationState
$Script:CurrentStep = "Preflight"

try {
    Test-Preflight -State $state -AllowProvision:$ProvisionPrerequisites.IsPresent
}
catch {
    Write-Host ""
    Write-Fail $_.Exception.Message
    try {
        # Persist Failed for in-flight prerequisite work, including a prior Failed phase that is
        # being retried (recovery from Failed/ProvisionHostingBundle without deleting state).
        if ($state.phase -in @("ProvisioningPrerequisites", "Failed")) {
            Set-FailedPhase -State $state -Step $Script:CurrentStep -Message $_.Exception.Message
        }
    }
    catch {
        Write-Fail "Additionally, installation state could not be written: $($_.Exception.Message)"
    }
    if ($state.phase -eq "AwaitingReboot" -or $state.phase -eq "Failed" -or $state.phase -eq "ProvisioningPrerequisites") {
        Write-Host "State: $Script:StatePath" -ForegroundColor Yellow
    }
    exit 1
}

if ($PrerequisitesOnly.IsPresent) {
    Write-Step "Prerequisites-only run"
    # Provisioning must not look like an application install. Clear Failed/ProvisioningPrerequisites
    # back to NotInstalled when nothing was ever activated so the next full install is a clean FreshInstall.
    if ([string]::IsNullOrWhiteSpace($state.activeVersion)) {
        $state.phase = "NotInstalled"
        $state.lastError = $null
        Save-InstallationState -State $state
    }
    Write-Ok "Required prerequisites are installed and re-confirmed. No application install was performed."
    Write-Detail "Re-run the bootstrap (or this script with -ReleaseDirectory) to continue the fresh install."
    exit 0
}

$artifact = $null
try {
    if (-not [string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
        # Canonical path: the bootstrap (or the Host Agent) already fetched this release from the
        # repository's distribution ref, and tells us which annotated tag it is meant to be.
        $artifact = Read-ValidatedReleaseDirectory -Path $ReleaseDirectory `
            -RequiredVersion $ExpectedVersion -RequiredSourceCommit $ExpectedSourceCommit
    }
    else {
        $artifact = Expand-AndValidateArtifact -Path $ArtifactPath
    }

    $state = Get-InstallationState
    $intent = Get-InstallationIntent -State $state -Candidate $artifact.Version

    Write-Step "Installation intent"
    Write-Detail "Current phase:  $($state.phase)"
    Write-Detail "Active version: $(if ($state.activeVersion) { $state.activeVersion } else { '(none)' })"
    Write-Detail "Release:        $($artifact.VersionText)"
    Write-Detail "Intent:         $intent"

    switch ($intent) {
        "RecoverInterruptedMigration" {
            throw "A previous run was interrupted during database migration, so the schema may be " +
                  "partially migrated. Review the database state, then clear 'migrationInFlight' in " +
                  "$Script:StatePath once you have confirmed it, and re-run."
        }
        "ResumeAfterReboot" {
            Write-Detail "Resuming after a prerequisite reboot; prerequisites already re-confirmed above."
        }
        "Downgrade" {
            if (-not $AllowDowngrade.IsPresent) {
                throw "Release $($artifact.VersionText) is older than the installed $($state.activeVersion). " +
                      "Database migrations are not reversed by this installer. Re-run with -AllowDowngrade " +
                      "only if you are certain the older release supports the current schema."
            }
            Write-Host "    WARN Proceeding with an explicit downgrade." -ForegroundColor Yellow
        }
    }

    $environmentConfig = Resolve-EnvironmentConfig
    $isUnattendedExistingInstall = $Unattended.IsPresent -and $intent -in @("Upgrade", "SameVersionRepair")
    $directory = $null
    if ($isUnattendedExistingInstall) {
        if ($null -eq $state.readiness -or
            -not $state.readiness.directoryUsable -or
            -not $state.readiness.administratorBootstrapped) {
            throw "The existing installation does not prove directory and administrator readiness. " +
                  "An unattended update cannot repair first-run setup; run an interactive repair."
        }
        $Script:Readiness.directoryUsable = $true
        $Script:Readiness.administratorBootstrapped = $true
        Write-Detail "Existing directory and administrator readiness will be preserved for this update."
    }
    else {
        $directory = Resolve-DirectoryConfiguration
    }

    Import-Module WebAdministration -ErrorAction Stop

    # Resolved during preflight, before ANY machine change. Discovering a port-80 conflict after the
    # site has been created surfaces as an opaque "site failed to start"; discovering it here costs
    # the operator one flag and a re-run.
    $bindingOwnership = Resolve-HttpBindingOwnership -Config $environmentConfig -State $state

    if ($WhatIfPreflightOnly.IsPresent) {
        Write-Step "Preflight-only run"
        Write-Ok "Preflight, release validation, environment/directory configuration, and HTTP binding"
        Write-Detail "ownership all succeeded. No machine changes were made."
        Write-Detail "Re-run without -WhatIfPreflightOnly to install."
        exit 0
    }

    New-MachineDirectories
    $releasePaths = Publish-StagedRelease -Artifact $artifact -State $state
    Save-EnvironmentConfig -Config $environmentConfig

    $connectionString = Resolve-DatabaseConnectionString -Config $environmentConfig -State $state
    Set-MachineConfiguration -Config $environmentConfig -ConnectionString $connectionString -State $state
    Invoke-DatabaseMigration -PayloadRoot $releasePaths.PayloadRoot -ConnectionString $connectionString `
        -Artifact $artifact -State $state
    # The directory is established BEFORE activation. An ITAdmin that is serving but has no
    # directory configuration and no administrator is not a usable installation, and reporting
    # success at that point would be a lie the operator only discovers at the login screen.
    $administratorName = $null
    if (-not $isUnattendedExistingInstall) {
        $administratorName = Invoke-DirectoryBootstrap -PayloadRoot $releasePaths.PayloadRoot `
            -Directory $directory -State $state
    }

    Set-CurrentUpdateOperationStage -State $state -Stage "Activating" `
        -Message "The verified release is being activated and health checked."
    Set-IisConfiguration -Config $environmentConfig -PayloadRoot $releasePaths.PayloadRoot -State $state `
        -Ownership $bindingOwnership
    Enable-Release -Config $environmentConfig -Artifact $artifact -State $state

    # A service-initiated update must not stop and overwrite the process that is coordinating it.
    # The target Host Agent remains verified in the release and is switched by the update
    # coordinator; interactive installs/repairs install it directly.
    $hostAgentRunning = if ($isUnattendedExistingInstall) {
        $service = Get-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
        $null -ne $service -and $service.Status -eq "Running"
    }
    else {
        $running = Install-HostAgent -Config $environmentConfig -Artifact $artifact
        Install-UpdateCoordinator -Artifact $artifact
        $running
    }

    Write-InstallationSummary -Config $environmentConfig -Artifact $artifact -Directory $directory `
        -AdministratorName $administratorName -HostAgentRunning $hostAgentRunning
    exit 0
}
catch {
    $message = $_.Exception.Message
    Write-Host ""
    Write-Fail $message
    try {
        if ($state.phase -ne "AwaitingReboot") {
            Set-FailedPhase -State $state -Step $Script:CurrentStep -Message $message
            Write-Host ""
            Write-Host "The machine is recorded as FAILED at step '$Script:CurrentStep'." -ForegroundColor Yellow
            Write-Host "It is not marked installed. Fix the cause and re-run this installer." -ForegroundColor Yellow
        }
        Write-Host "State: $Script:StatePath" -ForegroundColor Yellow
    }
    catch {
        Write-Fail "Additionally, installation state could not be written: $($_.Exception.Message)"
    }
    exit 1
}
finally {
    # Only a temporary extraction from the offline artifact mode is ours to remove. A release
    # directory handed to us by the bootstrap or the Host Agent belongs to the caller.
    if ($null -ne $artifact -and
        -not [string]::IsNullOrWhiteSpace($artifact.ExtractRoot) -and
        (Test-Path -LiteralPath $artifact.ExtractRoot)) {
        Remove-Item -LiteralPath $artifact.ExtractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
