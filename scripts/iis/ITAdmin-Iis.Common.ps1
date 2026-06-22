#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
        $name = $item.name
        $value = $item.value
        if ($null -ne $name) {
            $snapshot[[string]$name] = [string]$value
        }
    }

    return $snapshot
}

function Set-ITAdminAppPoolEnvironmentVariables {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName,

        [Parameter(Mandatory = $true)]
        [hashtable]$Variables
    )

    $current = Get-ITAdminAppPoolEnvironmentSnapshot -PoolName $PoolName

    foreach ($entry in $Variables.GetEnumerator()) {
        $name = [string]$entry.Key
        $value = [string]$entry.Value

        if ([string]::IsNullOrWhiteSpace($value)) {
            if ($current.ContainsKey($name)) {
                [void]$current.Remove($name)
            }
            continue
        }

        $current[$name] = $value
    }

    $filterPath = "system.applicationHost/applicationPools/add[@name='$PoolName']/environmentVariables"
    Clear-WebConfiguration `
        -PSPath "MACHINE/WEBROOT/APPHOST" `
        -Filter $filterPath `
        -ErrorAction SilentlyContinue

    foreach ($entry in ($current.GetEnumerator() | Sort-Object Name)) {
        Add-WebConfigurationProperty `
            -PSPath "MACHINE/WEBROOT/APPHOST" `
            -Filter $filterPath `
            -Name "." `
            -Value @{
                name = [string]$entry.Key
                value = [string]$entry.Value
            } | Out-Null
    }
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
            Source = "Machine"
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
        [hashtable]$Snapshot,

        [Parameter()]
        [switch]$MaskSecrets
    )

    Write-Host $Title
    if ($Snapshot.Count -eq 0) {
        Write-Host "  [none]"
        return
    }

    foreach ($entry in ($Snapshot.GetEnumerator() | Sort-Object Name)) {
        $displayValue = if ($MaskSecrets) {
            Format-ITAdminSecretValue -Name $entry.Key -Value $entry.Value
        }
        else {
            $entry.Value
        }

        Write-Host ("  {0}={1}" -f $entry.Key, $displayValue)
    }
}
