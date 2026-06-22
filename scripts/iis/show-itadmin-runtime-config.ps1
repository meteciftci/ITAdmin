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

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptRoot "ITAdmin-Iis.Common.ps1")

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
Write-ITAdminEnvironmentSnapshot -Title "App pool environment variables:" -Snapshot $appPoolEnvironment -MaskSecrets

Write-Host ""
Write-ITAdminEnvironmentSnapshot -Title "Machine environment variables (legacy ITADMIN_* / ASPNETCORE_ENVIRONMENT):" -Snapshot $machineEnvironment -MaskSecrets

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
Write-Host "  Machine-level ITADMIN_* values are legacy and shown for migration visibility only."

Write-Host ""
Write-Host "== End runtime configuration =="
