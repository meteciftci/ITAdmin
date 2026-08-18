#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Canonical repository-driven entrypoint for installing ITAdmin on a Windows server.

.DESCRIPTION
    Run this from a clone of the ITAdmin repository. It is the only command an operator needs after
    the one-time preparation described below.

    OPERATOR PREPARATION (once per server, unavoidable)
      1. Install Git for Windows.
      2. Create an SSH key pair for this server.
      3. Add the PUBLIC key to the ITAdmin repository as a Deploy Key, with write access OFF.
      4. Verify the Git host key fingerprint against the host's published value, then record it.

    THEN
      Clone the repository with the deploy-key-specific command in docs/first-install.md.
      cd C:\ITAdmin-bootstrap
      .\scripts\install\Bootstrap-ITAdmin.ps1

    WHAT THIS DOES, AND WHY

    The clone of main is bootstrap TRANSPORT ONLY. Nothing from a mutable branch is ever installed:
    a branch tip can change between the moment it is read and the moment it is used, which is not a
    property a production release may have.

    Release authority is an ANNOTATED stable SemVer tag. This script asks the remote which tags
    exist, keeps only annotated ones (a lightweight tag is a force-movable pointer with no tagger
    and no object of its own), keeps only stable ones on the stable channel, takes the highest, and
    records the commit that tag peels to.

    The application itself is delivered prebuilt. A customer's IIS server has no .NET SDK, no Node,
    and no EF tooling, and should never need them. The payload for each release lives on its own
    Git distribution ref (refs/itadmin/dist/<version>) as an orphan commit, so this script fetches
    exactly one ref at depth 1 and receives exactly one tree - no source history, no other releases.
    Before anything is staged, the payload's recorded version and source commit must match the tag
    that was resolved; a mismatch fails the run.

    Runtime prerequisite installers do not travel inside the distribution. Before downloading the
    application payload, the release-matched installer provisions IIS features, detects the .NET 10
    Hosting Bundle, and, when it is missing, shows Microsoft's official download page and waits for
    the operator to install it. ITAdmin never downloads or executes third-party prerequisites.

    Re-running on a partially installed host is safe: it re-enters the existing installation state
    machine, which resumes, repairs, or reports rather than wiping anything.

.PARAMETER DeployKeyPath
    Private half of the deploy key. Defaults to discovery from the current SSH configuration.

.PARAMETER Version
    Install a specific release instead of the newest. Must still be an annotated tag on the channel.

.PARAMETER Channel
    stable (default) or preview. Preview accepts pre-release tags and is for pilot hosts only.

.PARAMETER PrerequisitesOnly
    Provision IIS features, wait for manual Hosting Bundle installation when needed, then stop
    without downloading or installing the application.

.PARAMETER WhatIfPreflightOnly
    Resolve the release and validate everything, then stop without changing the machine.

.EXAMPLE
    .\scripts\install\Bootstrap-ITAdmin.ps1

.EXAMPLE
    .\scripts\install\Bootstrap-ITAdmin.ps1 -Version 2.1.0 -DeployKeyPath C:\ProgramData\ITAdmin\keys\deploy_key
#>
[CmdletBinding()]
param(
    [string]$RepositoryUrl,
    [string]$DeployKeyPath,
    [string]$Version,
    [ValidateSet("stable", "preview")]
    [string]$Channel = "stable",

    [string]$ProgramFilesRoot = "$env:ProgramFiles\ITAdmin",
    [string]$ProgramDataRoot = "$env:ProgramData\ITAdmin",

    [string]$SiteName = "ITAdmin",
    [string]$AppPoolName = "ITAdmin",

    [switch]$PrerequisitesOnly,
    [switch]$WhatIfPreflightOnly
)

$ErrorActionPreference = "Stop"

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
# Git and SSH capability
# --------------------------------------------------------------------------------------------

function Assert-GitAvailable {
    <#
        Git for Windows is an operator prerequisite, not something ITAdmin installs. Bootstrapping
        the tool that fetches the installer with the installer would be circular, and installing a
        general-purpose developer tool without being asked is not a decision this script should make
        on somebody's server.
    #>
    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($null -eq $git) {
        throw "Git was not found on PATH. Install Git for Windows (https://git-scm.com/download/win), " +
              "open a new elevated PowerShell session, and re-run this script."
    }

    $versionText = (& git --version 2>&1 | Out-String).Trim()
    Write-Detail "Git: $versionText"

    $ssh = Get-Command ssh -ErrorAction SilentlyContinue
    if ($null -eq $ssh) {
        throw "ssh was not found on PATH. Install Git for Windows with its OpenSSH client, or enable " +
              "the Windows OpenSSH Client optional feature, then re-run this script."
    }

    Write-Detail "ssh:  $($ssh.Source)"
}

function Resolve-RepositoryUrl {
    <#
        Discovered from the clone this script is running out of. Hard-coding an owner/name would
        make the product wrong for anyone who forks or mirrors it, and would silently disagree with
        the remote the operator actually cloned and keyed.
    #>
    if (-not [string]::IsNullOrWhiteSpace($RepositoryUrl)) {
        return $RepositoryUrl.Trim()
    }

    $repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $discovered = (& git -C $repositoryRoot remote get-url origin 2>&1 | Out-String).Trim()

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($discovered)) {
        throw "Could not determine the ITAdmin repository URL. Run this script from a clone of the " +
              "ITAdmin repository, or pass -RepositoryUrl explicitly."
    }

    if ($discovered -notmatch '^(git@|ssh://)') {
        throw "The repository origin '$discovered' is not an SSH URL. Deploy-key access requires SSH; " +
              "re-clone using the repository's SSH URL (through the ITAdmin SSH alias described in the " +
              "deployment guide)."
    }

    return $discovered
}

function Resolve-DeployKeyPath {
    <#
        The private key stays where the operator put it; this only locates it. Candidates are the
        conventional locations an administrator would have used when preparing the server.
    #>
    if (-not [string]::IsNullOrWhiteSpace($DeployKeyPath)) {
        if (-not (Test-Path -LiteralPath $DeployKeyPath)) {
            throw "Deploy key not found: $DeployKeyPath"
        }
        return (Resolve-Path -LiteralPath $DeployKeyPath).Path
    }

    $candidates = @(
        (Join-Path $ProgramDataRoot "keys\deploy_key"),
        (Join-Path $env:USERPROFILE ".ssh\itadmin_deploy"),
        (Join-Path $env:USERPROFILE ".ssh\id_ed25519"),
        (Join-Path $env:USERPROFILE ".ssh\id_rsa")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            Write-Detail "Using deploy key: $candidate"
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "No SSH deploy key was found. Create one under " +
          "`"$ProgramDataRoot\keys\deploy_key`", add the .pub half to the ITAdmin repository as a " +
          "read-only Deploy Key, then re-run with -DeployKeyPath."
}

function Get-GitSshCommand {
    <#
        Mirrors ITAdmin.Deployment.RepositoryAccessContract.BuildSshCommand (drift-tested).

        Every option is load-bearing:
          IdentitiesOnly       stops SSH silently offering an agent key or a default user key
                               instead of the deploy key, which would make the installation depend
                               on whoever happened to run it.
          BatchMode            stops any prompt hanging an unattended run or a service.
          StrictHostKeyChecking  the first connection is the one moment where accepting whatever
                               answers is genuinely dangerous, so it is never relaxed.
          UserKnownHostsFile   once the machine store exists, host trust comes from there rather
                               than from an administrator's profile, which LocalSystem cannot read
                               and which may be deleted with the account.
          GlobalKnownHostsFile a system-wide file must not silently widen what is trusted.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$KeyPath,
        [string]$KnownHostsPath
    )

    $command = "ssh -i `"$KeyPath`" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes"

    if (-not [string]::IsNullOrWhiteSpace($KnownHostsPath) -and (Test-Path -LiteralPath $KnownHostsPath)) {
        $command += " -o UserKnownHostsFile=`"$KnownHostsPath`" -o GlobalKnownHostsFile=/dev/null"
    }

    return $command
}

function Get-RepositorySshHost {
    <#
        The host from a Git SSH remote. Handles both forms Git accepts:
        git@host:owner/repo.git (scp-like) and ssh://git@host[:port]/owner/repo.git.
    #>
    param([Parameter(Mandatory = $true)][string]$Repository)

    if ($Repository -match '^ssh://(?:[^@/]+@)?([^:/]+)') { return $Matches[1] }
    if ($Repository -match '^(?:[^@/]+@)?([A-Za-z0-9._-]+):(?!//)') { return $Matches[1] }

    throw "Could not determine the SSH host from repository URL '$Repository'."
}

function Get-RepositoryKnownHostLookup {
    <#
        OpenSSH stores hosts reached on a non-default port as [host]:port in known_hosts.
        Repository URLs using ssh:// can carry that port, so preserve it for ssh-keygen -F.
    #>
    param([Parameter(Mandatory = $true)][string]$Repository)

    $sshHost = Get-RepositorySshHost -Repository $Repository
    if ($Repository -match '^ssh://') {
        try {
            $uri = [Uri]$Repository
            if (-not $uri.IsDefaultPort -and $uri.Port -ne 22) {
                return "[$sshHost]:$($uri.Port)"
            }
        }
        catch {
            throw "Could not determine the SSH endpoint from repository URL '$Repository'."
        }
    }

    return $sshHost
}

function Resolve-SshHostAlias {
    <#
        Turns an alias-form remote back into one that names the real host.

        The documented clone goes through an ITAdmin-specific SSH alias (github-itadmin) so the
        deploy key applies to ITAdmin's clone and does not capture every other GitHub operation the
        administrator performs. That alias only resolves inside the profile holding the SSH config
        entry - so the machine configuration the Host Agent reads must name the real host, or
        repository access breaks the moment that profile is removed.

        The real host is read from the operator's own SSH config, so ITAdmin never has to assume
        which Git host the alias points at.

        Mirrors ITAdmin.Deployment.RepositoryAccessContract.ResolveAliasToRealHost (drift-tested).
    #>
    param([Parameter(Mandatory = $true)][string]$Repository)

    $alias = "github-itadmin"
    $sshHost = Get-RepositorySshHost -Repository $Repository

    if ($sshHost -ne $alias) {
        return $Repository
    }

    # `ssh -G <alias>` asks OpenSSH to resolve the effective configuration, which is the most
    # reliable way to learn what HostName the operator actually configured.
    $realHost = $null
    try {
        foreach ($line in (& ssh -G $alias 2>$null)) {
            if ("$line" -match '^hostname\s+(\S+)$') { $realHost = $Matches[1]; break }
        }
    }
    catch {
        Write-Verbose "ssh -G could not resolve '$alias': $($_.Exception.Message)"
    }

    if ([string]::IsNullOrWhiteSpace($realHost) -or $realHost -eq $alias) {
        throw "The repository remote uses the ITAdmin SSH alias '$alias', but no HostName could be " +
              "resolved for it. Add the ITAdmin SSH config entry described in the deployment guide " +
              "(Host $alias / HostName <your Git host>), then re-run."
    }

    $resolved = $Repository -replace [regex]::Escape($alias), $realHost
    Write-Detail "Resolved SSH alias '$alias' to real host '$realHost' for machine configuration."
    return $resolved
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$SshCommand,
        [switch]$ShowOutput
    )

    $previousSsh = $env:GIT_SSH_COMMAND
    $previousPrompt = $env:GIT_TERMINAL_PROMPT
    try {
        if (-not [string]::IsNullOrWhiteSpace($SshCommand)) {
            $env:GIT_SSH_COMMAND = $SshCommand
        }
        $env:GIT_TERMINAL_PROMPT = "0"

        if ($ShowOutput.IsPresent) {
            $lines = New-Object System.Collections.Generic.List[string]
            if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
                & git @Arguments 2>&1 | ForEach-Object {
                    $line = "$_"
                    $lines.Add($line)
                    Write-Host "    $line"
                }
            }
            else {
                & git -C $WorkingDirectory @Arguments 2>&1 | ForEach-Object {
                    $line = "$_"
                    $lines.Add($line)
                    Write-Host "    $line"
                }
            }
            $output = $lines.ToArray()
        }
        elseif ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
            $output = & git @Arguments 2>&1
        }
        else {
            $output = & git -C $WorkingDirectory @Arguments 2>&1
        }

        $exitCode = $LASTEXITCODE
        return [pscustomobject]@{
            ExitCode = $exitCode
            Output   = @($output | ForEach-Object { "$_" })
        }
    }
    finally {
        if ($null -ne $previousSsh) { $env:GIT_SSH_COMMAND = $previousSsh }
        else { Remove-Item Env:\GIT_SSH_COMMAND -ErrorAction SilentlyContinue }
        if ($null -ne $previousPrompt) { $env:GIT_TERMINAL_PROMPT = $previousPrompt }
        else { Remove-Item Env:\GIT_TERMINAL_PROMPT -ErrorAction SilentlyContinue }
    }
}

function Get-RepositoryAccessDiagnosis {
    <#
        These three failures look identical in a raw stderr dump and have completely different
        fixes, so they are separated before the operator ever sees them.
    #>
    param([Parameter(Mandatory = $true)][string[]]$Output)

    $text = ($Output -join "`n")

    if ($text -match '(?i)permission denied|publickey') {
        return "The repository refused the deploy key. Confirm the PUBLIC half of this key is listed " +
               "as a Deploy Key on the ITAdmin repository and has not been revoked."
    }
    if ($text -match '(?i)could not resolve hostname|connection timed out|network is unreachable') {
        return "The repository host could not be reached. Check name resolution and outbound SSH " +
               "connectivity from this server (GitHub supports ssh.github.com on port 443 when " +
               "port 22 is blocked)."
    }
    if ($text -match '(?i)host key verification failed') {
        return "The repository host key is not trusted by this machine. Connect once interactively " +
               "(ssh -T git@<host>) to record it, then re-run."
    }

    return "Repository access failed. Git reported:`n" + $text
}

# --------------------------------------------------------------------------------------------
# Release resolution
#
# Mirrors ITAdmin.Deployment.ReleaseTagResolver, which is the unit-tested definition of these
# rules. Kept aligned by a drift test.
# --------------------------------------------------------------------------------------------

function Resolve-ReleaseTag {
    <#
        `git ls-remote --tags` prints one row per ref, and for an ANNOTATED tag it prints a second
        row suffixed with ^{} carrying the commit the tag object points at. That second row is both
        the proof the tag is annotated and the source of the commit we pin: the unpeeled row of an
        annotated tag names the tag object, not the commit, so installing from it would pin the
        wrong object entirely.
    #>
    param(
        # AllowEmptyCollection: a repository with no tags yet advertises nothing, and that must
        # produce the "publish an annotated release tag first" diagnosis rather than a parameter
        # binding error.
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$LsRemoteLines,

        [Parameter(Mandatory = $true)][string]$ReleaseChannel,
        [string]$ExactVersion
    )

    $unpeeled = @{}
    $peeled = @{}

    foreach ($line in $LsRemoteLines) {
        if ($line -notmatch '^([0-9a-fA-F]{40,64})\s+refs/tags/(.+)$') { continue }

        $objectId = $Matches[1].ToLowerInvariant()
        $name = $Matches[2]

        if ($name.EndsWith('^{}')) {
            $peeled[$name.Substring(0, $name.Length - 3)] = $objectId
        }
        else {
            $unpeeled[$name] = $objectId
        }
    }

    $candidates = New-Object System.Collections.Generic.List[object]
    $rejected = New-Object System.Collections.Generic.List[string]

    foreach ($name in ($unpeeled.Keys | Sort-Object)) {
        $normalized = $name
        if ($normalized -match '^[vV]') { $normalized = $normalized.Substring(1) }

        $preRelease = $null
        $numeric = $normalized
        $hyphen = $normalized.IndexOf('-')
        if ($hyphen -ge 0) {
            $preRelease = $normalized.Substring($hyphen + 1)
            $numeric = $normalized.Substring(0, $hyphen)
        }

        $parsed = $null
        if ($numeric -notmatch '^\d+\.\d+\.\d+$' -or -not [version]::TryParse($numeric, [ref]$parsed)) {
            $rejected.Add("'$name' is not a MAJOR.MINOR.PATCH release tag.")
            continue
        }

        if (-not $peeled.ContainsKey($name)) {
            $rejected.Add("'$name' is a lightweight tag. Production releases must be annotated (git tag -a), " +
                          "which carry a tagger and a stable peeled commit.")
            continue
        }

        if ($null -ne $preRelease -and $ReleaseChannel -eq "stable") {
            $rejected.Add("'$name' is a pre-release and this host is on the stable channel.")
            continue
        }

        $candidates.Add([pscustomobject]@{
            TagName      = $name
            VersionText  = $normalized
            Version      = $parsed
            IsPreRelease = ($null -ne $preRelease)
            SourceCommit = $peeled[$name]
        })
    }

    $selected = $null
    if (-not [string]::IsNullOrWhiteSpace($ExactVersion)) {
        $selected = @($candidates | Where-Object { $_.VersionText -eq $ExactVersion }) | Select-Object -First 1
    }
    else {
        # Stable outranks a pre-release of the same number, which only matters on the preview channel.
        $selected = @($candidates |
            Sort-Object -Property @{ Expression = "Version"; Descending = $true },
                                  @{ Expression = "IsPreRelease"; Descending = $false } ) |
            Select-Object -First 1
    }

    # ToArray() rather than @(...): converting an empty generic List to an array inside a
    # [pscustomobject] literal throws "Argument types do not match" in PowerShell, which would turn
    # the perfectly ordinary "this repository has no release tags yet" case into an unreadable
    # engine error instead of the diagnosis below.
    return [pscustomobject]@{
        Selected   = $selected
        Candidates = $candidates.ToArray()
        Rejected   = $rejected.ToArray()
    }
}

# --------------------------------------------------------------------------------------------
# Machine persistence
# --------------------------------------------------------------------------------------------

function Install-DeployKey {
    <#
        The deploy key becomes machine-owned: it is copied under ProgramData with inheritance
        removed and access granted to SYSTEM and Administrators only.

        It is deliberately NOT placed in the web root, the release directory, or anything the
        application pool can read. Repository access is a deployment-authority capability; an
        application that could read this key could clone the product's source, and a flaw in
        request handling would hand that to whoever found it.

        An existing key is left alone. A re-run must not silently replace working repository access.
    #>
    param([Parameter(Mandatory = $true)][string]$SourceKeyPath)

    $keyDirectory = Join-Path $ProgramDataRoot "keys"
    $destination = Join-Path $keyDirectory "deploy_key"

    if (-not (Test-Path -LiteralPath $keyDirectory)) {
        New-Item -ItemType Directory -Path $keyDirectory -Force | Out-Null
    }

    if (Test-Path -LiteralPath $destination) {
        Write-Detail "Machine deploy key already present; left unchanged."
    }
    else {
        Copy-Item -LiteralPath $SourceKeyPath -Destination $destination -Force
        Write-Detail "Deploy key installed to $destination"
    }

    & icacls $keyDirectory /inheritance:r | Out-Null
    & icacls $keyDirectory /grant:r "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null
    & icacls $destination /inheritance:r | Out-Null
    & icacls $destination /grant:r "SYSTEM:F" "Administrators:F" | Out-Null

    Write-Ok "Repository access key is machine-owned (SYSTEM + Administrators only)"
    return $destination
}

function Install-MachineKnownHosts {
    <#
        Copies the repository host key the OPERATOR already verified into a machine-owned store.

        Trust is derived, never invented. This does not run ssh-keyscan and record whatever answers -
        that would be exactly the "accept whatever host key appears" behaviour the preparation steps
        exist to avoid. It extracts the entry from the administrator's own known_hosts, which they
        populated during preparation after comparing the fingerprint against the value the Git host
        publishes. If they did not, this fails and says so.

        Persisting it matters because the Host Agent runs as LocalSystem: it cannot read an
        administrator's profile, and that profile may be removed when the account is. After this the
        machine's Git access survives the administrator who set it up.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$KeyDirectory
    )

    $sshHost = Get-RepositorySshHost -Repository $Repository
    $knownHostLookup = Get-RepositoryKnownHostLookup -Repository $Repository
    $destination = Join-Path $KeyDirectory "known_hosts"

    if (Test-Path -LiteralPath $destination) {
        $existing = Get-Content -LiteralPath $destination -Raw -ErrorAction SilentlyContinue
        if (-not [string]::IsNullOrWhiteSpace($existing)) {
            Write-Detail "Machine known_hosts already present; left unchanged."
            & icacls $destination /inheritance:r | Out-Null
            & icacls $destination /grant:r "SYSTEM:F" "Administrators:F" | Out-Null
            return $destination
        }
    }

    $operatorKnownHosts = Join-Path $env:USERPROFILE ".ssh\known_hosts"
    if (-not (Test-Path -LiteralPath $operatorKnownHosts)) {
        throw "No known_hosts file was found at $operatorKnownHosts. Complete the one-time host " +
              "verification step from the ITAdmin deployment guide (compare the host key fingerprint " +
              "against the one your Git host publishes, then record it) and re-run."
    }

    # ssh-keygen -F resolves both plain and hashed entries, which a text search cannot.
    $entries = & ssh-keygen -F $knownHostLookup -f $operatorKnownHosts 2>$null
    $entryLines = @($entries | Where-Object { "$_" -notmatch '^\s*#' -and -not [string]::IsNullOrWhiteSpace("$_") })

    if ($entryLines.Count -eq 0) {
        throw "No verified host key for '$knownHostLookup' was found in $operatorKnownHosts. Complete the " +
              "one-time host verification step from the ITAdmin deployment guide and re-run. " +
              "ITAdmin will not record a host key it has not seen you verify."
    }

    Set-Content -LiteralPath $destination -Value $entryLines -Encoding ASCII

    & icacls $destination /inheritance:r | Out-Null
    & icacls $destination /grant:r "SYSTEM:F" "Administrators:F" | Out-Null

    Write-Ok "Verified host key for '$sshHost' persisted for the machine ($($entryLines.Count) entry/entries)"
    return $destination
}

function Install-DeploymentTooling {
    <#
        Persists the installer and deployment scripts from the EXACT release tag being installed,
        not from the mutable clone this script is running out of.

        That matters for updates: when the Host Agent later applies release N, it must run release
        N's installer, whose staging and activation steps match release N's payload. Taking the
        tooling from whatever main happens to contain would couple every past release to today's
        script.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$TagName,
        [Parameter(Mandatory = $true)][string]$SshCommand,
        [Parameter(Mandatory = $true)][string]$ExpectedSourceCommit
    )

    $toolingRoot = Join-Path $ProgramFilesRoot "tooling"
    $scratch = Join-Path ([System.IO.Path]::GetTempPath()) ("itadmin-tooling-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $scratch -Force | Out-Null

    try {
        foreach ($step in @(
                @("init", "--quiet"),
                @("remote", "add", "origin", $Repository),
                @("fetch", "--depth", "1", "--quiet", "origin", "refs/tags/$TagName"),
                @("checkout", "--quiet", "FETCH_HEAD", "--", "scripts/install"))) {
            $result = Invoke-Git -Arguments $step -WorkingDirectory $scratch -SshCommand $SshCommand
            if ($result.ExitCode -ne 0) {
                throw (Get-RepositoryAccessDiagnosis -Output $result.Output)
            }
        }

        $identity = Invoke-Git -Arguments @("rev-parse", "FETCH_HEAD^{commit}") `
            -WorkingDirectory $scratch -SshCommand $SshCommand
        $actualCommit = $identity.Output |
            Where-Object { "$_" -match '^[0-9a-fA-F]{40,64}$' } |
            Select-Object -First 1
        if ($identity.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace("$actualCommit") -or
            $actualCommit.ToString().ToLowerInvariant() -ne $ExpectedSourceCommit.ToLowerInvariant()) {
            throw "Deployment tooling source commit does not match the resolved release tag."
        }

        if (-not (Test-Path -LiteralPath $toolingRoot)) {
            New-Item -ItemType Directory -Path $toolingRoot -Force | Out-Null
        }

        $installTooling = Join-Path $toolingRoot "install"
        if (Test-Path -LiteralPath $installTooling) {
            Remove-Item -LiteralPath $installTooling -Recurse -Force
        }

        Copy-Item -LiteralPath (Join-Path $scratch "scripts\install") -Destination $installTooling -Recurse -Force

        # Deployment tooling is administrator-owned. The application pool must never be able to
        # modify a script that the privileged Host Agent later executes.
        $identity = "IIS AppPool\$AppPoolName"
        & icacls $toolingRoot /deny "${identity}:(OI)(CI)(W)" | Out-Null

        Write-Ok "Deployment tooling for $TagName persisted to $installTooling"
        return (Join-Path $installTooling "Install-ITAdmin.ps1")
    }
    finally {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-ReleasePayload {
    <#
        Fetches the release's distribution ref at depth 1. One ref, one orphan commit, one tree:
        the server downloads the release it is installing and nothing else, no matter how long the
        repository's history becomes or how many releases precede this one.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$VersionText,
        [Parameter(Mandatory = $true)][string]$SshCommand
    )

    $distributionRef = "refs/itadmin/dist/$VersionText"
    $destination = Join-Path ([System.IO.Path]::GetTempPath()) ("itadmin-release-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    Write-Detail "Fetching $distributionRef (depth 1)"

    foreach ($step in @(
            @("init", "--quiet"),
            @("remote", "add", "origin", $Repository),
            @("fetch", "--depth", "1", "--progress", "origin", $distributionRef),
            @("checkout", "--quiet", "FETCH_HEAD"))) {
        $result = Invoke-Git -Arguments $step -WorkingDirectory $destination -SshCommand $SshCommand `
            -ShowOutput:($step[0] -eq "fetch")
        if ($result.ExitCode -ne 0) {
            $diagnosis = Get-RepositoryAccessDiagnosis -Output $result.Output
            Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction SilentlyContinue
            throw "Could not obtain the release payload for $VersionText. $diagnosis`n" +
                  "The release tag exists but its prebuilt payload ($distributionRef) was not found or " +
                  "could not be fetched. Publishing the payload is a release-pipeline step; the tag alone " +
                  "is not enough to install."
        }
    }

    Write-Ok "Release payload acquired"
    return $destination
}

function Save-HostAgentSettings {
    <#
        Records where the repository is, which channel this host follows, and where the deploy key
        lives - but never the key itself. A key inlined into configuration would turn every backup
        of ProgramData into a key leak.

        In-app updates are enabled only after this bootstrap has proven machine-owned repository
        access. Application permissions still gate who can request one.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$KeyDirectory
    )

    $configRoot = Join-Path $ProgramDataRoot "config"
    if (-not (Test-Path -LiteralPath $configRoot)) {
        New-Item -ItemType Directory -Path $configRoot -Force | Out-Null
    }

    $settingsPath = Join-Path $configRoot "hostagent.json"

    $existingUpdatesEnabled = $false
    if (Test-Path -LiteralPath $settingsPath) {
        try {
            $existing = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
            $existingUpdatesEnabled = [bool]$existing.updatesEnabled
        }
        catch {
            Write-Detail "Existing host agent settings were unreadable and will be rewritten."
        }
    }

    $settings = [pscustomobject]@{
        schemaVersion      = 1
        repositoryUrl      = $Repository
        channel            = $(if ($Channel -eq "preview") { "Preview" } else { "Stable" })
        deployKeyDirectory = $KeyDirectory
        programFilesRoot   = $ProgramFilesRoot
        programDataRoot    = $ProgramDataRoot
        siteName           = $SiteName
        appPoolName        = $AppPoolName
        # Repository access was proven above with the machine-owned key and host trust. A signed-in
        # administrator still needs System.Updates.Manage before the web application can request an
        # update, so no second server-side enablement switch is required.
        updatesEnabled     = $true
    }

    $settings | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    $registryPath = "HKLM:\SOFTWARE\ITAdmin"
    New-Item -Path $registryPath -Force | Out-Null
    New-ItemProperty -Path $registryPath -Name "ProgramDataRoot" -Value $ProgramDataRoot `
        -PropertyType String -Force | Out-Null
    Write-Ok "Host Agent configuration written to $settingsPath"
}

function Get-VerifiedReleaseInstaller {
    param(
        [Parameter(Mandatory = $true)][string]$ReleaseDirectory,
        [Parameter(Mandatory = $true)][string]$VersionText,
        [Parameter(Mandatory = $true)][string]$SourceCommit
    )

    $manifestPath = Join-Path $ReleaseDirectory "release.manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "The distribution has no release manifest; its installer will not be executed."
    }
    try { $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json }
    catch { throw "The distribution release manifest is not valid JSON; its installer will not be executed." }

    $commitMatches = "$($manifest.source.commit)" -eq $SourceCommit -or
        $SourceCommit.StartsWith("$($manifest.source.commit)", [StringComparison]::OrdinalIgnoreCase) -or
        "$($manifest.source.commit)".StartsWith($SourceCommit, [StringComparison]::OrdinalIgnoreCase)
    if ("$($manifest.source.version)" -ne $VersionText -or
        "$($manifest.distribution.version)" -ne $VersionText -or
        -not $commitMatches) {
        throw "The distribution identity does not match the resolved annotated release."
    }

    $component = $manifest.components.PSObject.Properties["deployment-tooling"]
    if ($null -eq $component -or "$($component.Value.kind)" -ne "DeploymentTooling") {
        throw "The distribution has no declared deployment-tooling component."
    }
    $root = Join-Path $ReleaseDirectory "deployment-tooling"
    $expected = @{}
    foreach ($file in $component.Value.integrity.files.PSObject.Properties) {
        if ($file.Name -match '(^/)|(\\)|(:)|(^\.\.)|(/\.\./)|(/\.\.$)') {
            throw "The deployment-tooling manifest contains an unsafe path."
        }
        $expected[$file.Name] = "$($file.Value)"
        $path = Join-Path $root ($file.Name -replace '/', '\')
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected[$file.Name]) {
            throw "The deployment-tooling component failed SHA-256 verification."
        }
    }
    foreach ($actual in (Get-ChildItem -LiteralPath $root -Recurse -File -Force)) {
        $relative = $actual.FullName.Substring($root.Length).TrimStart('\') -replace '\\', '/'
        if (-not $expected.ContainsKey($relative)) {
            throw "The deployment-tooling component contains an undeclared file."
        }
    }

    $installer = Join-Path $root "Install-ITAdmin.ps1"
    if (-not $expected.ContainsKey("Install-ITAdmin.ps1") -or -not (Test-Path -LiteralPath $installer)) {
        throw "The verified deployment-tooling component has no installer."
    }
    Write-Ok "Release-matched deployment tooling verified"
    return $installer
}

# --------------------------------------------------------------------------------------------
# Main
# --------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "ITAdmin bootstrap" -ForegroundColor White
Write-Host "=================" -ForegroundColor White

try {
    Write-Step "Verifying Git and SSH capability"
    Assert-GitAvailable

    $repository = Resolve-RepositoryUrl
    Write-Detail "Repository (as cloned): $repository"

    # Everything persisted for the machine must name the real host. The alias is an operator
    # convenience that exists only inside their profile.
    $machineRepository = Resolve-SshHostAlias -Repository $repository
    if ($machineRepository -ne $repository) {
        Write-Detail "Repository (machine): $machineRepository"
    }

    $keyPath = Resolve-DeployKeyPath
    # Before persistence, host trust necessarily comes from the operator's profile - that is where
    # they verified it. Everything after Install-MachineKnownHosts uses the machine store.
    $sshCommand = Get-GitSshCommand -KeyPath $keyPath

    Write-Step "Verifying repository access with the deploy key"
    $lsRemote = Invoke-Git -Arguments @("ls-remote", "--tags", $repository) -SshCommand $sshCommand
    if ($lsRemote.ExitCode -ne 0) {
        throw (Get-RepositoryAccessDiagnosis -Output $lsRemote.Output)
    }
    Write-Ok "Repository access verified (read-only deploy key)"

    Write-Step "Resolving the release to install"
    $resolution = Resolve-ReleaseTag -LsRemoteLines $lsRemote.Output -ReleaseChannel $Channel -ExactVersion $Version

    foreach ($rejection in $resolution.Rejected) {
        Write-Detail "rejected: $rejection"
    }

    if ($null -eq $resolution.Selected) {
        if ($resolution.Rejected.Count -eq 0) {
            throw "The repository advertises no release tags. Publish an annotated stable release tag " +
                  "(git tag -a v1.0.0 -m `"ITAdmin 1.0.0`") before installing."
        }
        if (-not [string]::IsNullOrWhiteSpace($Version)) {
            throw "Release $Version is not published as an annotated $Channel release tag."
        }
        throw "No usable release tag was found on the $Channel channel; $($resolution.Rejected.Count) tag(s) were rejected."
    }

    $release = $resolution.Selected
    Write-Ok "Release $($release.VersionText) (annotated tag $($release.TagName))"
    Write-Detail "Source commit: $($release.SourceCommit)"

    Write-Step "Persisting repository access for the machine"
    $installedKey = Install-DeployKey -SourceKeyPath $keyPath
    $keyDirectory = Split-Path -Parent $installedKey
    $knownHostsPath = Install-MachineKnownHosts -Repository $machineRepository -KeyDirectory $keyDirectory

    # From here on, every Git operation - including everything the Host Agent will do later - uses
    # the machine-owned key and the machine-owned host keys, not the operator's profile.
    $sshCommand = Get-GitSshCommand -KeyPath $installedKey -KnownHostsPath $knownHostsPath

    Save-HostAgentSettings -Repository $machineRepository -KeyDirectory $keyDirectory

    Write-Step "Checking server prerequisites before downloading the application payload"
    $prerequisiteInstaller = Install-DeploymentTooling -Repository $machineRepository `
        -TagName $release.TagName -SshCommand $sshCommand `
        -ExpectedSourceCommit $release.SourceCommit

    & $prerequisiteInstaller -PrerequisitesOnly -ProvisionPrerequisites `
        -ProgramFilesRoot $ProgramFilesRoot -ProgramDataRoot $ProgramDataRoot `
        -SiteName $SiteName -AppPoolName $AppPoolName
    if ($LASTEXITCODE -ne 0) {
        throw "Server prerequisite preparation did not complete successfully."
    }

    if ($PrerequisitesOnly.IsPresent) {
        Write-Ok "Prerequisites confirmed; application payload was not downloaded."
        exit 0
    }

    Write-Step "Acquiring the prebuilt Windows payload"
    $releaseDirectory = Get-ReleasePayload -Repository $machineRepository -VersionText $release.VersionText -SshCommand $sshCommand

    try {
        $installerPath = Get-VerifiedReleaseInstaller -ReleaseDirectory $releaseDirectory `
            -VersionText $release.VersionText -SourceCommit $release.SourceCommit

        Write-Step "Handing off to the installer"
        Write-Detail "Installer: $installerPath"

        $installerArguments = @(
            "-ReleaseDirectory", $releaseDirectory,
            "-ExpectedVersion", $release.VersionText,
            "-ExpectedSourceCommit", $release.SourceCommit,
            "-ProgramFilesRoot", $ProgramFilesRoot,
            "-ProgramDataRoot", $ProgramDataRoot,
            "-SiteName", $SiteName,
            "-AppPoolName", $AppPoolName
        )

        if ($WhatIfPreflightOnly.IsPresent) {
            $installerArguments += "-WhatIfPreflightOnly"
        }

        & $installerPath @installerArguments
        exit $LASTEXITCODE
    }
    finally {
        Remove-Item -LiteralPath $releaseDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
catch {
    Write-Host ""
    Write-Fail $_.Exception.Message
    Write-Host ""
    Write-Host "No application changes were made by the bootstrap." -ForegroundColor Yellow
    exit 1
}
