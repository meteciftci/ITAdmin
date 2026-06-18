#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[CmdletBinding()]
param(
    [Parameter()]
    [string]$SiteName = "ITAdmin",

    [Parameter()]
    [string]$AppPoolName = "ITAdmin"
)

function Format-ITAdminSecretValue {
    param(
        [AllowNull()]
        [string]$Name,

        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "[not set]"
    }

    if ($Name -match '(?i)(ConnectionStrings|Password|Secret|Key|Hash|Token|Thumbprint)') {
        if ($Name -match '(?i)ConnectionStrings') {
            return [regex]::Replace(
                $Value,
                '(?i)(Password|Pwd)\s*=\s*[^;]+',
                '$1=[REDACTED]'
            )
        }

        return "[REDACTED]"
    }

    return $Value
}

function Get-ITAdminMachineEnvironmentSnapshot {
    $prefix = "ITADMIN_"
    $snapshot = @{}

    foreach ($entry in [Environment]::GetEnvironmentVariables("Machine").GetEnumerator()) {
        $name = [string]$entry.Key
        if (-not $name.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            continue
        }

        $snapshot[$name] = Format-ITAdminSecretValue -Name $name -Value ([string]$entry.Value)
    }

    return $snapshot
}

function Get-ITAdminAppPoolEnvironmentSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName
    )

    $snapshot = @{}
    $filterPath = "system.applicationHost/applicationPools/add[@name='$PoolName']/environmentVariables/add"
    $items = Get-WebConfiguration -PSPath "MACHINE/WEBROOT/APPHOST" -Filter $filterPath -ErrorAction SilentlyContinue

    if ($null -eq $items) {
        return $snapshot
    }

    foreach ($item in @($items)) {
        $name = $item.name
        $value = $item.value
        if ($null -ne $name) {
            $snapshot[[string]$name] = Format-ITAdminSecretValue -Name ([string]$name) -Value ([string]$value)
        }
    }

    return $snapshot
}

function Get-ITAdminDataProtectionStatus {
    param(
        [AllowNull()]
        [string]$KeysPath
    )

    if ([string]::IsNullOrWhiteSpace($KeysPath)) {
        return [PSCustomObject]@{
            Path = $null
            Exists = $false
            FileCount = 0
        }
    }

    $exists = Test-Path -LiteralPath $KeysPath
    $fileCount = 0
    if ($exists) {
        $fileCount = (Get-ChildItem -LiteralPath $KeysPath -File -ErrorAction SilentlyContinue | Measure-Object).Count
    }

    return [PSCustomObject]@{
        Path = $KeysPath
        Exists = $exists
        FileCount = $fileCount
    }
}

Import-Module WebAdministration -ErrorAction Stop

Write-Host "== ITAdmin runtime configuration =="

$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if ($null -eq $site) {
    Write-Warning "IIS site not found: $SiteName"
}
else {
    Write-Host "Site name: $($site.Name)"
    Write-Host "Site state: $($site.State)"
    Write-Host "Physical path: $($site.PhysicalPath)"
    Write-Host "Application pool: $($site.ApplicationPool)"

    $bindings = Get-WebBinding -Name $SiteName -ErrorAction SilentlyContinue
    if ($null -ne $bindings) {
        Write-Host "Bindings:"
        foreach ($binding in @($bindings)) {
            $bindingInfo = $binding.bindingInformation
            $protocol = $binding.protocol
            Write-Host "  - $protocol $bindingInfo"
        }
    }
}

$appPool = Get-Item "IIS:\AppPools\$AppPoolName" -ErrorAction SilentlyContinue
if ($null -eq $appPool) {
    Write-Warning "IIS app pool not found: $AppPoolName"
}
else {
    Write-Host "App pool name: $AppPoolName"
    Write-Host "Managed runtime: $($appPool.managedRuntimeVersion)"
    Write-Host "Start mode: $($appPool.startMode)"
    Write-Host "Auto start: $($appPool.autoStart)"
    Write-Host "Identity: $($appPool.processModel.identityType)"
    Write-Host "Load user profile: $($appPool.processModel.loadUserProfile)"
}

Write-Host ""
Write-Host "App pool environment variables:"
$appPoolEnvironment = Get-ITAdminAppPoolEnvironmentSnapshot -PoolName $AppPoolName
if ($appPoolEnvironment.Count -eq 0) {
    Write-Host "  [none]"
}
else {
    foreach ($entry in ($appPoolEnvironment.GetEnumerator() | Sort-Object Name)) {
        Write-Host ("  {0}={1}" -f $entry.Key, $entry.Value)
    }
}

Write-Host ""
Write-Host "Machine environment variables (ITADMIN_ prefix):"
$machineEnvironment = Get-ITAdminMachineEnvironmentSnapshot
if ($machineEnvironment.Count -eq 0) {
    Write-Host "  [none]"
}
else {
    foreach ($entry in ($machineEnvironment.GetEnumerator() | Sort-Object Name)) {
        Write-Host ("  {0}={1}" -f $entry.Key, $entry.Value)
    }
}

$keysPath = [Environment]::GetEnvironmentVariable("ITADMIN_DataProtection__KeysPath", "Machine")
$dataProtectionStatus = Get-ITAdminDataProtectionStatus -KeysPath $keysPath

Write-Host ""
Write-Host "DataProtection keys:"
Write-Host ("  Path: {0}" -f $(if ($null -eq $dataProtectionStatus.Path) { "[not set]" } else { $dataProtectionStatus.Path }))
Write-Host ("  Path exists: {0}" -f $dataProtectionStatus.Exists)
Write-Host ("  Key file count: {0}" -f $dataProtectionStatus.FileCount)

Write-Host ""
Write-Host "== End runtime configuration =="
