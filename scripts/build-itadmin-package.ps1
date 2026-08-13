#Requires -Version 5.1
# DEPRECATED - superseded by scripts/release/build-release.zsh, which produces a versioned,
# integrity-stamped, environment-neutral release artifact. This script emits an unversioned
# zip with no manifest and no checksums, and is retained only until Installer v2 acceptance.


[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepositoryRoot,

    [Parameter()]
    [string]$OutputPackagePath,

    [Parameter()]
    [string]$OutputMigrationSqlPath,

    [Parameter()]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-ITAdminPackageMessage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host $Message
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$backendRoot = Join-Path $RepositoryRoot "backend"
$frontendRoot = Join-Path $RepositoryRoot "frontend"
$apiProject = Join-Path $backendRoot "src\ITAdmin.Api\ITAdmin.Api.csproj"
$persistenceProject = Join-Path $backendRoot "src\ITAdmin.Persistence\ITAdmin.Persistence.csproj"
$stagingRoot = Join-Path $RepositoryRoot "artifacts\package-staging"
$publishRoot = Join-Path $stagingRoot "publish"
$installScriptPath = Join-Path $RepositoryRoot "scripts\iis\install-itadmin-server.ps1"

if ([string]::IsNullOrWhiteSpace($OutputPackagePath)) {
    $OutputPackagePath = Join-Path $RepositoryRoot "artifacts\itadmin-package.zip"
}

if ([string]::IsNullOrWhiteSpace($OutputMigrationSqlPath)) {
    $OutputMigrationSqlPath = Join-Path $RepositoryRoot "artifacts\itadmin-migrations.sql"
}

if (-not (Test-Path -LiteralPath $apiProject)) {
    throw "API project not found: $apiProject"
}

if (-not (Test-Path -LiteralPath $frontendRoot)) {
    throw "Frontend directory not found: $frontendRoot"
}

Write-ITAdminPackageMessage -Message "== Building ITAdmin deployment package =="

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPackagePath) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputMigrationSqlPath) -Force | Out-Null

Write-ITAdminPackageMessage -Message "Publishing backend API..."
dotnet publish $apiProject -c $Configuration -o $publishRoot --no-self-contained
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-ITAdminPackageMessage -Message "Building frontend..."
Push-Location $frontendRoot
try {
    if (-not (Test-Path -LiteralPath "node_modules")) {
        npm ci
        if ($LASTEXITCODE -ne 0) {
            throw "npm ci failed with exit code $LASTEXITCODE."
        }
    }

    npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$frontendDist = Join-Path $frontendRoot "dist"
$wwwrootPath = Join-Path $publishRoot "wwwroot"
if (-not (Test-Path -LiteralPath $frontendDist)) {
    throw "Frontend dist directory not found: $frontendDist"
}

New-Item -ItemType Directory -Path $wwwrootPath -Force | Out-Null
Copy-Item -Path (Join-Path $frontendDist "*") -Destination $wwwrootPath -Recurse -Force

Write-ITAdminPackageMessage -Message "Creating idempotent SQL migration script..."
dotnet ef migrations script `
    --idempotent `
    --project $persistenceProject `
    --startup-project $apiProject `
    --output $OutputMigrationSqlPath `
    --configuration $Configuration

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $OutputMigrationSqlPath)) {
    throw "EF migration SQL script creation failed."
}

$webConfigPath = Join-Path $publishRoot "web.config"
$apiDllPath = Join-Path $publishRoot "ITAdmin.Api.dll"
$apiExePath = Join-Path $publishRoot "ITAdmin.Api.exe"
$indexPath = Join-Path $publishRoot "wwwroot\index.html"

if (-not (Test-Path -LiteralPath $webConfigPath)) {
    throw "Package validation failed: web.config is missing."
}

if (-not (Test-Path -LiteralPath $apiDllPath) -and -not (Test-Path -LiteralPath $apiExePath)) {
    throw "Package validation failed: ITAdmin.Api.dll or ITAdmin.Api.exe is missing."
}

if (-not (Test-Path -LiteralPath $indexPath)) {
    throw "Package validation failed: wwwroot\index.html is missing."
}

if (Test-Path -LiteralPath $OutputPackagePath) {
    Remove-Item -LiteralPath $OutputPackagePath -Force
}

Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $OutputPackagePath -Force

Write-ITAdminPackageMessage -Message "Package created: $OutputPackagePath"
Write-ITAdminPackageMessage -Message "SQL migration script created: $OutputMigrationSqlPath"
Write-ITAdminPackageMessage -Message ""
Write-ITAdminPackageMessage -Message "Copy these files to the Windows Server deployment folder:"
Write-ITAdminPackageMessage -Message "  $OutputPackagePath"
Write-ITAdminPackageMessage -Message "  $installScriptPath"
Write-ITAdminPackageMessage -Message "  $OutputMigrationSqlPath (optional, for SqlFile migration mode)"
Write-ITAdminPackageMessage -Message "== ITAdmin deployment package build completed =="
