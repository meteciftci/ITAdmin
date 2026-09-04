#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Builds ITAdmin from a source clone and deploys it to IIS. First install and update in one script.

.DESCRIPTION
    ITAdmin is deployed straight from its public Git repository. There are no release tags, no
    prebuilt packages, and no distribution refs: this script clones (or fast-forwards) the repo,
    builds the backend and the frontend on the server, provisions/migrates the database, and points
    IIS at the fresh build.

    Run it once for a first install; run it again - or press Install in Settings -> Updates, which
    invokes this same script - to pick up whatever is currently on the branch.

    Server prerequisites (checked in preflight, never installed by this script except IIS features):
      - Windows Server 2022 / 2025, elevated PowerShell
      - Git
      - .NET SDK 10.x           (dotnet publish)
      - Node.js 20+ and npm     (frontend build)
      - IIS + ASP.NET Core 10 Hosting Bundle (ANCM, out-of-process hosting)

    Layout:
      <InstallRoot>\src\                 the clone (branch tip only; reset --hard on every run)
      <InstallRoot>\app\<sha>\           dotnet publish output + wwwroot   <- IIS physicalPath
      <InstallRoot>\hostagent\<sha>\     published Host Agent              <- service ImagePath
      <InstallRoot>\update-coordinator\<sha>\
      %ProgramData%\ITAdmin\config\      app.json, hostagent.json (non-secret)
      %ProgramData%\ITAdmin\secrets\     runtime.secrets.dpapi (DPAPI LocalMachine)
      %ProgramData%\ITAdmin\state\       deploy.json
    The newest three builds are kept under app\ and hostagent\ for rollback.

.PARAMETER Unattended
    Never prompt. Every first-run value must already be in app.json or supplied as a parameter.
    Used by the Host Agent when it applies an update.

.PARAMETER SkipBuild
    Reuse the currently active build. For a configuration-only change (e.g. -HttpHostHeader).

.PARAMETER ConfigureHttps
    Add or replace the HTTPS binding (certificate from Cert:\LocalMachine\My) and optionally the
    HTTP-to-HTTPS redirect, then exit. No source sync, no build.

.PARAMETER Rollback
    Point IIS back at the previously active build and health-check it, then exit. Database
    migrations are forward-only and are NOT reversed.

.PARAMETER ProvisionDatabase
    Force the database provisioning step (CREATE ROLE / CREATE DATABASE / grants) even when a
    connection is already configured. Requires -DatabaseAdminUser / -DatabaseAdminPassword.

.PARAMETER InstallIisFeatures
    Install the required IIS Windows features with Install-WindowsFeature if any are missing.

.EXAMPLE
    git clone https://github.com/meteciftci/ITAdmin.git C:\ITAdmin\src
    C:\ITAdmin\src\scripts\deploy\Deploy-ITAdmin.ps1 -InstallIisFeatures

.EXAMPLE
    # Update to the current branch tip.
    C:\ITAdmin\src\scripts\deploy\Deploy-ITAdmin.ps1
#>
[CmdletBinding()]
param(
    [string]$RepositoryUrl = "https://github.com/meteciftci/ITAdmin.git",
    [string]$Branch = "main",

    [string]$InstallRoot = "C:\ITAdmin",
    [string]$DataRoot = "$env:ProgramData\ITAdmin",

    [string]$SiteName = "ITAdmin",
    [string]$AppPoolName = "ITAdmin",
    [int]$HttpPort = 80,
    [string]$HttpHostHeader,

    [string]$DatabaseHost,
    [int]$DatabasePort = 5432,
    [string]$DatabaseName,
    [string]$DatabaseUser,
    [SecureString]$DatabasePassword,
    [string]$DatabaseAdminUser,
    [SecureString]$DatabaseAdminPassword,
    [string]$DatabaseAdminDatabase = "postgres",

    [string]$DirectoryName,
    [string]$DirectoryHost,
    [string]$DirectoryBaseDn,
    [string]$DirectoryUserSearchFilter = "(sAMAccountName={0})",
    [string]$DirectoryBindUser,
    [string]$DirectoryBindDomain,
    [SecureString]$DirectoryBindPassword,
    [string]$InitialAdministrator,

    [string]$CertificateThumbprint,
    [int]$HttpsPort = 443,
    [switch]$RedirectHttpToHttps,

    [switch]$ProvisionDatabase,
    [switch]$InstallIisFeatures,
    [switch]$SkipBuild,
    [switch]$ConfigureHttps,
    [switch]$Rollback,
    [switch]$WhatIfPreflightOnly,
    [switch]$Unattended,

    # Set by the Update Coordinator: it owns the Host Agent service swap, because the running
    # Host Agent process cannot stop and repoint itself.
    [switch]$NoHostAgentService
)

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
function Write-Detail { param([string]$Message) Write-Host "    $Message" }
function Write-Ok     { param([string]$Message) Write-Host "    OK  $Message" -ForegroundColor Green }
function Write-Fail   { param([string]$Message) Write-Host "    !!  $Message" -ForegroundColor Red }

# --------------------------------------------------------------------------------------------
# Layout
# --------------------------------------------------------------------------------------------

$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
$DataRoot = [System.IO.Path]::GetFullPath($DataRoot)

$Script:Layout = [pscustomobject]@{
    InstallRoot        = $InstallRoot
    SrcRoot            = Join-Path $InstallRoot "src"
    AppRoot            = Join-Path $InstallRoot "app"
    HostAgentRoot      = Join-Path $InstallRoot "hostagent"
    CoordinatorRoot    = Join-Path $InstallRoot "update-coordinator"
    DataRoot           = $DataRoot
    ConfigRoot         = Join-Path $DataRoot "config"
    SecretsRoot        = Join-Path $DataRoot "secrets"
    StateRoot          = Join-Path $DataRoot "state"
    DataProtectionRoot = Join-Path $DataRoot "DataProtection-Keys"
    LogsRoot           = Join-Path $DataRoot "logs"
}
$Script:AppConfigPath = Join-Path $Script:Layout.ConfigRoot "app.json"
$Script:HostAgentConfigPath = Join-Path $Script:Layout.ConfigRoot "hostagent.json"
$Script:StatePath = Join-Path $Script:Layout.StateRoot "deploy.json"
$Script:KeepBuilds = 3

# Established during runtime configuration, consumed by the directory bootstrap step. Held in a
# script variable so it is never written to a file, a log, or the summary.
$Script:SetupKey = $null
$Script:DatabaseAppPassword = $null
$Script:DatabaseAppPasswordGenerated = $false
$Script:LastMigrationApplied = $null

# --------------------------------------------------------------------------------------------
# State
# --------------------------------------------------------------------------------------------

function Get-DeployState {
    if (-not (Test-Path -LiteralPath $Script:StatePath)) {
        return [pscustomobject]@{
            schemaVersion    = 1
            product          = "ITAdmin"
            repositoryUrl    = $RepositoryUrl
            branch           = $Branch
            activeSha        = $null
            previousSha      = $null
            activeBuiltAtUtc = $null
            lastMigration    = $null
            updatedAtUtc     = (Get-Date).ToUniversalTime().ToString("o")
        }
    }
    try {
        return Get-Content -LiteralPath $Script:StatePath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Deployment state at $Script:StatePath is unreadable: $($_.Exception.Message). Inspect or remove it before re-running."
    }
}

function Save-DeployState {
    param([Parameter(Mandatory = $true)][psobject]$State)
    $State.updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    if (-not (Test-Path -LiteralPath $Script:Layout.StateRoot)) {
        New-Item -ItemType Directory -Path $Script:Layout.StateRoot -Force | Out-Null
    }
    $temp = "$Script:StatePath.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        $State | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temp -Encoding UTF8
        Move-Item -LiteralPath $temp -Destination $Script:StatePath -Force
    }
    finally {
        Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
    }
}

# --------------------------------------------------------------------------------------------
# Secrets - DPAPI LocalMachine store, schema shared with MachineSecretsConfigurationExtensions
# --------------------------------------------------------------------------------------------

function ConvertFrom-SecureStringToPlainText {
    param([Parameter(Mandatory = $true)][SecureString]$Secure)
    $pointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function New-RandomSecret {
    param([int]$ByteCount = 48)
    $bytes = New-Object byte[] $ByteCount
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($bytes)
}

function New-DatabaseAppPassword {
    <#
        Generated PostgreSQL password for the application login role. Alphanumeric only: the value
        goes into a single-quoted SQL literal and an Npgsql connection string, and restricting the
        alphabet keeps both unambiguous without escaping. 32 characters from the OS CSPRNG.
    #>
    param([int]$Length = 32)
    $alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
    $bytes = New-Object byte[] $Length
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $characters = foreach ($byte in $bytes) { $alphabet[[int]$byte % $alphabet.Length] }
    return -join $characters
}

function Get-SetupKeyHash {
    param([Parameter(Mandatory = $true)][string]$SetupKey)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try { $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($SetupKey)) }
    finally { $sha256.Dispose() }
    return "sha256:" + ([Convert]::ToBase64String($hashBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'))
}

function Get-MachineSecretsPath { return (Join-Path $Script:Layout.SecretsRoot "runtime.secrets.dpapi") }

function Read-MachineSecrets {
    $path = Get-MachineSecretsPath
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    Add-Type -AssemblyName System.Security -ErrorAction SilentlyContinue
    $protected = [System.IO.File]::ReadAllBytes($path)
    if ($null -eq $protected -or $protected.Length -eq 0) { return $null }
    try {
        $plainBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
            $protected, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
        return [System.Text.Encoding]::UTF8.GetString($plainBytes) | ConvertFrom-Json
    }
    catch {
        throw "Machine secret store at $path could not be decrypted. LocalMachine DPAPI secrets do not " +
              "travel to another host. Re-enter secrets or restore this machine's DPAPI state."
    }
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
    $plainBytes = [System.Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Compress))
    Add-Type -AssemblyName System.Security -ErrorAction SilentlyContinue
    $protected = [System.Security.Cryptography.ProtectedData]::Protect(
        $plainBytes, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
    $path = Get-MachineSecretsPath
    [System.IO.File]::WriteAllBytes($path, $protected)

    $identity = "IIS AppPool\$AppPoolName"
    & icacls $Script:Layout.SecretsRoot /inheritance:r | Out-Null
    & icacls $Script:Layout.SecretsRoot /grant:r "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null
    & icacls $Script:Layout.SecretsRoot /grant:r "${identity}:(OI)(CI)R" | Out-Null
    & icacls $path /inheritance:r | Out-Null
    & icacls $path /grant:r "SYSTEM:F" "Administrators:F" "${identity}:R" | Out-Null
    Write-Detail "Machine secrets written to the DPAPI-protected store under $($Script:Layout.SecretsRoot)"
}

# --------------------------------------------------------------------------------------------
# Preflight
# --------------------------------------------------------------------------------------------

$Script:RequiredIisFeatures = @(
    "Web-Server", "Web-Default-Doc", "Web-Http-Errors", "Web-Static-Content",
    "Web-Http-Logging", "Web-Stat-Compression", "Web-Filtering",
    "Web-Mgmt-Console", "Web-Scripting-Tools"
)

function Get-ToolVersion {
    param([string]$Command, [string]$Argument = "--version")
    $found = Get-Command $Command -ErrorAction SilentlyContinue
    if ($null -eq $found) { return $null }
    try { return ((& $Command $Argument 2>&1 | Out-String).Trim() -split "`n")[0].Trim() }
    catch { return "" }
}

function Test-Preflight {
    Write-Step "Preflight"

    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    Write-Detail "OS: $($os.Caption) ($($os.Version))"
    if ($os.ProductType -eq 1) {
        throw "Client Windows is not supported. ITAdmin production requires Windows Server 2022 or 2025."
    }
    if ([version]$os.Version -lt [version]"10.0.20348") {
        throw "Windows Server 2022 or newer is required (found $($os.Version))."
    }

    $problems = New-Object System.Collections.Generic.List[string]

    $gitVersion = Get-ToolVersion -Command "git"
    if ($null -eq $gitVersion) { $problems.Add("Git is not on PATH. Install Git for Windows: https://git-scm.com/download/win") }
    else { Write-Detail "git:    $gitVersion" }

    $dotnetVersion = Get-ToolVersion -Command "dotnet"
    if ($null -eq $dotnetVersion) {
        $problems.Add("The .NET SDK is not on PATH. Install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0")
    }
    else {
        Write-Detail "dotnet: $dotnetVersion"
        $sdks = & dotnet --list-sdks 2>&1
        if (-not ($sdks | Where-Object { "$_" -match '^10\.' })) {
            $problems.Add("A .NET 10 SDK was not found (dotnet --list-sdks). Install it from https://dotnet.microsoft.com/download/dotnet/10.0")
        }
    }

    $nodeVersion = Get-ToolVersion -Command "node" -Argument "--version"
    if ($null -eq $nodeVersion) {
        $problems.Add("Node.js is not on PATH. Install the current LTS: https://nodejs.org/")
    }
    else {
        Write-Detail "node:   $nodeVersion"
        $major = 0
        if ($nodeVersion -match 'v?(\d+)\.' -and [int]::TryParse($Matches[1], [ref]$major) -and $major -lt 20) {
            $problems.Add("Node.js 20 or newer is required (found $nodeVersion).")
        }
    }
    if ($null -eq (Get-Command "npm" -ErrorAction SilentlyContinue)) {
        $problems.Add("npm is not on PATH (it ships with Node.js).")
    }

    if ($null -eq (Get-Module -ListAvailable -Name WebAdministration)) {
        if ($InstallIisFeatures.IsPresent) {
            Install-RequiredIisFeatures
        }
        else {
            $problems.Add("IIS is not installed (WebAdministration module missing). Re-run with -InstallIisFeatures.")
        }
    }
    else {
        $missing = @()
        foreach ($feature in $Script:RequiredIisFeatures) {
            $state = Get-WindowsFeature -Name $feature -ErrorAction SilentlyContinue
            if ($null -eq $state -or -not $state.Installed) { $missing += $feature }
        }
        if ($missing.Count -gt 0) {
            if ($InstallIisFeatures.IsPresent) { Install-RequiredIisFeatures -Features $missing }
            else { $problems.Add("Missing IIS features: $($missing -join ', '). Re-run with -InstallIisFeatures.") }
        }
    }

    $ancm = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    $sharedFx = Join-Path $env:ProgramFiles "dotnet\shared\Microsoft.AspNetCore.App"
    $sharedFxOk = $false
    if (Test-Path -LiteralPath $sharedFx) {
        foreach ($dir in (Get-ChildItem -LiteralPath $sharedFx -Directory -ErrorAction SilentlyContinue)) {
            $v = $null
            if ([version]::TryParse($dir.Name, [ref]$v) -and $v.Major -eq 10) { $sharedFxOk = $true; break }
        }
    }
    if (-not (Test-Path -LiteralPath $ancm) -or -not $sharedFxOk) {
        $problems.Add("The ASP.NET Core 10 Hosting Bundle is missing or incomplete. Install it from " +
                      "https://dotnet.microsoft.com/download/dotnet/10.0 (ASP.NET Core Runtime -> Hosting Bundle), then re-run.")
    }
    else {
        Write-Detail "ASP.NET Core Module V2 present; Microsoft.AspNetCore.App 10.x present"
    }

    if ($problems.Count -gt 0) {
        foreach ($p in $problems) { Write-Fail $p }
        throw "Preflight failed with $($problems.Count) blocking problem(s). No changes were made."
    }
    Write-Ok "All prerequisites confirmed"
}

function Install-RequiredIisFeatures {
    param([string[]]$Features = $Script:RequiredIisFeatures)
    Write-Detail "Installing IIS features: $($Features -join ', ')"
    $result = Install-WindowsFeature -Name ($Features | Select-Object -Unique) -ErrorAction Stop
    if ($null -ne $result -and $result.Success -eq $false) {
        throw "Install-WindowsFeature reported failure for: $($Features -join ', ')."
    }
    if ($null -ne $result -and "$($result.RestartNeeded)" -match '^(Yes|True)$') {
        throw "IIS feature installation requires a reboot. Reboot this server, then re-run."
    }
    Import-Module WebAdministration -ErrorAction Stop
    Write-Ok "IIS features installed"
}

# --------------------------------------------------------------------------------------------
# Configuration
# --------------------------------------------------------------------------------------------

function Get-JoinedDomainInfo {
    try {
        $cs = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop
        if ($cs.PartOfDomain -and -not [string]::IsNullOrWhiteSpace($cs.Domain)) {
            $baseDn = (($cs.Domain -split '\.' | Where-Object { $_ } | ForEach-Object { "DC=$_" }) -join ',')
            return [pscustomobject]@{ Domain = $cs.Domain; BaseDn = $baseDn }
        }
    }
    catch { Write-Verbose "Domain discovery failed: $($_.Exception.Message)" }
    return $null
}

function Read-RequiredValue {
    param(
        [string]$Supplied, [string]$Existing, [string]$Default,
        [Parameter(Mandatory = $true)][string]$Prompt,
        [Parameter(Mandatory = $true)][string]$Name
    )
    if (-not [string]::IsNullOrWhiteSpace($Supplied)) { return $Supplied }
    if (-not [string]::IsNullOrWhiteSpace($Existing)) { return $Existing }
    if ($Unattended.IsPresent) {
        if (-not [string]::IsNullOrWhiteSpace($Default)) { return $Default }
        throw "-$Name is required in unattended mode and was not found in $Script:AppConfigPath."
    }
    $suffix = if (-not [string]::IsNullOrWhiteSpace($Default)) { " [$Default]" } else { "" }
    $value = Read-Host ($Prompt + $suffix)
    if ([string]::IsNullOrWhiteSpace($value)) {
        if (-not [string]::IsNullOrWhiteSpace($Default)) { return $Default }
        throw "$Prompt is required."
    }
    return $value
}

function Resolve-AppConfig {
    param([switch]$RequireExisting)

    $existing = $null
    if (Test-Path -LiteralPath $Script:AppConfigPath) {
        $existing = Get-Content -LiteralPath $Script:AppConfigPath -Raw | ConvertFrom-Json
    }
    elseif ($RequireExisting.IsPresent) {
        throw "No configuration found at $Script:AppConfigPath. Run a full deployment first."
    }

    Write-Step "Resolving configuration"

    $dbHost = Read-RequiredValue -Supplied $DatabaseHost -Existing $(if ($existing) { $existing.database.host }) `
        -Prompt "PostgreSQL host" -Name "DatabaseHost"
    $dbName = Read-RequiredValue -Supplied $DatabaseName -Existing $(if ($existing) { $existing.database.name }) `
        -Default "itadmin" -Prompt "PostgreSQL database name" -Name "DatabaseName"
    $dbUser = Read-RequiredValue -Supplied $DatabaseUser -Existing $(if ($existing) { $existing.database.username }) `
        -Default "itadmin_app" -Prompt "PostgreSQL application user" -Name "DatabaseUser"
    $dbPort = if ($PSBoundParameters.ContainsKey('DatabasePort')) { $DatabasePort }
              elseif ($existing) { [int]$existing.database.port } else { $DatabasePort }

    $discovered = Get-JoinedDomainInfo
    $dirHost = Read-RequiredValue -Supplied $DirectoryHost -Existing $(if ($existing) { $existing.directory.host }) `
        -Default $(if ($discovered) { $discovered.Domain }) -Prompt "Directory host (AD domain or controller)" -Name "DirectoryHost"
    $dirBaseDn = Read-RequiredValue -Supplied $DirectoryBaseDn -Existing $(if ($existing) { $existing.directory.baseDn }) `
        -Default $(if ($discovered) { $discovered.BaseDn }) -Prompt "Directory Base DN" -Name "DirectoryBaseDn"
    $dirFilter = if (-not [string]::IsNullOrWhiteSpace($DirectoryUserSearchFilter)) { $DirectoryUserSearchFilter }
                 elseif ($existing -and $existing.directory.userSearchFilter) { $existing.directory.userSearchFilter }
                 else { "(sAMAccountName={0})" }
    $dirName = if (-not [string]::IsNullOrWhiteSpace($DirectoryName)) { $DirectoryName }
               elseif ($existing -and $existing.directory.name) { $existing.directory.name }
               else { $dirHost }
    $dirBindUser = Read-RequiredValue -Supplied $DirectoryBindUser -Existing $(if ($existing) { $existing.directory.bindUser }) `
        -Prompt "Directory bind account" -Name "DirectoryBindUser"
    $dirBindDomain = if (-not [string]::IsNullOrWhiteSpace($DirectoryBindDomain)) { $DirectoryBindDomain }
                     elseif ($existing -and $existing.directory.bindDomain) { $existing.directory.bindDomain }
                     else { $null }
    $initialAdmin = Read-RequiredValue -Supplied $InitialAdministrator -Existing $(if ($existing) { $existing.initialAdministrator }) `
        -Prompt "Initial ITAdmin administrator (UPN / sAMAccountName / mail)" -Name "InitialAdministrator"

    $hostHeader = if ($PSBoundParameters.ContainsKey('HttpHostHeader')) { $HttpHostHeader }
                  elseif ($existing -and $existing.web.httpHostHeader) { $existing.web.httpHostHeader }
                  else { $null }
    $httpPortResolved = if ($PSBoundParameters.ContainsKey('HttpPort')) { $HttpPort }
                        elseif ($existing) { [int]$existing.web.httpPort } else { $HttpPort }

    $preservedHttps = if ($existing -and $existing.web.https) { $existing.web.https } else {
        [pscustomobject]@{ enabled = $false; port = 443; certificateThumbprint = $null; redirectHttpToHttps = $false }
    }

    $config = [pscustomobject]@{
        schemaVersion       = 1
        initialAdministrator = $initialAdmin
        web = [pscustomobject]@{
            httpPort       = $httpPortResolved
            httpHostHeader = $(if ([string]::IsNullOrWhiteSpace($hostHeader)) { $null } else { $hostHeader })
            https          = $preservedHttps
        }
        database = [pscustomobject]@{
            host = $dbHost; port = $dbPort; name = $dbName; username = $dbUser; sslMode = "Prefer"
        }
        directory = [pscustomobject]@{
            name = $dirName; host = $dirHost; baseDn = $dirBaseDn
            userSearchFilter = $dirFilter; bindUser = $dirBindUser; bindDomain = $dirBindDomain
        }
        iis = [pscustomobject]@{ siteName = $SiteName; appPoolName = $AppPoolName }
    }

    Write-Ok "Configuration resolved (database $($dbHost):$dbPort/$dbName, directory $dirHost)"
    return $config
}

function Save-AppConfig {
    param([Parameter(Mandatory = $true)][psobject]$Config)
    if (-not (Test-Path -LiteralPath $Script:Layout.ConfigRoot)) {
        New-Item -ItemType Directory -Path $Script:Layout.ConfigRoot -Force | Out-Null
    }
    $Config | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Script:AppConfigPath -Encoding UTF8
    Write-Detail "Configuration written to $Script:AppConfigPath"
}

# --------------------------------------------------------------------------------------------
# Source sync
# --------------------------------------------------------------------------------------------

function Sync-Source {
    Write-Step "Synchronising source from $RepositoryUrl ($Branch)"

    if (-not (Test-Path -LiteralPath (Join-Path $Script:Layout.SrcRoot ".git"))) {
        if (Test-Path -LiteralPath $Script:Layout.SrcRoot) {
            if (@(Get-ChildItem -LiteralPath $Script:Layout.SrcRoot -Force).Count -gt 0) {
                throw "$($Script:Layout.SrcRoot) exists and is not a Git clone. Move it aside or point -InstallRoot elsewhere."
            }
        }
        else {
            New-Item -ItemType Directory -Path $Script:Layout.InstallRoot -Force | Out-Null
        }
        & git clone --branch $Branch --single-branch $RepositoryUrl $Script:Layout.SrcRoot
        if ($LASTEXITCODE -ne 0) { throw "git clone failed (exit $LASTEXITCODE)." }
    }
    else {
        & git -C $Script:Layout.SrcRoot remote set-url origin $RepositoryUrl
        & git -C $Script:Layout.SrcRoot fetch --prune origin
        if ($LASTEXITCODE -ne 0) { throw "git fetch failed (exit $LASTEXITCODE)." }
        & git -C $Script:Layout.SrcRoot reset --hard "origin/$Branch"
        if ($LASTEXITCODE -ne 0) { throw "git reset --hard origin/$Branch failed (exit $LASTEXITCODE)." }
        & git -C $Script:Layout.SrcRoot clean -fdx | Out-Null
    }

    $sha = (& git -C $Script:Layout.SrcRoot rev-parse --short HEAD).Trim()
    $subject = (& git -C $Script:Layout.SrcRoot log -1 --pretty=%s).Trim()
    Write-Ok "Source at $sha - $subject"
    return [pscustomobject]@{ Sha = $sha; Subject = $subject }
}

# --------------------------------------------------------------------------------------------
# Build
# --------------------------------------------------------------------------------------------

function Invoke-Build {
    param([Parameter(Mandatory = $true)][string]$Sha)

    Write-Step "Building release $Sha"
    $src = $Script:Layout.SrcRoot
    $appOut = Join-Path $Script:Layout.AppRoot $Sha
    $agentOut = Join-Path $Script:Layout.HostAgentRoot $Sha
    $coordOut = Join-Path $Script:Layout.CoordinatorRoot $Sha

    foreach ($dir in @($appOut, $agentOut, $coordOut)) {
        if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    Write-Detail "dotnet publish ITAdmin.Api"
    & dotnet publish (Join-Path $src "backend\src\ITAdmin.Api\ITAdmin.Api.csproj") `
        -c Release -o $appOut --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish (ITAdmin.Api) failed (exit $LASTEXITCODE)." }

    Write-Detail "dotnet publish ITAdmin.HostAgent"
    & dotnet publish (Join-Path $src "backend\src\ITAdmin.HostAgent\ITAdmin.HostAgent.csproj") `
        -c Release -o $agentOut --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish (ITAdmin.HostAgent) failed (exit $LASTEXITCODE)." }

    Write-Detail "dotnet publish ITAdmin.UpdateCoordinator"
    & dotnet publish (Join-Path $src "backend\src\ITAdmin.UpdateCoordinator\ITAdmin.UpdateCoordinator.csproj") `
        -c Release -o $coordOut --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish (ITAdmin.UpdateCoordinator) failed (exit $LASTEXITCODE)." }

    Write-Detail "npm ci && npm run build (frontend)"
    Push-Location (Join-Path $src "frontend")
    try {
        & npm ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed (exit $LASTEXITCODE)." }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build failed (exit $LASTEXITCODE)." }
    }
    finally { Pop-Location }

    $wwwroot = Join-Path $appOut "wwwroot"
    if (-not (Test-Path -LiteralPath $wwwroot)) { New-Item -ItemType Directory -Path $wwwroot -Force | Out-Null }
    Copy-Item -Path (Join-Path (Join-Path $src "frontend") "dist\*") -Destination $wwwroot -Recurse -Force

    Remove-OldBuilds -Root $Script:Layout.AppRoot -Keep @($Sha)
    Remove-OldBuilds -Root $Script:Layout.HostAgentRoot -Keep @($Sha)
    Remove-OldBuilds -Root $Script:Layout.CoordinatorRoot -Keep @($Sha)

    Write-Ok "Build complete: $appOut"
}

function Remove-OldBuilds {
    param([string]$Root, [string[]]$Keep)
    if (-not (Test-Path -LiteralPath $Root)) { return }
    $state = Get-DeployState
    $protected = @($Keep) + @($state.activeSha, $state.previousSha) | Where-Object { $_ }
    $dirs = Get-ChildItem -LiteralPath $Root -Directory | Sort-Object LastWriteTime -Descending
    $index = 0
    foreach ($dir in $dirs) {
        $index++
        if ($dir.Name -in $protected) { continue }
        if ($index -le $Script:KeepBuilds) { continue }
        Remove-Item -LiteralPath $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --------------------------------------------------------------------------------------------
# Database
# --------------------------------------------------------------------------------------------

function Invoke-DatabaseProvisioning {
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][string]$AppExe
    )
    Write-Step "Provisioning the database and application role"

    if ($null -ne $DatabasePassword) {
        $Script:DatabaseAppPassword = ConvertFrom-SecureStringToPlainText -Secure $DatabasePassword
        $Script:DatabaseAppPasswordGenerated = $false
        Write-Detail "Using the supplied application-role password (-DatabasePassword)."
    }
    else {
        $Script:DatabaseAppPassword = New-DatabaseAppPassword
        $Script:DatabaseAppPasswordGenerated = $true
        Write-Detail "Generated an application-role password (32 chars, CSPRNG)."
    }

    $adminUser = Read-RequiredValue -Supplied $DatabaseAdminUser -Existing $null `
        -Prompt "PostgreSQL administrator user (used only to create the database and role)" -Name "DatabaseAdminUser"
    $adminSecure = $DatabaseAdminPassword
    if ($null -eq $adminSecure) {
        if ($Unattended.IsPresent) { throw "-DatabaseAdminPassword is required in unattended mode." }
        $adminSecure = Read-Host -AsSecureString "PostgreSQL administrator password for '$adminUser'"
    }
    $adminPlain = ConvertFrom-SecureStringToPlainText -Secure $adminSecure
    if ([string]::IsNullOrWhiteSpace($adminPlain)) { throw "The PostgreSQL administrator password is required." }

    $adminConnection = "Host=$($Config.database.host);Port=$($Config.database.port);" +
                       "Database=$DatabaseAdminDatabase;Username=$adminUser;Password=$adminPlain;" +
                       "SSL Mode=$($Config.database.sslMode);Trust Server Certificate=true"

    $inputPath = Join-Path $Script:Layout.StateRoot ("database-provision-{0}.json" -f [guid]::NewGuid().ToString("N"))
    try {
        [pscustomobject]@{
            adminConnectionString = $adminConnection
            targetDatabase        = $Config.database.name
            appRole               = $Config.database.username
            appRolePassword       = $Script:DatabaseAppPassword
        } | ConvertTo-Json -Compress | Set-Content -LiteralPath $inputPath -Encoding UTF8
        & icacls $inputPath /inheritance:r | Out-Null
        & icacls $inputPath /grant:r "SYSTEM:F" "Administrators:F" | Out-Null

        $output = & $AppExe --provision-database --input $inputPath 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 4) {
            foreach ($line in $output) { Write-Fail "$line" }
            throw "PostgreSQL accepted the administrator credential but the database precondition is still not met."
        }
        if ($exitCode -ne 0) {
            foreach ($line in $output) { Write-Fail "$line" }
            throw "Database provisioning failed (exit $exitCode)."
        }
        $resultLine = @($output | Where-Object { "$_" -match '^\s*\{' } | Select-Object -Last 1)
        if ($resultLine.Count -gt 0) {
            $result = "$($resultLine[0])" | ConvertFrom-Json
            $changes = @()
            if ($result.roleCreated) { $changes += "role created" } elseif ($result.roleUpdated) { $changes += "role password set" }
            if ($result.databaseCreated) { $changes += "database created" }
            if ($changes.Count -eq 0) { $changes += "already provisioned" }
            Write-Ok ("Database '{0}' / role '{1}' ready ({2})" -f $Config.database.name, $Config.database.username, ($changes -join ", "))
        }
        else { Write-Ok "Database and application role are ready." }
    }
    finally {
        if (Test-Path -LiteralPath $inputPath) { Remove-Item -LiteralPath $inputPath -Force -ErrorAction SilentlyContinue }
    }
}

function Get-ConnectionString {
    param([Parameter(Mandatory = $true)][psobject]$Config)
    $existingSecrets = Read-MachineSecrets
    if ([string]::IsNullOrWhiteSpace($Script:DatabaseAppPassword) -and $null -ne $existingSecrets -and
        -not [string]::IsNullOrWhiteSpace($existingSecrets.connectionString)) {
        Write-Detail "Reusing the previously configured database connection from the machine secret store."
        return $existingSecrets.connectionString
    }
    if ([string]::IsNullOrWhiteSpace($Script:DatabaseAppPassword)) {
        throw "The application database password was not established; database provisioning must run first."
    }
    return "Host=$($Config.database.host);Port=$($Config.database.port);Database=$($Config.database.name);" +
           "Username=$($Config.database.username);Password=$($Script:DatabaseAppPassword);" +
           "SSL Mode=$($Config.database.sslMode);Trust Server Certificate=true"
}

function Invoke-DatabaseMigration {
    param([Parameter(Mandatory = $true)][string]$AppExe)
    Write-Step "Applying database migrations"

    $previous = @{}
    foreach ($name in @("ITADMIN_ConnectionStrings__DefaultConnection", "ITADMIN_Jwt__Key", "ITADMIN_Secrets__Root")) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name)
    }
    try {
        $env:ITADMIN_Secrets__Root = $Script:Layout.SecretsRoot
        Remove-Item Env:\ITADMIN_ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
        Remove-Item Env:\ITADMIN_Jwt__Key -ErrorAction SilentlyContinue

        $output = & $AppExe --migrate 2>&1
        $exitCode = $LASTEXITCODE
        foreach ($line in $output) {
            if ("$line" -match 'currentMigration=(.+)$') { $Script:LastMigrationApplied = $Matches[1].Trim() }
            if ("$line" -match '^(Applying|No pending|Migration completed|  \d{14}_)') { Write-Detail "$line" }
        }
        if ($exitCode -ne 0) { throw "Database migration failed (exit $exitCode)." }
    }
    finally {
        foreach ($name in $previous.Keys) {
            if ($null -ne $previous[$name]) { Set-Item "Env:\$name" -Value $previous[$name] }
            else { Remove-Item "Env:\$name" -ErrorAction SilentlyContinue }
        }
    }
    Write-Ok "Schema at $($Script:LastMigrationApplied)"
}

# --------------------------------------------------------------------------------------------
# Runtime configuration (app pool + DPAPI secrets)
# --------------------------------------------------------------------------------------------

function Register-MachineLayout {
    <#
        The Host Agent and the Update Coordinator both need to find %ProgramData%\ITAdmin\config
        before they can read anything inside it - a chicken-and-egg problem when -DataRoot is
        non-default. The registry value breaks that: it is the one thing both LocalSystem processes
        read before they know anything else about this machine.
    #>
    $registryPath = "HKLM:\SOFTWARE\ITAdmin"
    New-Item -Path $registryPath -Force | Out-Null
    New-ItemProperty -Path $registryPath -Name "ProgramDataRoot" -Value $Script:Layout.DataRoot -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $registryPath -Name "InstallRoot" -Value $Script:Layout.InstallRoot -PropertyType String -Force | Out-Null
}

function New-MachineDirectories {
    foreach ($dir in @(
        $Script:Layout.InstallRoot, $Script:Layout.AppRoot, $Script:Layout.HostAgentRoot, $Script:Layout.CoordinatorRoot,
        $Script:Layout.ConfigRoot, $Script:Layout.SecretsRoot, $Script:Layout.StateRoot,
        $Script:Layout.DataProtectionRoot, $Script:Layout.LogsRoot)) {
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    }
}

function Initialize-AppPool {
    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
        Write-Detail "Created app pool $AppPoolName"
    }
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "managedRuntimeVersion" -Value ""
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "startMode" -Value "AlwaysRunning"
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "processModel.idleTimeout" -Value ([TimeSpan]::Zero)
}

function Get-AppPoolEnvironmentVariable {
    param([Parameter(Mandatory = $true)][string]$Name)
    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) { return $null }
    $collection = Get-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "environmentVariables" -ErrorAction SilentlyContinue
    if ($null -eq $collection -or $null -eq $collection.Collection) { return $null }
    foreach ($entry in $collection.Collection) { if ($entry.name -eq $Name) { return $entry.value } }
    return $null
}

function Set-AppPoolEnvironmentVariables {
    param([Parameter(Mandatory = $true)][hashtable]$Variables)
    foreach ($name in $Variables.Keys) {
        if ($null -ne (Get-AppPoolEnvironmentVariable -Name $name)) {
            Remove-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" `
                -Filter "system.applicationHost/applicationPools/add[@name='$AppPoolName']/environmentVariables" `
                -Name "." -AtElement @{ name = $name } -ErrorAction SilentlyContinue
        }
        Add-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" `
            -Filter "system.applicationHost/applicationPools/add[@name='$AppPoolName']/environmentVariables" `
            -Name "." -Value @{ name = $name; value = $Variables[$name] }
    }
}

function Set-RuntimeConfiguration {
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][string]$ConnectionString
    )
    Write-Step "Configuring application runtime"
    Initialize-AppPool

    $existingSecrets = Read-MachineSecrets
    $jwtKey = if ($null -ne $existingSecrets -and -not [string]::IsNullOrWhiteSpace($existingSecrets.jwtKey)) {
        Write-Detail "Preserved the existing JWT signing key."
        $existingSecrets.jwtKey
    }
    else { Write-Detail "Generated a JWT signing key (48 bytes, CSPRNG)."; New-RandomSecret }

    $setupKey = if ($null -ne $existingSecrets -and
        $existingSecrets.PSObject.Properties.Match('setupKey').Count -gt 0 -and
        -not [string]::IsNullOrWhiteSpace($existingSecrets.setupKey)) {
        Write-Detail "Preserved the existing first-run setup key."
        $existingSecrets.setupKey
    }
    else { Write-Detail "Generated a first-run setup key (48 bytes, CSPRNG)."; New-RandomSecret }
    $Script:SetupKey = $setupKey

    Save-MachineSecrets -ConnectionString $ConnectionString -JwtKey $jwtKey -SetupKey $setupKey

    Set-AppPoolEnvironmentVariables -Variables @{
        "ASPNETCORE_ENVIRONMENT"                  = "Production"
        "ITADMIN_Secrets__Root"                   = $Script:Layout.SecretsRoot
        "ITADMIN_Jwt__Issuer"                     = "ITAdmin"
        "ITADMIN_Jwt__Audience"                   = "ITAdmin"
        "ITADMIN_DataProtection__ApplicationName" = "ITAdmin"
        "ITADMIN_DataProtection__KeysPath"        = $Script:Layout.DataProtectionRoot
    }

    $identity = "IIS AppPool\$AppPoolName"
    foreach ($writable in @($Script:Layout.DataProtectionRoot, $Script:Layout.LogsRoot)) {
        & icacls $writable /grant "${identity}:(OI)(CI)M" /T | Out-Null
    }
    & icacls $Script:Layout.ConfigRoot /grant "${identity}:(OI)(CI)R" /T | Out-Null
    Write-Ok "Runtime configuration applied (secrets in the DPAPI store; non-secret app pool variables set)"
}

# --------------------------------------------------------------------------------------------
# Directory bootstrap (first run only)
# --------------------------------------------------------------------------------------------

function Invoke-DirectoryBootstrap {
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][string]$AppExe
    )
    Write-Step "Establishing the Primary Directory and the initial administrator"
    if ([string]::IsNullOrWhiteSpace($Script:SetupKey)) {
        throw "The first-run setup key was not established; runtime configuration must run first."
    }

    $bindSecure = $DirectoryBindPassword
    if ($null -eq $bindSecure) {
        if ($Unattended.IsPresent) { throw "-DirectoryBindPassword is required in unattended mode." }
        $bindSecure = Read-Host -AsSecureString "Password for '$($Config.directory.bindUser)'"
    }
    $bindPlain = ConvertFrom-SecureStringToPlainText -Secure $bindSecure
    if ([string]::IsNullOrWhiteSpace($bindPlain)) { throw "The directory bind password is required." }

    $inputPath = Join-Path $Script:Layout.StateRoot ("directory-bootstrap-{0}.json" -f [guid]::NewGuid().ToString("N"))
    try {
        [pscustomobject]@{
            setupKey                = $Script:SetupKey
            directoryName           = $Config.directory.name
            host                    = $Config.directory.host
            baseDn                  = $Config.directory.baseDn
            userSearchFilter        = $Config.directory.userSearchFilter
            bindUserName            = $Config.directory.bindUser
            bindUserDomain          = $Config.directory.bindDomain
            bindPassword            = $bindPlain
            administratorIdentifier = $Config.initialAdministrator
        } | ConvertTo-Json -Compress | Set-Content -LiteralPath $inputPath -Encoding UTF8
        & icacls $inputPath /inheritance:r | Out-Null
        & icacls $inputPath /grant:r "SYSTEM:F" "Administrators:F" | Out-Null

        $previousSecretsRoot = $env:ITADMIN_Secrets__Root
        try {
            $env:ITADMIN_Secrets__Root = $Script:Layout.SecretsRoot
            $output = & $AppExe --bootstrap-directory --input $inputPath 2>&1
            $exitCode = $LASTEXITCODE
        }
        finally {
            if ($null -ne $previousSecretsRoot) { $env:ITADMIN_Secrets__Root = $previousSecretsRoot }
            else { Remove-Item Env:\ITADMIN_Secrets__Root -ErrorAction SilentlyContinue }
        }

        if ($exitCode -eq 3) {
            foreach ($line in $output) { Write-Fail "$line" }
            throw "The directory rejected the supplied configuration. Correct the values and re-run; no administrator was created."
        }
        if ($exitCode -ne 0) {
            foreach ($line in $output) { Write-Fail "$line" }
            throw "Directory bootstrap failed (exit $exitCode)."
        }
        $resultLine = @($output | Where-Object { "$_" -match '^\s*\{' } | Select-Object -Last 1)
        if ($resultLine.Count -gt 0) {
            $result = "$($resultLine[0])" | ConvertFrom-Json
            if ($result.status -eq "AlreadyBootstrapped") {
                Write-Ok "Directory configuration and the initial administrator already exist; nothing was changed."
            }
            else {
                Write-Ok "Initial administrator '$($result.administratorUserName)' resolved from the directory and granted access"
            }
        }
        else { Write-Ok "Directory configured." }
    }
    finally {
        if (Test-Path -LiteralPath $inputPath) { Remove-Item -LiteralPath $inputPath -Force -ErrorAction SilentlyContinue }
    }
}

# --------------------------------------------------------------------------------------------
# IIS site and activation
# --------------------------------------------------------------------------------------------

function Get-ActiveAppPath {
    if (-not (Test-Path "IIS:\Sites\$SiteName")) { return $null }
    return (Get-ItemProperty -Path "IIS:\Sites\$SiteName" -Name "physicalPath" -ErrorAction SilentlyContinue).physicalPath
}

function Set-ActiveAppPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    Set-ItemProperty -Path "IIS:\Sites\$SiteName" -Name "physicalPath" -Value $Path
    if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne "Started") { Start-WebAppPool -Name $AppPoolName }
    else { Restart-WebAppPool -Name $AppPoolName }
    Start-Website -Name $SiteName -ErrorAction SilentlyContinue
}

function Set-IisSite {
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][string]$AppPath
    )
    Write-Step "Pointing IIS at $AppPath"

    if (-not (Test-Path "IIS:\Sites\$SiteName")) {
        $common = @{ Name = $SiteName; PhysicalPath = $AppPath; ApplicationPool = $AppPoolName; Port = $Config.web.httpPort; Force = $true }
        if (-not [string]::IsNullOrWhiteSpace($Config.web.httpHostHeader)) { $common["HostHeader"] = $Config.web.httpHostHeader }
        New-Website @common | Out-Null
        Write-Detail "Created site $SiteName on port $($Config.web.httpPort)$(if ($Config.web.httpHostHeader) { " (host header $($Config.web.httpHostHeader))" })"
    }
    else {
        Set-ItemProperty -Path "IIS:\Sites\$SiteName" -Name "applicationPool" -Value $AppPoolName
    }
    Set-ActiveAppPath -Path $AppPath
    Write-Detail "Site physical path -> $AppPath"
}

function Set-HttpsBinding {
    param([Parameter(Mandatory = $true)][psobject]$Config)
    Write-Step "Configuring the HTTPS binding"

    $thumbprint = $CertificateThumbprint
    if ([string]::IsNullOrWhiteSpace($thumbprint)) {
        if ($Unattended.IsPresent) { throw "-CertificateThumbprint is required in unattended mode." }
        Write-Detail "Certificates in Cert:\LocalMachine\My:"
        Get-ChildItem Cert:\LocalMachine\My | ForEach-Object {
            Write-Host ("      {0}  {1}  (expires {2:yyyy-MM-dd})" -f $_.Thumbprint, $_.Subject, $_.NotAfter)
        }
        $thumbprint = Read-Host "Certificate thumbprint"
    }
    $thumbprint = ($thumbprint -replace '\s', '').ToUpperInvariant()
    $cert = Get-Item -Path "Cert:\LocalMachine\My\$thumbprint" -ErrorAction SilentlyContinue
    if ($null -eq $cert) { throw "No certificate with thumbprint $thumbprint in Cert:\LocalMachine\My." }

    $hostHeader = if (-not [string]::IsNullOrWhiteSpace($Config.web.httpHostHeader)) { $Config.web.httpHostHeader } else { "" }
    $existing = Get-WebBinding -Name $SiteName -Protocol "https" -ErrorAction SilentlyContinue
    foreach ($b in @($existing)) {
        if ($null -ne $b) { Remove-WebBinding -Name $SiteName -Protocol "https" -Port $b.bindingInformation.Split(':')[1] -ErrorAction SilentlyContinue }
    }
    New-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -HostHeader $hostHeader -SslFlags $(if ($hostHeader) { 1 } else { 0 })
    $binding = Get-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort
    $binding.AddSslCertificate($cert.Thumbprint, "My")

    $Config.web.https = [pscustomobject]@{
        enabled = $true; port = $HttpsPort
        certificateThumbprint = $cert.Thumbprint
        redirectHttpToHttps = [bool]$RedirectHttpToHttps.IsPresent
    }
    Write-Ok "HTTPS binding on port $HttpsPort using certificate $($cert.Thumbprint)"
    if ($RedirectHttpToHttps.IsPresent) {
        Write-Detail "HTTP-to-HTTPS redirect is recorded; the application enforces it via UseHttpsRedirection."
    }
}

# --------------------------------------------------------------------------------------------
# Host Agent service
# --------------------------------------------------------------------------------------------

function Write-HostAgentConfig {
    param([Parameter(Mandatory = $true)][psobject]$Config)
    $settings = [ordered]@{
        schemaVersion  = 2
        repositoryUrl  = $RepositoryUrl
        branch         = $Branch
        installRoot    = $Script:Layout.InstallRoot
        dataRoot       = $Script:Layout.DataRoot
        siteName       = $Config.iis.siteName
        appPoolName    = $Config.iis.appPoolName
        updatesEnabled = $true
    }
    $settings | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Script:HostAgentConfigPath -Encoding UTF8
    Write-Detail "Host Agent settings written to $Script:HostAgentConfigPath"
}

function Install-HostAgentService {
    param([Parameter(Mandatory = $true)][string]$AgentDirectory)
    Write-Step "Installing the ITAdmin Host Agent"

    $agentExe = Join-Path $AgentDirectory "ITAdmin.HostAgent.exe"
    if (-not (Test-Path -LiteralPath $agentExe)) { throw "Host Agent executable not found: $agentExe" }

    # The app pool identity must never reach the privileged binaries.
    $identity = "IIS AppPool\$AppPoolName"
    & icacls $AgentDirectory /deny "${identity}:(OI)(CI)(F)" | Out-Null

    $existing = Get-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
    if ($null -ne $existing -and $existing.Status -eq "Running") {
        Stop-Service -Name "ITAdminHostAgent" -Force -ErrorAction SilentlyContinue
    }
    if ($null -eq $existing) {
        & sc.exe create "ITAdminHostAgent" binPath= "`"$agentExe`"" start= auto DisplayName= "ITAdmin Host Agent" obj= "LocalSystem" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not register the ITAdmin Host Agent service (sc.exe exit $LASTEXITCODE)." }
        & sc.exe description "ITAdminHostAgent" "Performs privileged ITAdmin host operations (source sync, build, and IIS reconciliation) over a local ACL'd named pipe." | Out-Null
    }
    else {
        & sc.exe config "ITAdminHostAgent" binPath= "`"$agentExe`"" start= auto | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not update the ITAdmin Host Agent service image path (sc.exe exit $LASTEXITCODE)." }
    }
    Start-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
    $service = Get-Service -Name "ITAdminHostAgent" -ErrorAction SilentlyContinue
    if ($null -ne $service -and $service.Status -eq "Running") { Write-Ok "ITAdmin Host Agent is running"; return $true }
    Write-Host "    WARN The ITAdmin Host Agent is registered but not running." -ForegroundColor Yellow
    return $false
}

# --------------------------------------------------------------------------------------------
# Health
# --------------------------------------------------------------------------------------------

function Test-Health {
    param([Parameter(Mandatory = $true)][psobject]$Config, [switch]$RequireSetupComplete)
    Write-Step "Health check"

    $hostName = if ([string]::IsNullOrWhiteSpace($Config.web.httpHostHeader)) { $env:COMPUTERNAME } else { $Config.web.httpHostHeader }
    $baseUrl = "http://${hostName}:$($Config.web.httpPort)"

    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -eq 200) {
                Write-Ok "Health endpoint returned 200"
                $setup = Invoke-WebRequest -Uri "$baseUrl/api/setup/status" -UseBasicParsing -TimeoutSec 15
                $setupStatus = $setup.Content | ConvertFrom-Json
                if ($RequireSetupComplete.IsPresent -and $setupStatus.isSetupRequired) {
                    Show-FailureDiagnostics
                    throw "The application is serving but reports first-run setup as INCOMPLETE. The directory bootstrap did not take effect."
                }
                Write-Ok "Application is serving"
                return
            }
        }
        catch {
            if ($attempt -eq 10) {
                Write-Fail "Last health check error: $($_.Exception.Message)"
                Show-FailureDiagnostics
                throw "Health check failed against $baseUrl/health after 10 attempts."
            }
        }
        Start-Sleep -Seconds 3
    }
}

function Show-FailureDiagnostics {
    $recent = Get-ChildItem -Path (Join-Path $Script:Layout.LogsRoot "*") -Filter "*.log" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -ne $recent) {
        Write-Host "    --- last 20 log lines ($($recent.Name)) ---" -ForegroundColor Yellow
        Get-Content -LiteralPath $recent.FullName -Tail 20 | ForEach-Object { Write-Host "    $_" }
    }
}

# --------------------------------------------------------------------------------------------
# Summary
# --------------------------------------------------------------------------------------------

function Write-DeploySummary {
    param(
        [Parameter(Mandatory = $true)][psobject]$Config,
        [Parameter(Mandatory = $true)][psobject]$Source,
        [Parameter(Mandatory = $true)][bool]$FirstRun
    )
    $hostName = if (-not [string]::IsNullOrWhiteSpace($Config.web.httpHostHeader)) { $Config.web.httpHostHeader } else { $env:COMPUTERNAME }
    $port = $Config.web.httpPort
    $authority = if ($port -eq 80) { $hostName } else { "${hostName}:${port}" }

    Write-Host ""
    Write-Host "ITAdmin deployment completed successfully." -ForegroundColor Green
    Write-Host ""
    Write-Host "  Source            $($Source.Sha) - $($Source.Subject)"
    Write-Host "  Branch            $Branch"
    Write-Host "  Web (HTTP)        http://$authority/"
    Write-Host "  Database          $($Config.database.host):$($Config.database.port)/$($Config.database.name)"
    Write-Host "  Schema            $(if ($Script:LastMigrationApplied) { $Script:LastMigrationApplied } else { 'current' })"
    Write-Host "  IIS               site $($Config.iis.siteName), app pool $($Config.iis.appPoolName)"
    Write-Host "  Active build      $(Join-Path $Script:Layout.AppRoot $Source.Sha)"
    Write-Host "  Config            $Script:AppConfigPath"
    Write-Host "  Secrets           $($Script:Layout.SecretsRoot)  (Windows DPAPI LocalMachine)"
    Write-Host "  State             $Script:StatePath"
    Write-Host ""
    if ($FirstRun) {
        Write-Host "  Next: open http://$authority/ and sign in with the initial administrator's directory credentials."
        Write-Host "        Configure HTTPS later with:  .\Deploy-ITAdmin.ps1 -ConfigureHttps"
        Write-Host ""
    }
    else {
        Write-Host "  Update: run this script again, or use Settings -> Updates in the app."
        Write-Host ""
    }

    if ($Script:DatabaseAppPasswordGenerated -and -not [string]::IsNullOrWhiteSpace($Script:DatabaseAppPassword) -and -not $Unattended.IsPresent) {
        Write-Host "  ----------------------------------------------------------------------" -ForegroundColor Yellow
        Write-Host "  SHOWN ONCE - RECORD THIS NOW" -ForegroundColor Yellow
        Write-Host "  ----------------------------------------------------------------------" -ForegroundColor Yellow
        Write-Host ("    Database role       {0}" -f $Config.database.username)
        Write-Host ("    Database password   {0}" -f $Script:DatabaseAppPassword) -ForegroundColor Yellow
        Write-Host "    The installer generated this password and stored it in the DPAPI secret"
        Write-Host "    store above. It is not written to any log or file in clear text and will"
        Write-Host "    not be shown again. Record it in your database and backup runbooks."
        Write-Host "  ----------------------------------------------------------------------" -ForegroundColor Yellow
        Write-Host ""
    }
}

# --------------------------------------------------------------------------------------------
# Rollback
# --------------------------------------------------------------------------------------------

function Invoke-Rollback {
    Write-Step "Rolling back to the previous build"
    Import-Module WebAdministration -ErrorAction Stop
    $state = Get-DeployState
    if ([string]::IsNullOrWhiteSpace($state.previousSha)) { throw "No previous build is recorded in $Script:StatePath." }
    $target = Join-Path $Script:Layout.AppRoot $state.previousSha
    if (-not (Test-Path -LiteralPath $target)) { throw "The previous build directory is gone: $target" }

    Write-Host "    WARN Database migrations are forward-only and are NOT reversed by a rollback." -ForegroundColor Yellow
    $rolledBackFrom = $state.activeSha
    Set-ActiveAppPath -Path $target
    $config = Get-Content -LiteralPath $Script:AppConfigPath -Raw | ConvertFrom-Json
    Test-Health -Config $config

    $state.activeSha = $state.previousSha
    $state.previousSha = $rolledBackFrom
    Save-DeployState -State $state
    Write-Ok "Rolled back to $($state.activeSha) (was $rolledBackFrom)"
}

# ==========================================================================================
# Main
# ==========================================================================================

Write-Host ""
Write-Host "ITAdmin deploy" -ForegroundColor White
Write-Host "==============" -ForegroundColor White

try {
    if ($Rollback.IsPresent) { Invoke-Rollback; exit 0 }

    Test-Preflight
    Import-Module WebAdministration -ErrorAction Stop

    if ($ConfigureHttps.IsPresent) {
        $config = Resolve-AppConfig -RequireExisting
        Set-HttpsBinding -Config $config
        Save-AppConfig -Config $config
        Write-Ok "HTTPS configuration updated."
        exit 0
    }

    $state = Get-DeployState
    $firstRun = [string]::IsNullOrWhiteSpace($state.activeSha) -or -not (Test-Path -LiteralPath $Script:AppConfigPath)

    $config = Resolve-AppConfig
    New-MachineDirectories
    Register-MachineLayout
    Save-AppConfig -Config $config

    if ($WhatIfPreflightOnly.IsPresent) {
        Write-Ok "Preflight and configuration succeeded. No build or deployment was performed."
        exit 0
    }

    $source = Sync-Source
    $appPath = Join-Path $Script:Layout.AppRoot $source.Sha
    $agentPath = Join-Path $Script:Layout.HostAgentRoot $source.Sha

    if (-not $SkipBuild.IsPresent) { Invoke-Build -Sha $source.Sha }
    elseif (-not (Test-Path -LiteralPath $appPath)) { throw "-SkipBuild was set but no build exists for $($source.Sha)." }

    $appExe = Join-Path $appPath "ITAdmin.Api.exe"
    if (-not (Test-Path -LiteralPath $appExe)) { throw "Build output is missing $appExe." }

    if ($firstRun -or $ProvisionDatabase.IsPresent) {
        Invoke-DatabaseProvisioning -Config $config -AppExe $appExe
    }
    $connectionString = Get-ConnectionString -Config $config
    Set-RuntimeConfiguration -Config $config -ConnectionString $connectionString
    Invoke-DatabaseMigration -AppExe $appExe

    if ($firstRun) { Invoke-DirectoryBootstrap -Config $config -AppExe $appExe }

    $previousAppPath = Get-ActiveAppPath
    Set-IisSite -Config $config -AppPath $appPath
    try {
        Test-Health -Config $config -RequireSetupComplete:$firstRun
    }
    catch {
        if (-not [string]::IsNullOrWhiteSpace($previousAppPath) -and (Test-Path -LiteralPath $previousAppPath) -and $previousAppPath -ne $appPath) {
            Write-Fail "Health check failed; reverting IIS to the previous build at $previousAppPath."
            Set-ActiveAppPath -Path $previousAppPath
        }
        throw
    }

    Write-HostAgentConfig -Config $config
    if (-not $NoHostAgentService.IsPresent -and (Test-Path -LiteralPath (Join-Path $agentPath "ITAdmin.HostAgent.exe"))) {
        Install-HostAgentService -AgentDirectory $agentPath
    }
    elseif ($NoHostAgentService.IsPresent) {
        Write-Detail "Host Agent service swap deferred to the Update Coordinator (-NoHostAgentService)."
    }

    $state.repositoryUrl = $RepositoryUrl
    $state.branch = $Branch
    if ($state.activeSha -ne $source.Sha) { $state.previousSha = $state.activeSha }
    $state.activeSha = $source.Sha
    $state.activeBuiltAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    $state.lastMigration = $Script:LastMigrationApplied
    Save-DeployState -State $state

    Write-DeploySummary -Config $config -Source $source -FirstRun $firstRun
    exit 0
}
catch {
    Write-Host ""
    Write-Fail $_.Exception.Message
    Write-Host ""
    Write-Host "The deployment did not complete. The previously active build (if any) was left in place." -ForegroundColor Yellow
    exit 1
}
