#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter()]
    [string]$SiteName = "ITAdmin",

    [Parameter()]
    [string]$AppPoolName = "ITAdmin"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Script:ITAdminKnownRuntimeVariableNames = @(
    "ASPNETCORE_ENVIRONMENT",
    "ITADMIN_ConnectionStrings__DefaultConnection",
    "ITADMIN_Jwt__Key",
    "ITADMIN_Jwt__Issuer",
    "ITADMIN_Jwt__Audience",
    "ITADMIN_Setup__SetupKeyHash",
    "ITADMIN_DataProtection__ApplicationName",
    "ITADMIN_DataProtection__KeysPath",
    "ITADMIN_DataProtection__CertificateThumbprint"
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
    $snapshot = @{}
    $prefix = "ITADMIN_"

    foreach ($entry in [Environment]::GetEnvironmentVariables("Machine").GetEnumerator()) {
        $name = [string]$entry.Key
        if (-not $name.StartsWith($prefix, [System.StringComparison]::Ordinal) -and $name -ne "ASPNETCORE_ENVIRONMENT") {
            continue
        }

        $snapshot[$name] = [string]$entry.Value
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
        if ($null -ne $item.name) {
            $snapshot[[string]$item.name] = [string]$item.value
        }
    }

    return $snapshot
}

function Get-ITAdminEffectiveRuntimeVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [hashtable]$AppPoolEnvironment,

        [Parameter(Mandatory = $true)]
        [hashtable]$MachineEnvironment
    )

    if ($AppPoolEnvironment.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace($AppPoolEnvironment[$Name])) {
        return @{
            Value = [string]$AppPoolEnvironment[$Name]
            Source = "AppPool"
        }
    }

    if ($MachineEnvironment.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace($MachineEnvironment[$Name])) {
        return @{
            Value = [string]$MachineEnvironment[$Name]
            Source = "MachineLegacy"
        }
    }

    return @{
        Value = $null
        Source = "NotSet"
    }
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

function Write-ITAdminEnvironmentSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        [hashtable]$Snapshot
    )

    Write-Host $Title
    if ($Snapshot.Count -eq 0) {
        Write-Host "  [none]"
        return
    }

    foreach ($entry in ($Snapshot.GetEnumerator() | Sort-Object Name)) {
        Write-Host ("  {0}={1}" -f $entry.Key, (Format-ITAdminSecretValue -Name $entry.Key -Value $entry.Value))
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
            Write-Host ("  - {0} {1}" -f $binding.protocol, $binding.bindingInformation)
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

$appPoolEnvironment = Get-ITAdminAppPoolEnvironmentSnapshot -PoolName $AppPoolName
$machineEnvironment = Get-ITAdminMachineEnvironmentSnapshot

Write-Host ""
Write-ITAdminEnvironmentSnapshot -Title "App pool environment variables (primary runtime source):" -Snapshot $appPoolEnvironment

Write-Host ""
Write-ITAdminEnvironmentSnapshot -Title "Machine environment variables (legacy visibility only):" -Snapshot $machineEnvironment

Write-Host ""
Write-Host "Effective runtime configuration:"
foreach ($name in $Script:ITAdminKnownRuntimeVariableNames) {
    $effective = Get-ITAdminEffectiveRuntimeVariable `
        -Name $name `
        -AppPoolEnvironment $appPoolEnvironment `
        -MachineEnvironment $machineEnvironment

    $displayValue = Format-ITAdminSecretValue -Name $name -Value $effective.Value
    Write-Host ("  {0}={1} (source: {2})" -f $name, $displayValue, $effective.Source)
}

$keysPathResult = Get-ITAdminEffectiveRuntimeVariable `
    -Name "ITADMIN_DataProtection__KeysPath" `
    -AppPoolEnvironment $appPoolEnvironment `
    -MachineEnvironment $machineEnvironment

$dataProtectionStatus = Get-ITAdminDataProtectionStatus -KeysPath $keysPathResult.Value

Write-Host ""
Write-Host "DataProtection keys:"
Write-Host ("  Path: {0}" -f $(if ($null -eq $dataProtectionStatus.Path) { "[not set]" } else { $dataProtectionStatus.Path }))
Write-Host ("  Path exists: {0}" -f $dataProtectionStatus.Exists)
Write-Host ("  Key file count: {0}" -f $dataProtectionStatus.FileCount)

Write-Host ""
Write-Host "Configuration source note:"
Write-Host "  App pool environment variables are the primary runtime configuration source."
Write-Host "  Machine-level ITADMIN_* values are legacy and shown for visibility only."

Write-Host ""
Write-Host "== End runtime configuration =="
