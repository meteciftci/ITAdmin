#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepositoryRoot,

    [Parameter()]
    [string]$OutputPackagePath,

    [Parameter()]
    [string]$Configuration = "Release",

    [Parameter()]
    [ValidateSet("bundle", "sql", "auto")]
    [string]$MigrationArtifactMode = "auto"
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
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$backendRoot = Join-Path $RepositoryRoot "backend"
$frontendRoot = Join-Path $RepositoryRoot "frontend"
$apiProject = Join-Path $backendRoot "src\ITAdmin.Api\ITAdmin.Api.csproj"
$persistenceProject = Join-Path $backendRoot "src\ITAdmin.Persistence\ITAdmin.Persistence.csproj"
$stagingRoot = Join-Path $RepositoryRoot "artifacts\package-staging"
$publishRoot = Join-Path $stagingRoot "publish"
$deployRoot = Join-Path $publishRoot "_deploy"

if ([string]::IsNullOrWhiteSpace($OutputPackagePath)) {
    $OutputPackagePath = Join-Path $PSScriptRoot "itadmin-package.zip"
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
New-Item -ItemType Directory -Path $deployRoot -Force | Out-Null

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

$migrationBundlePath = Join-Path $deployRoot "ITAdmin.Migrations.exe"
$migrationSqlPath = Join-Path $deployRoot "itadmin-migrations.sql"
$migrationCreated = $false

if ($MigrationArtifactMode -in @("bundle", "auto")) {
    Write-ITAdminPackageMessage -Message "Creating EF migration bundle..."
    dotnet ef migrations bundle `
        --project $persistenceProject `
        --startup-project $apiProject `
        --output $migrationBundlePath `
        --configuration $Configuration `
        --target-runtime win-x64 `
        --self-contained false

    if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $migrationBundlePath)) {
        $migrationCreated = $true
        Write-ITAdminPackageMessage -Message "Migration bundle created: $migrationBundlePath"
    }
    elseif ($MigrationArtifactMode -eq "bundle") {
        throw "EF migration bundle creation failed."
    }
}

if (-not $migrationCreated) {
    Write-ITAdminPackageMessage -Message "Creating idempotent SQL migration script..."
    dotnet ef migrations script `
        --idempotent `
        --project $persistenceProject `
        --startup-project $apiProject `
        --output $migrationSqlPath `
        --configuration $Configuration

    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $migrationSqlPath)) {
        throw "EF migration SQL script creation failed."
    }

    Write-ITAdminPackageMessage -Message "Migration SQL script created: $migrationSqlPath"
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

if (-not (Test-Path -LiteralPath $migrationBundlePath) -and -not (Test-Path -LiteralPath $migrationSqlPath)) {
    throw "Package validation failed: migration artifact is missing."
}

if (Test-Path -LiteralPath $OutputPackagePath) {
    Remove-Item -LiteralPath $OutputPackagePath -Force
}

Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $OutputPackagePath -Force

Write-ITAdminPackageMessage -Message "Package created: $OutputPackagePath"
Write-ITAdminPackageMessage -Message "== ITAdmin deployment package build completed =="
