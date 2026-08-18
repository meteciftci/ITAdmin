#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the operator-facing ITAdmin Windows installation ZIP from a verified distribution tree.

.DESCRIPTION
    Runs on the release build machine, never on the production server. The distribution tree is
    already the closed, digest-declared output of dist-stage/dist-verify. This script wraps that tree
    with the production Setup and optional update-configuration entrypoints.

    Archive entries are sorted and every ZIP timestamp is pinned to the annotated tagger timestamp.
    Given identical input bytes and release identity, the resulting ZIP and SHA-256 are reproducible.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Timestamp,
    [Parameter(Mandatory = $true)][string]$DistributionRoot,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$SetupScript = (Join-Path $PSScriptRoot "..\install\Setup-ITAdmin.ps1"),
    [string]$UpdateConfigurationScript = (Join-Path $PSScriptRoot "..\install\Configure-ITAdminUpdates.ps1")
)

$ErrorActionPreference = "Stop"

function Resolve-RequiredFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$Description not found: $fullPath"
    }
    return $fullPath
}

function Resolve-RequiredDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "$Description not found: $fullPath"
    }
    return $fullPath
}

function New-DeterministicZip {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$DestinationPath,
        [Parameter(Mandatory = $true)][DateTimeOffset]$FixedTimestamp
    )

    Add-Type -AssemblyName System.IO.Compression

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $rootPath = (Resolve-Path -LiteralPath $SourceDirectory).Path
    $fileStream = [System.IO.File]::Open(
        $DestinationPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)

    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $fileStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $true)
        try {
            $files = Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File -Force |
                Sort-Object { $_.FullName.Substring($rootPath.Length) }

            foreach ($file in $files) {
                $relative = $file.FullName.Substring($rootPath.Length).TrimStart([char]'\', [char]'/') -replace '\\', '/'
                $entry = $archive.CreateEntry(
                    $relative,
                    [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $FixedTimestamp

                $entryStream = $entry.Open()
                $sourceStream = [System.IO.File]::OpenRead($file.FullName)
                try {
                    $sourceStream.CopyTo($entryStream)
                }
                finally {
                    $sourceStream.Dispose()
                    $entryStream.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $fileStream.Dispose()
    }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be a stable MAJOR.MINOR.PATCH value."
}

try {
    $fixedTimestamp = [DateTimeOffset]::Parse($Timestamp).ToUniversalTime()
}
catch {
    throw "Timestamp must be an ISO-8601 annotated tagger timestamp."
}

$distribution = Resolve-RequiredDirectory -Path $DistributionRoot -Description "Verified distribution tree"
$setup = Resolve-RequiredFile -Path $SetupScript -Description "Production setup script"
$updateConfiguration = Resolve-RequiredFile -Path $UpdateConfigurationScript -Description "Update configuration script"

foreach ($required in @(
        "release.manifest.json",
        "deployment-tooling\Install-ITAdmin.ps1")) {
    if (-not (Test-Path -LiteralPath (Join-Path $distribution $required) -PathType Leaf)) {
        throw "Verified distribution tree is incomplete: $required is missing."
    }
}

$output = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$zipName = "ITAdmin-$Version-windows.zip"
$zipPath = Join-Path $output $zipName
$shaPath = "$zipPath.sha256"

$packageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("itadmin-package-" + [guid]::NewGuid().ToString("N"))
$releaseRoot = Join-Path $packageRoot "release"
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

try {
    Copy-Item -LiteralPath $setup -Destination (Join-Path $packageRoot "Setup-ITAdmin.ps1") -Force
    Copy-Item -LiteralPath $updateConfiguration -Destination (Join-Path $packageRoot "Configure-ITAdminUpdates.ps1") -Force
    Copy-Item -Path (Join-Path $distribution "*") -Destination $releaseRoot -Recurse -Force

    @(
        "ITAdmin $Version - Windows installation package",
        "",
        "1. Extract this ZIP to a temporary folder on Windows Server 2022/2025.",
        "2. Open an elevated Windows PowerShell 5.1 session in that folder.",
        "3. Run: .\Setup-ITAdmin.ps1",
        "",
        "First installation is local/offline. Git and GitHub access are not required.",
        "Repository-backed in-app updates are optional and can be enabled after installation with",
        ".\Configure-ITAdminUpdates.ps1."
    ) | Set-Content -LiteralPath (Join-Path $packageRoot "README.txt") -Encoding UTF8

    New-DeterministicZip -SourceDirectory $packageRoot -DestinationPath $zipPath -FixedTimestamp $fixedTimestamp

    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $zipName" | Set-Content -LiteralPath $shaPath -Encoding ASCII

    $probe = Join-Path ([System.IO.Path]::GetTempPath()) ("itadmin-package-probe-" + [guid]::NewGuid().ToString("N"))
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $probe)
        foreach ($required in @(
                "Setup-ITAdmin.ps1",
                "Configure-ITAdminUpdates.ps1",
                "release\release.manifest.json",
                "release\deployment-tooling\Install-ITAdmin.ps1")) {
            if (-not (Test-Path -LiteralPath (Join-Path $probe $required))) {
                throw "Published package is missing $required."
            }
        }
    }
    finally {
        Remove-Item -LiteralPath $probe -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Built $zipPath"
    Write-Host "SHA-256 $hash"

    [pscustomobject]@{
        ZipPath = $zipPath
        Sha256Path = $shaPath
        ZipName = $zipName
        Sha256 = $hash
    }
}
finally {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
}
