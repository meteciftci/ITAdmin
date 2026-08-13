#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [Parameter()]
    [string]$SiteName = "ITAdmin",

    [Parameter()]
    [string]$AppPoolName = "ITAdmin",

    [Parameter()]
    [string]$HostName,

    [Parameter()]
    [string]$PhysicalPath = "C:\inetpub\wwwroot\ITAdmin",

    [Parameter()]
    [string]$RuntimeRoot = "C:\ProgramData\ITAdmin",

    [Parameter()]
    [ValidateSet("Staging", "Production")]
    [string]$EnvironmentName = "Production",

    [Parameter()]
    [ValidateSet("Existing", "Skip", "InstallLocalPostgreSql")]
    [string]$DatabaseMode,

    [Parameter()]
    [string]$PackagePath,

    [Parameter()]
    [string]$CertificateThumbprint,

    [Parameter()]
    [string]$DataProtectionCertificateThumbprint,

    [Parameter()]
    [string]$PostgreSqlInstallerPath,

    [Parameter()]
    [int]$PostgreSqlPort = 5432,

    [Parameter()]
    [string]$DatabaseHost = "localhost",

    [Parameter()]
    [string]$DatabaseName = "itadmin",

    [Parameter()]
    [string]$DatabaseUser = "itadmin_app",

    [Parameter()]
    [SecureString]$DatabasePassword,

    [Parameter()]
    [switch]$ForceRuntimeConfig,

    [Parameter()]
    [ValidateSet("Manual", "SqlFile", "Skip")]
    [string]$MigrationMode,

    [Parameter()]
    [string]$MigrationSqlPath,

    [Parameter()]
    [switch]$SkipMigration,

    [Parameter()]
    [switch]$SkipSmokeTest,

    [Parameter()]
    [switch]$NoHttps
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# =============================================================================================
# DEPRECATED - superseded by scripts/install/Install-ITAdmin.ps1 (Installer v2).
#
# This script is the legacy deployment path. It extracts a release zip directly over the live IIS
# directory with no staging, no integrity verification, no release identity, and no installation
# state, so a failure part-way through leaves the site destroyed and unrecoverable without a
# rebuild. It is retained only until Installer v2 has passed acceptance on a real Windows host,
# after which it will be removed.
#
# Do not use it for new installations. Set ITADMIN_USE_LEGACY_INSTALLER=1 to run it anyway.
# =============================================================================================
if ($env:ITADMIN_USE_LEGACY_INSTALLER -ne "1") {
    throw "install-itadmin-server.ps1 is deprecated and superseded by scripts/install/Install-ITAdmin.ps1. " +
          "Set ITADMIN_USE_LEGACY_INSTALLER=1 only if you deliberately need the legacy path."
}



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

function Write-ITAdminMessage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter()]
        [ValidateSet("Info", "Warning", "Error")]
        [string]$Level = "Info"
    )

    switch ($Level) {
        "Warning" { Write-Warning $Message }
        "Error" { Write-Error $Message }
        default { Write-Host $Message }
    }
}

function Test-ITAdminParameterWasBound {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return $PSBoundParameters.ContainsKey($Name)
}

function Read-ITAdminPromptValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt,

        [Parameter()]
        [AllowNull()]
        [string]$DefaultValue
    )

    if ($null -ne $DefaultValue -and $DefaultValue.Length -gt 0) {
        $inputValue = Read-Host -Prompt "$Prompt [$DefaultValue]"
        if ([string]::IsNullOrWhiteSpace($inputValue)) {
            return $DefaultValue
        }

        return $inputValue.Trim()
    }

    return (Read-Host -Prompt $Prompt).Trim()
}

function Read-ITAdminYesNoPrompt {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt,

        [Parameter()]
        [bool]$DefaultValue = $false
    )

    $defaultLabel = if ($DefaultValue) { "Y/n" } else { "y/N" }
    $inputValue = Read-Host -Prompt "$Prompt ($defaultLabel)"
    if ([string]::IsNullOrWhiteSpace($inputValue)) {
        return $DefaultValue
    }

    return $inputValue -match '^(y|yes)$'
}

function Read-ITAdminSecurePrompt {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt
    )

    return Read-Host -Prompt $Prompt -AsSecureString
}

function ConvertFrom-ITAdminSecureString {
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$SecureString
    )

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Test-ITAdminAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function ConvertTo-ITAdminBase64Url {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes
    )

    $base64 = [Convert]::ToBase64String($Bytes)
    return $base64.TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-ITAdminCryptographicSecret {
    param(
        [Parameter()]
        [ValidateRange(1, 4096)]
        [int]$ByteLength = 64,

        [Parameter()]
        [ValidateSet("Base64Url", "Base64", "Hex")]
        [string]$Format = "Base64Url"
    )

    $bytes = New-Object byte[] $ByteLength
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        if ($null -ne $rng) {
            $rng.Dispose()
        }
    }

    switch ($Format) {
        "Base64Url" { return ConvertTo-ITAdminBase64Url -Bytes $bytes }
        "Base64" { return [Convert]::ToBase64String($bytes) }
        "Hex" { return ([BitConverter]::ToString($bytes)).Replace("-", "").ToLowerInvariant() }
        default { throw "Unsupported secret format: $Format" }
    }
}

function New-ITAdminSetupKeyMaterial {
    param(
        [Parameter()]
        [ValidateRange(16, 128)]
        [int]$PlaintextByteLength = 32
    )

    $plaintext = New-ITAdminCryptographicSecret -ByteLength $PlaintextByteLength -Format "Base64Url"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($plaintext))
    }
    finally {
        if ($null -ne $sha) {
            $sha.Dispose()
        }
    }

    return [PSCustomObject]@{
        PlaintextSetupKey = $plaintext
        SetupKeyHash = "sha256:{0}" -f (ConvertTo-ITAdminBase64Url -Bytes $hashBytes)
    }
}

function Hide-ITAdminSecretValue {
    param(
        [AllowNull()]
        [string]$Value
    )

    return "[REDACTED]"
}

function Hide-ITAdminConnectionString {
    param(
        [AllowNull()]
        [string]$ConnectionString
    )

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return "[REDACTED]"
    }

    return [regex]::Replace(
        $ConnectionString,
        '(?i)(Password|Pwd)\s*=\s*[^;]+',
        '$1=[REDACTED]'
    )
}

function Format-ITAdminRuntimeVariableForDisplay {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "[not set]"
    }

    if ($Name -eq "ITADMIN_ConnectionStrings__DefaultConnection") {
        return Hide-ITAdminConnectionString -ConnectionString $Value
    }

    if ($Name -match '(?i)(Password|Secret|Key|Hash|Token|Thumbprint)') {
        return Hide-ITAdminSecretValue -Value $Value
    }

    return $Value
}

function Get-ITAdminMachineEnvironmentVariables {
    $snapshot = @{}
    $prefix = "ITADMIN_"

    foreach ($entry in [Environment]::GetEnvironmentVariables("Machine").GetEnumerator()) {
        $name = [string]$entry.Key
        if ($name.StartsWith($prefix, [System.StringComparison]::Ordinal) -or $name -eq "ASPNETCORE_ENVIRONMENT") {
            $snapshot[$name] = [string]$entry.Value
        }
    }

    return $snapshot
}

function Get-ITAdminAppPoolEnvironmentVariables {
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

function Set-ITAdminAppPoolEnvironmentVariables {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName,

        [Parameter(Mandatory = $true)]
        [hashtable]$Variables
    )

    $current = Get-ITAdminAppPoolEnvironmentVariables -PoolName $PoolName

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
        return [PSCustomObject]@{
            Value = [string]$AppPoolEnvironment[$Name]
            Source = "AppPool"
        }
    }

    if ($MachineEnvironment.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace($MachineEnvironment[$Name])) {
        return [PSCustomObject]@{
            Value = [string]$MachineEnvironment[$Name]
            Source = "MachineLegacy"
        }
    }

    return [PSCustomObject]@{
        Value = $null
        Source = "NotSet"
    }
}

function Show-ITAdminExistingRuntimeConfiguration {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$AppPoolEnvironment,

        [Parameter(Mandatory = $true)]
        [hashtable]$MachineEnvironment
    )

    Write-ITAdminMessage -Message "Existing runtime configuration detected."

    Write-ITAdminMessage -Message "App pool environment variables (primary runtime source):"
    if ($AppPoolEnvironment.Count -eq 0) {
        Write-ITAdminMessage -Message "  [none]"
    }
    else {
        foreach ($entry in ($AppPoolEnvironment.GetEnumerator() | Sort-Object Name)) {
            Write-ITAdminMessage -Message ("  {0}={1}" -f $entry.Key, (Format-ITAdminRuntimeVariableForDisplay -Name $entry.Key -Value $entry.Value))
        }
    }

    Write-ITAdminMessage -Message "Machine environment variables (legacy visibility only):"
    if ($MachineEnvironment.Count -eq 0) {
        Write-ITAdminMessage -Message "  [none]"
    }
    else {
        foreach ($entry in ($MachineEnvironment.GetEnumerator() | Sort-Object Name)) {
            Write-ITAdminMessage -Message ("  {0}={1}" -f $entry.Key, (Format-ITAdminRuntimeVariableForDisplay -Name $entry.Key -Value $entry.Value))
        }

        Write-ITAdminMessage -Message "Legacy machine values are shown for visibility only. New runtime config is written to app pool environment variables."
    }
}

function Test-ITAdminExistingRuntimeConfiguration {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$AppPoolEnvironment,

        [Parameter(Mandatory = $true)]
        [hashtable]$MachineEnvironment
    )

    foreach ($name in $Script:ITAdminKnownRuntimeVariableNames) {
        if ($AppPoolEnvironment.ContainsKey($name) -and -not [string]::IsNullOrWhiteSpace($AppPoolEnvironment[$name])) {
            return $true
        }

        if ($MachineEnvironment.ContainsKey($name) -and -not [string]::IsNullOrWhiteSpace($MachineEnvironment[$name])) {
            return $true
        }
    }

    return $false
}

function Resolve-ITAdminRuntimeConfigOverwrite {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$HasExistingConfiguration
    )

    if (-not $HasExistingConfiguration) {
        return $true
    }

    if ($ForceRuntimeConfig) {
        Write-ITAdminMessage -Message "ForceRuntimeConfig specified. Existing runtime configuration will be overwritten."
        return $true
    }

    return Read-ITAdminYesNoPrompt -Prompt "Overwrite existing runtime configuration?" -DefaultValue $false
}

function Ensure-ITAdminWindowsFeatures {
    $requiredFeatures = @(
        "Web-Server",
        "Web-WebSockets",
        "Web-Common-Http",
        "Web-Default-Doc",
        "Web-Http-Errors",
        "Web-Static-Content",
        "Web-Http-Logging",
        "Web-Request-Monitor",
        "Web-Performance",
        "Web-Stat-Compression",
        "Web-Filtering",
        "Web-Mgmt-Console"
    )

    $missingFeatures = @()
    foreach ($featureName in $requiredFeatures) {
        $feature = Get-WindowsFeature -Name $featureName -ErrorAction SilentlyContinue
        if ($null -eq $feature) {
            Write-ITAdminMessage -Message "Windows feature not found on this server: $featureName" -Level Warning
            continue
        }

        if (-not $feature.Installed) {
            $missingFeatures += $featureName
        }
    }

    if ($missingFeatures.Count -eq 0) {
        Write-ITAdminMessage -Message "Required IIS Windows features are installed."
        return
    }

    Write-ITAdminMessage -Message ("Installing missing IIS features: {0}" -f ($missingFeatures -join ", "))
    $result = Install-WindowsFeature -Name $missingFeatures -IncludeManagementTools
    if ($null -ne $result -and $result.RestartNeeded -eq "Yes") {
        Write-ITAdminMessage -Message "A server restart may be required to complete IIS feature installation." -Level Warning
    }
}

function Test-ITAdminAspNetCoreHostingBundle {
    $hostingDllPath = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    if (Test-Path -LiteralPath $hostingDllPath) {
        Write-ITAdminMessage -Message "ASP.NET Core Hosting Bundle appears to be installed."
        return
    }

    throw "ASP.NET Core Hosting Bundle was not detected. Install the .NET Hosting Bundle on this server before running ITAdmin installation."
}

function Get-ITAdminCertificateInfoByThumbprint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Thumbprint
    )

    $normalized = $Thumbprint.Replace(" ", "").ToUpperInvariant()

    $certificate = Get-Item "Cert:\LocalMachine\My\$normalized" -ErrorAction SilentlyContinue
    if ($null -ne $certificate) {
        return @{
            Certificate = $certificate
            StoreName = "My"
            Thumbprint = $normalized
        }
    }

    $certificate = Get-Item "Cert:\LocalMachine\WebHosting\$normalized" -ErrorAction SilentlyContinue
    if ($null -ne $certificate) {
        return @{
            Certificate = $certificate
            StoreName = "WebHosting"
            Thumbprint = $normalized
        }
    }

    throw "Certificate not found in LocalMachine\My or LocalMachine\WebHosting. Thumbprint: $Thumbprint"
}

function Ensure-ITAdminRuntimeDirectories {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeRootPath,

        [Parameter(Mandatory = $true)]
        [string]$PhysicalSitePath,

        [Parameter(Mandatory = $true)]
        [string]$AppPoolIdentityName
    )

    $dataProtectionPath = Join-Path $RuntimeRootPath "DataProtection-Keys"
    $logsPath = Join-Path $RuntimeRootPath "Logs"

    foreach ($path in @($RuntimeRootPath, $dataProtectionPath, $logsPath, $PhysicalSitePath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            New-Item -ItemType Directory -Path $path -Force | Out-Null
            Write-ITAdminMessage -Message "Created directory: $path"
        }
    }

    foreach ($path in @($dataProtectionPath, $logsPath)) {
        & icacls $path /grant "${AppPoolIdentityName}:(OI)(CI)M" /T | Out-Null
    }

    & icacls $PhysicalSitePath /grant "${AppPoolIdentityName}:(OI)(CI)RX" /T | Out-Null
}

function Ensure-ITAdminAppPool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName
    )

    if (-not (Test-Path "IIS:\AppPools\$PoolName")) {
        Write-ITAdminMessage -Message "Creating app pool: $PoolName"
        New-WebAppPool -Name $PoolName | Out-Null
    }
    else {
        Write-ITAdminMessage -Message "App pool exists: $PoolName"
    }

    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name enable32BitAppOnWin64 -Value $false
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name startMode -Value "AlwaysRunning"
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name autoStart -Value $true
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.loadUserProfile -Value $true
}

function Ensure-ITAdminSite {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Site,

        [Parameter(Mandatory = $true)]
        [string]$PoolName,

        [Parameter(Mandatory = $true)]
        [string]$SiteHostName,

        [Parameter(Mandatory = $true)]
        [string]$SitePhysicalPath,

        [Parameter()]
        [AllowNull()]
        [string]$HttpsCertificateThumbprint
    )

    $existingSite = Get-Website -Name $Site -ErrorAction SilentlyContinue
    if ($null -eq $existingSite) {
        Write-ITAdminMessage -Message "Creating IIS site: $Site"
        New-Website `
            -Name $Site `
            -PhysicalPath $SitePhysicalPath `
            -ApplicationPool $PoolName `
            -Port 80 `
            -HostHeader $SiteHostName | Out-Null
    }
    else {
        Write-ITAdminMessage -Message "IIS site exists: $Site"
        Set-ItemProperty "IIS:\Sites\$Site" -Name physicalPath -Value $SitePhysicalPath
        Set-ItemProperty "IIS:\Sites\$Site" -Name applicationPool -Value $PoolName
    }

    $httpBinding = Get-WebBinding -Name $Site -Protocol "http" -HostHeader $SiteHostName -Port 80 -ErrorAction SilentlyContinue
    if ($null -eq $httpBinding) {
        New-WebBinding -Name $Site -Protocol "http" -Port 80 -HostHeader $SiteHostName | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($HttpsCertificateThumbprint)) {
        $httpsBinding = Get-WebBinding -Name $Site -Protocol "https" -HostHeader $SiteHostName -Port 443 -ErrorAction SilentlyContinue
        if ($null -eq $httpsBinding) {
            New-WebBinding `
                -Name $Site `
                -Protocol "https" `
                -Port 443 `
                -HostHeader $SiteHostName `
                -SslFlags 1 | Out-Null
        }

        $certInfo = Get-ITAdminCertificateInfoByThumbprint -Thumbprint $HttpsCertificateThumbprint
        $httpsBinding = Get-WebBinding -Name $Site -Protocol "https" -Port 443 -HostHeader $SiteHostName -ErrorAction SilentlyContinue
        if ($null -eq $httpsBinding) {
            throw "HTTPS web binding could not be found for ${SiteHostName}:443"
        }

        Write-ITAdminMessage -Message "Applying HTTPS certificate binding using store $($certInfo.StoreName)"
        $httpsBinding.AddSslCertificate($certInfo.Thumbprint, $certInfo.StoreName)
    }
}

function Find-ITAdminPsqlExecutable {
    $command = Get-Command "psql.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidateRoots = @(
        "C:\Program Files\PostgreSQL",
        "C:\Program Files (x86)\PostgreSQL"
    )

    foreach ($root in $candidateRoots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        $versionDirs = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending

        foreach ($versionDir in $versionDirs) {
            $psqlPath = Join-Path $versionDir.FullName "bin\psql.exe"
            if (Test-Path -LiteralPath $psqlPath) {
                return $psqlPath
            }
        }
    }

    return $null
}

function New-ITAdminPostgreSqlConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$HostNameValue,

        [Parameter(Mandatory = $true)]
        [int]$Port,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$User,

        [Parameter(Mandatory = $true)]
        [string]$Password
    )

    return "Host=$HostNameValue;Port=$Port;Database=$Name;Username=$User;Password=$Password"
}

function ConvertFrom-ITAdminPostgreSqlConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString
    )

    $parts = @{}
    foreach ($segment in $ConnectionString.Split(';')) {
        if ([string]::IsNullOrWhiteSpace($segment)) {
            continue
        }

        $pair = $segment.Split('=', 2)
        if ($pair.Length -ne 2) {
            continue
        }

        $key = $pair[0].Trim()
        $value = $pair[1].Trim()
        switch -Regex ($key) {
            '^(?i)Host$' { $parts.Host = $value }
            '^(?i)Port$' { $parts.Port = [int]$value }
            '^(?i)Database$' { $parts.Database = $value }
            '^(?i)Username$' { $parts.Username = $value }
            '^(?i)Password$' { $parts.Password = $value }
        }
    }

    return [PSCustomObject]$parts
}

function Show-ITAdminDatabaseTargetSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$HostNameValue,

        [Parameter(Mandatory = $true)]
        [int]$Port,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$User
    )

    Write-ITAdminMessage -Message "Effective database target:"
    Write-ITAdminMessage -Message "  Host: $HostNameValue"
    Write-ITAdminMessage -Message "  Port: $Port"
    Write-ITAdminMessage -Message "  Database: $Name"
    Write-ITAdminMessage -Message "  Username: $User"
    Write-ITAdminMessage -Message "  Password: [REDACTED]"
}

function Resolve-ITAdminDatabaseConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Mode,

        [Parameter()]
        [int]$Port,

        [Parameter()]
        [string]$HostNameValue,

        [Parameter()]
        [string]$Name,

        [Parameter()]
        [string]$User,

        [Parameter()]
        [SecureString]$PasswordSecure,

        [Parameter(Mandatory = $true)]
        [hashtable]$AppPoolEnvironment,

        [Parameter(Mandatory = $true)]
        [hashtable]$MachineEnvironment,

        [Parameter(Mandatory = $true)]
        [bool]$OverwriteRuntimeConfig
    )

    switch ($Mode) {
        "Skip" {
            $existing = Get-ITAdminEffectiveRuntimeVariable `
                -Name "ITADMIN_ConnectionStrings__DefaultConnection" `
                -AppPoolEnvironment $AppPoolEnvironment `
                -MachineEnvironment $MachineEnvironment

            if ([string]::IsNullOrWhiteSpace($existing.Value)) {
                Write-ITAdminMessage -Message "DatabaseMode is Skip and no existing connection string was found." -Level Warning
            }
            else {
                Write-ITAdminMessage -Message ("Keeping existing connection string from {0}: {1}" -f $existing.Source, (Hide-ITAdminConnectionString -ConnectionString $existing.Value))
            }

            return $existing.Value
        }

        "Existing" {
            if (-not (Test-ITAdminParameterWasBound -Name "DatabaseHost")) {
                $HostNameValue = Read-ITAdminPromptValue -Prompt "PostgreSQL host" -DefaultValue $HostNameValue
            }

            if (-not (Test-ITAdminParameterWasBound -Name "PostgreSqlPort")) {
                $portText = Read-ITAdminPromptValue -Prompt "PostgreSQL port" -DefaultValue ([string]$Port)
                $Port = [int]$portText
            }

            if (-not (Test-ITAdminParameterWasBound -Name "DatabaseName")) {
                $Name = Read-ITAdminPromptValue -Prompt "Database name" -DefaultValue $Name
            }

            if (-not (Test-ITAdminParameterWasBound -Name "DatabaseUser")) {
                $User = Read-ITAdminPromptValue -Prompt "Database user" -DefaultValue $User
            }

            if ($null -ne $PasswordSecure) {
                $passwordPlain = ConvertFrom-ITAdminSecureString -SecureString $PasswordSecure
            }
            else {
                $passwordPlain = ConvertFrom-ITAdminSecureString -SecureString (Read-ITAdminSecurePrompt -Prompt "Database password")
            }

            Show-ITAdminDatabaseTargetSummary `
                -HostNameValue $HostNameValue `
                -Port $Port `
                -Name $Name `
                -User $User

            $confirmed = Read-ITAdminYesNoPrompt -Prompt "Confirm database name '$Name'?" -DefaultValue $true
            if (-not $confirmed) {
                throw "Database configuration was not confirmed."
            }

            return New-ITAdminPostgreSqlConnectionString `
                -HostNameValue $HostNameValue `
                -Port $Port `
                -Name $Name `
                -User $User `
                -Password $passwordPlain
        }

        "InstallLocalPostgreSql" {
            throw "InstallLocalPostgreSql is not part of the primary installer flow. Use DatabaseMode Existing after preparing PostgreSQL."
        }

        default {
            throw "Unsupported DatabaseMode: $Mode"
        }
    }
}

function Build-ITAdminRuntimeEnvironmentVariables {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AspNetCoreEnvironment,

        [Parameter()]
        [AllowNull()]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$DataProtectionApplicationName,

        [Parameter(Mandatory = $true)]
        [string]$DataProtectionKeysPath,

        [Parameter()]
        [AllowNull()]
        [string]$DataProtectionCertificateThumbprint,

        [Parameter(Mandatory = $true)]
        [hashtable]$AppPoolEnvironment,

        [Parameter(Mandatory = $true)]
        [hashtable]$MachineEnvironment,

        [Parameter(Mandatory = $true)]
        [bool]$OverwriteRuntimeConfig,

        [Parameter(Mandatory = $false)]
        [ref]$SetupKeyPlaintextToShow
    )

    $variables = @{}

    $existingJwtKey = $null
    $existingSetupKeyHash = $null

    if (-not $OverwriteRuntimeConfig) {
        $existingJwt = Get-ITAdminEffectiveRuntimeVariable `
            -Name "ITADMIN_Jwt__Key" `
            -AppPoolEnvironment $AppPoolEnvironment `
            -MachineEnvironment $MachineEnvironment
        $existingJwtKey = $existingJwt.Value

        $existingSetup = Get-ITAdminEffectiveRuntimeVariable `
            -Name "ITADMIN_Setup__SetupKeyHash" `
            -AppPoolEnvironment $AppPoolEnvironment `
            -MachineEnvironment $MachineEnvironment
        $existingSetupKeyHash = $existingSetup.Value
    }

    $variables["ASPNETCORE_ENVIRONMENT"] = $AspNetCoreEnvironment

    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
        $variables["ITADMIN_ConnectionStrings__DefaultConnection"] = $ConnectionString
        Write-ITAdminMessage -Message ("Configured ITADMIN_ConnectionStrings__DefaultConnection: {0}" -f (Hide-ITAdminConnectionString -ConnectionString $ConnectionString))
    }
    elseif (-not $OverwriteRuntimeConfig) {
        $existingConnection = Get-ITAdminEffectiveRuntimeVariable `
            -Name "ITADMIN_ConnectionStrings__DefaultConnection" `
            -AppPoolEnvironment $AppPoolEnvironment `
            -MachineEnvironment $MachineEnvironment

        if (-not [string]::IsNullOrWhiteSpace($existingConnection.Value)) {
            $variables["ITADMIN_ConnectionStrings__DefaultConnection"] = $existingConnection.Value
            Write-ITAdminMessage -Message ("Preserved ITADMIN_ConnectionStrings__DefaultConnection from {0}: {1}" -f $existingConnection.Source, (Hide-ITAdminConnectionString -ConnectionString $existingConnection.Value))
        }
    }

    if ($OverwriteRuntimeConfig -or [string]::IsNullOrWhiteSpace($existingJwtKey)) {
        $variables["ITADMIN_Jwt__Key"] = New-ITAdminCryptographicSecret -ByteLength 64 -Format "Base64Url"
        Write-ITAdminMessage -Message "Configured new ITADMIN_Jwt__Key."
    }
    else {
        $variables["ITADMIN_Jwt__Key"] = $existingJwtKey
        Write-ITAdminMessage -Message "Preserved existing ITADMIN_Jwt__Key."
    }

    $variables["ITADMIN_Jwt__Issuer"] = "ITAdmin"
    $variables["ITADMIN_Jwt__Audience"] = "ITAdmin.Client"

    if ($OverwriteRuntimeConfig -or [string]::IsNullOrWhiteSpace($existingSetupKeyHash)) {
        $setupMaterial = New-ITAdminSetupKeyMaterial
        $variables["ITADMIN_Setup__SetupKeyHash"] = $setupMaterial.SetupKeyHash
        if ($null -ne $SetupKeyPlaintextToShow) {
            $SetupKeyPlaintextToShow.Value = $setupMaterial.PlaintextSetupKey
        }

        Write-ITAdminMessage -Message "Configured new ITADMIN_Setup__SetupKeyHash."
    }
    else {
        $variables["ITADMIN_Setup__SetupKeyHash"] = $existingSetupKeyHash
        Write-ITAdminMessage -Message "Preserved existing ITADMIN_Setup__SetupKeyHash. Plaintext setup key is not available."
    }

    $variables["ITADMIN_DataProtection__ApplicationName"] = $DataProtectionApplicationName
    $variables["ITADMIN_DataProtection__KeysPath"] = $DataProtectionKeysPath

    if (-not [string]::IsNullOrWhiteSpace($DataProtectionCertificateThumbprint)) {
        $variables["ITADMIN_DataProtection__CertificateThumbprint"] = $DataProtectionCertificateThumbprint.Replace(" ", "").ToUpperInvariant()
        Write-ITAdminMessage -Message "Configured ITADMIN_DataProtection__CertificateThumbprint."
    }
    else {
        $variables["ITADMIN_DataProtection__CertificateThumbprint"] = ""
        Write-ITAdminMessage -Message "DataProtection certificate thumbprint was not provided. ITADMIN_DataProtection__CertificateThumbprint will not be set."
    }

    return $variables
}

function Test-ITAdminPackagePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Package file not found: $Path"
    }

    if ([System.IO.Path]::GetExtension($Path) -ne ".zip") {
        throw "Package file must be a .zip archive: $Path"
    }
}

function Test-ITAdminDeployedPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SiteRoot
    )

    $webConfigPath = Join-Path $SiteRoot "web.config"
    $apiDllPath = Join-Path $SiteRoot "ITAdmin.Api.dll"
    $apiExePath = Join-Path $SiteRoot "ITAdmin.Api.exe"
    $indexPath = Join-Path $SiteRoot "wwwroot\index.html"

    if (-not (Test-Path -LiteralPath $webConfigPath)) {
        throw "Deployed package is missing required file: web.config"
    }

    if (-not (Test-Path -LiteralPath $apiDllPath) -and -not (Test-Path -LiteralPath $apiExePath)) {
        throw "Deployed package is missing required file: ITAdmin.Api.dll or ITAdmin.Api.exe"
    }

    if (-not (Test-Path -LiteralPath $indexPath)) {
        throw "Deployed package is missing required file: wwwroot\index.html"
    }
}

function Deploy-ITAdminPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageArchivePath,

        [Parameter(Mandatory = $true)]
        [string]$SiteRoot,

        [Parameter(Mandatory = $true)]
        [string]$DeploySiteName,

        [Parameter(Mandatory = $true)]
        [string]$DeployAppPoolName,

        [Parameter(Mandatory = $true)]
        [string]$AppPoolIdentityName
    )

    Write-ITAdminMessage -Message "Starting package deployment from: $PackageArchivePath"

    if (Get-Website -Name $DeploySiteName -ErrorAction SilentlyContinue) {
        Stop-Website -Name $DeploySiteName -ErrorAction SilentlyContinue
    }

    if (Test-Path "IIS:\AppPools\$DeployAppPoolName") {
        Stop-WebAppPool -Name $DeployAppPoolName -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    if (-not (Test-Path -LiteralPath $SiteRoot)) {
        New-Item -ItemType Directory -Path $SiteRoot -Force | Out-Null
    }

    $offlineFile = Join-Path $SiteRoot "app_offline.htm"
    Set-Content -LiteralPath $offlineFile -Value "<html><body><h1>ITAdmin deployment in progress</h1></body></html>" -Encoding UTF8

    Get-ChildItem -LiteralPath $SiteRoot -Force |
        Where-Object { $_.Name -ne "app_offline.htm" } |
        Remove-Item -Recurse -Force

    Expand-Archive -LiteralPath $PackageArchivePath -DestinationPath $SiteRoot -Force

    Test-ITAdminDeployedPackage -SiteRoot $SiteRoot

    & icacls $SiteRoot /grant "${AppPoolIdentityName}:(OI)(CI)RX" /T | Out-Null

    Write-ITAdminMessage -Message "Package deployment completed."
}

function Invoke-ITAdminDatabaseMigration {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Manual", "SqlFile", "Skip")]
        [string]$Mode,

        [Parameter()]
        [AllowNull()]
        [string]$SqlFilePath,

        [Parameter()]
        [AllowNull()]
        [string]$ConnectionString
    )

    switch ($Mode) {
        "Skip" {
            Write-ITAdminMessage -Message "Database migration skipped."
            return "Skipped"
        }

        "Manual" {
            Write-ITAdminMessage -Message "Database migration is expected to be applied manually."
            return "Manual"
        }

        "SqlFile" {
            if ([string]::IsNullOrWhiteSpace($SqlFilePath)) {
                throw "SQL migration file path is required for SqlFile migration mode."
            }

            if (-not (Test-Path -LiteralPath $SqlFilePath)) {
                throw "SQL migration file not found: $SqlFilePath"
            }

            if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
                throw "Database migration cannot run without a connection string. Configure DatabaseMode Existing or preserve an existing connection string."
            }

            $psqlPath = Find-ITAdminPsqlExecutable
            if ($null -eq $psqlPath) {
                throw "psql.exe was not found. Run the SQL migration file manually on the database server or install PostgreSQL client tools."
            }

            $connectionParts = ConvertFrom-ITAdminPostgreSqlConnectionString -ConnectionString $ConnectionString
            if ([string]::IsNullOrWhiteSpace($connectionParts.Host) -or
                [string]::IsNullOrWhiteSpace($connectionParts.Database) -or
                [string]::IsNullOrWhiteSpace($connectionParts.Username) -or
                [string]::IsNullOrWhiteSpace($connectionParts.Password)) {
                throw "Connection string is missing required PostgreSQL fields for SQL migration."
            }

            $portValue = if ($null -ne $connectionParts.Port -and $connectionParts.Port -gt 0) { $connectionParts.Port } else { 5432 }

            Write-ITAdminMessage -Message "Applying database migration from SQL file: $SqlFilePath"
            $env:PGPASSWORD = $connectionParts.Password
            try {
                & $psqlPath `
                    -h $connectionParts.Host `
                    -p $portValue `
                    -U $connectionParts.Username `
                    -d $connectionParts.Database `
                    -v ON_ERROR_STOP=1 `
                    -f $SqlFilePath | Out-Null

                if ($LASTEXITCODE -ne 0) {
                    throw "SQL migration script failed with exit code $LASTEXITCODE."
                }
            }
            finally {
                Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
            }

            return "Applied (SqlFile)"
        }

        default {
            throw "Unsupported migration mode: $Mode"
        }
    }
}

function Get-ITAdminRecentApplicationLogTail {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeRootPath,

        [Parameter()]
        [int]$MaxLines = 40
    )

    $logsPath = Join-Path $RuntimeRootPath "Logs"
    if (-not (Test-Path -LiteralPath $logsPath)) {
        return "[no application log directory found]"
    }

    $latestLog = Get-ChildItem -LiteralPath $logsPath -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latestLog) {
        return "[no application log files found]"
    }

    return (Get-Content -LiteralPath $latestLog.FullName -Tail $MaxLines -ErrorAction SilentlyContinue) -join [Environment]::NewLine
}

function Get-ITAdminRecentWindowsEventLogErrors {
    param(
        [Parameter()]
        [int]$MaxEvents = 10
    )

    $providers = @(
        "IIS AspNetCore Module V2",
        ".NET Runtime",
        "Application Error"
    )

    $messages = New-Object System.Collections.Generic.List[string]
    foreach ($provider in $providers) {
        try {
            $events = Get-WinEvent -FilterHashtable @{
                LogName = "Application"
                ProviderName = $provider
            } -MaxEvents $MaxEvents -ErrorAction SilentlyContinue

            foreach ($event in @($events)) {
                $messages.Add(("{0} [{1}] {2}" -f $event.TimeCreated, $provider, $event.Message))
            }
        }
        catch {
            continue
        }
    }

    if ($messages.Count -eq 0) {
        return "[no recent IIS or .NET application event log entries found]"
    }

    return ($messages | Select-Object -First $MaxEvents) -join [Environment]::NewLine
}

function Test-ITAdminSetupStatusEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,

        [Parameter(Mandatory = $true)]
        [string]$RuntimeRootPath,

        [Parameter()]
        [int]$MaxAttempts = 10,

        [Parameter()]
        [int]$DelaySeconds = 3
    )

    $uri = "{0}/api/setup/status" -f $BaseUrl.TrimEnd('/')
    $lastStatusCode = $null
    $lastBody = $null
    $lastError = $null

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 30
            $lastStatusCode = [int]$response.StatusCode
            $lastBody = $response.Content

            if ($lastStatusCode -eq 200) {
                return [PSCustomObject]@{
                    Success = $true
                    StatusCode = $lastStatusCode
                    Body = $lastBody
                    Uri = $uri
                }
            }
        }
        catch {
            $lastError = $_.Exception.Message
            if ($_.Exception.Response) {
                try {
                    $lastStatusCode = [int]$_.Exception.Response.StatusCode
                }
                catch {
                    $lastStatusCode = $null
                }
            }
        }

        Write-ITAdminMessage -Message "Smoke test attempt $attempt/$MaxAttempts failed for $uri. Retrying in $DelaySeconds second(s)..."
        Start-Sleep -Seconds $DelaySeconds
    }

    return [PSCustomObject]@{
        Success = $false
        StatusCode = $lastStatusCode
        Body = $lastBody
        Error = $lastError
        Uri = $uri
        LogTail = (Get-ITAdminRecentApplicationLogTail -RuntimeRootPath $RuntimeRootPath)
        EventLogTail = (Get-ITAdminRecentWindowsEventLogErrors)
    }
}

function Remove-ITAdminAppOfflineFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SiteRoot
    )

    $offlineFile = Join-Path $SiteRoot "app_offline.htm"
    if (Test-Path -LiteralPath $offlineFile) {
        Remove-Item -LiteralPath $offlineFile -Force
    }
}

function Start-ITAdminWebStack {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StartSiteName,

        [Parameter(Mandatory = $true)]
        [string]$StartAppPoolName
    )

    if (Test-Path "IIS:\AppPools\$StartAppPoolName") {
        Start-WebAppPool -Name $StartAppPoolName
    }

    if (Get-Website -Name $StartSiteName -ErrorAction SilentlyContinue) {
        Start-Website -Name $StartSiteName
    }
}

function Show-ITAdminFinalSummary {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Summary
    )

    Write-ITAdminMessage -Message "== ITAdmin installation summary =="
    Write-ITAdminMessage -Message ("Site name: {0}" -f $Summary.SiteName)
    Write-ITAdminMessage -Message ("App pool name: {0}" -f $Summary.AppPoolName)
    Write-ITAdminMessage -Message ("Hostname: {0}" -f $Summary.HostName)
    Write-ITAdminMessage -Message ("Physical path: {0}" -f $Summary.PhysicalPath)
    Write-ITAdminMessage -Message ("Runtime root: {0}" -f $Summary.RuntimeRoot)
    Write-ITAdminMessage -Message ("Setup URL: {0}" -f $Summary.SetupUrl)

    if ($null -ne $Summary.DatabaseSummary) {
        Write-ITAdminMessage -Message $Summary.DatabaseSummary
    }

    Write-ITAdminMessage -Message ("Migration: {0}" -f $Summary.MigrationResult)
    Write-ITAdminMessage -Message ("Smoke test: {0}" -f $Summary.SmokeTestResult)

    if (-not [string]::IsNullOrWhiteSpace($Summary.SetupKeyPlaintext)) {
        Write-Host ""
        Write-Host "IMPORTANT: Save the setup key below securely. It is shown once and is not stored on the server."
        Write-Host ("Setup key: {0}" -f $Summary.SetupKeyPlaintext)
        Write-Host ""
    }
    elseif ($Summary.SetupKeyPreserved) {
        Write-ITAdminMessage -Message "Existing setup key hash was preserved. Plaintext setup key is not available."
    }
}

if ($PSVersionTable.PSVersion.Major -lt 5) {
    throw "PowerShell 5.1 or later is required."
}

if (-not (Test-ITAdminAdministrator)) {
    throw "This script must be run from an elevated PowerShell session."
}

Write-ITAdminMessage -Message "== ITAdmin installation started =="

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $PSScriptRoot "itadmin-package.zip"
}

if (-not (Test-ITAdminParameterWasBound -Name "SiteName")) {
    $SiteName = Read-ITAdminPromptValue -Prompt "Site name" -DefaultValue $SiteName
}

if (-not (Test-ITAdminParameterWasBound -Name "AppPoolName")) {
    $AppPoolName = Read-ITAdminPromptValue -Prompt "App pool name" -DefaultValue $AppPoolName
}

if (-not (Test-ITAdminParameterWasBound -Name "HostName") -or [string]::IsNullOrWhiteSpace($HostName)) {
    $HostName = Read-ITAdminPromptValue -Prompt "Host name (example: itadmin.domain.local)" -DefaultValue $null
    if ([string]::IsNullOrWhiteSpace($HostName)) {
        throw "Host name is required."
    }
}

if (-not (Test-ITAdminParameterWasBound -Name "PhysicalPath")) {
    $PhysicalPath = Read-ITAdminPromptValue -Prompt "Physical path" -DefaultValue $PhysicalPath
}

if (-not (Test-ITAdminParameterWasBound -Name "RuntimeRoot")) {
    $RuntimeRoot = Read-ITAdminPromptValue -Prompt "Runtime root" -DefaultValue $RuntimeRoot
}

if (-not (Test-ITAdminParameterWasBound -Name "EnvironmentName")) {
    $EnvironmentName = Read-ITAdminPromptValue -Prompt "Environment (Staging/Production)" -DefaultValue $EnvironmentName
}

if (-not (Test-ITAdminParameterWasBound -Name "NoHttps") -and -not (Test-ITAdminParameterWasBound -Name "CertificateThumbprint")) {
    $useHttps = Read-ITAdminYesNoPrompt -Prompt "Use HTTPS?" -DefaultValue $true
    if (-not $useHttps) {
        $NoHttps = $true
    }
}

if ($NoHttps) {
    $CertificateThumbprint = $null
}
elseif (-not (Test-ITAdminParameterWasBound -Name "CertificateThumbprint") -or [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $CertificateThumbprint = Read-ITAdminPromptValue -Prompt "HTTPS certificate thumbprint" -DefaultValue $null
    if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        throw "HTTPS certificate thumbprint is required when HTTPS is enabled."
    }
}

if (-not (Test-ITAdminParameterWasBound -Name "DataProtectionCertificateThumbprint")) {
    $dataProtectionInput = Read-ITAdminPromptValue -Prompt "DataProtection certificate thumbprint (optional)" -DefaultValue ""
    if (-not [string]::IsNullOrWhiteSpace($dataProtectionInput)) {
        $DataProtectionCertificateThumbprint = $dataProtectionInput
    }
}

if (-not (Test-ITAdminParameterWasBound -Name "DatabaseMode") -or [string]::IsNullOrWhiteSpace($DatabaseMode)) {
    Write-ITAdminMessage -Message "Database mode:"
    Write-ITAdminMessage -Message "  1. Existing PostgreSQL"
    Write-ITAdminMessage -Message "  2. Skip database configuration"
    $databaseChoice = Read-ITAdminPromptValue -Prompt "Select database mode [1/2]" -DefaultValue "1"
    switch ($databaseChoice) {
        "2" { $DatabaseMode = "Skip" }
        default { $DatabaseMode = "Existing" }
    }
}

if ($SkipMigration.IsPresent) {
    $MigrationMode = "Skip"
}

if (-not (Test-ITAdminParameterWasBound -Name "MigrationMode") -or [string]::IsNullOrWhiteSpace($MigrationMode)) {
    Write-ITAdminMessage -Message "Migration mode:"
    Write-ITAdminMessage -Message "  1. Manual - SQL migration applied or will be applied manually on the database"
    Write-ITAdminMessage -Message "  2. SqlFile - Apply SQL file from this server"
    Write-ITAdminMessage -Message "  3. Skip - Skip migration step entirely"
    $migrationChoice = Read-ITAdminPromptValue -Prompt "Select migration mode [1/2/3]" -DefaultValue "1"
    switch ($migrationChoice) {
        "2" { $MigrationMode = "SqlFile" }
        "3" { $MigrationMode = "Skip" }
        default { $MigrationMode = "Manual" }
    }
}

if ($MigrationMode -eq "SqlFile" -and [string]::IsNullOrWhiteSpace($MigrationSqlPath)) {
    $MigrationSqlPath = Join-Path $PSScriptRoot "itadmin-migrations.sql"
}

Import-Module WebAdministration -ErrorAction Stop

Ensure-ITAdminWindowsFeatures
Test-ITAdminAspNetCoreHostingBundle
Test-ITAdminPackagePath -Path $PackagePath

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    Get-ITAdminCertificateInfoByThumbprint -Thumbprint $CertificateThumbprint | Out-Null
}

if (-not [string]::IsNullOrWhiteSpace($DataProtectionCertificateThumbprint)) {
    Get-ITAdminCertificateInfoByThumbprint -Thumbprint $DataProtectionCertificateThumbprint | Out-Null
}

$appPoolIdentity = "IIS AppPool\$AppPoolName"
Ensure-ITAdminRuntimeDirectories `
    -RuntimeRootPath $RuntimeRoot `
    -PhysicalSitePath $PhysicalPath `
    -AppPoolIdentityName $appPoolIdentity

Ensure-ITAdminAppPool -PoolName $AppPoolName
Ensure-ITAdminSite `
    -Site $SiteName `
    -PoolName $AppPoolName `
    -SiteHostName $HostName `
    -SitePhysicalPath $PhysicalPath `
    -HttpsCertificateThumbprint $CertificateThumbprint

$appPoolEnvironment = Get-ITAdminAppPoolEnvironmentVariables -PoolName $AppPoolName
$machineEnvironment = Get-ITAdminMachineEnvironmentVariables
$hasExistingRuntimeConfiguration = Test-ITAdminExistingRuntimeConfiguration `
    -AppPoolEnvironment $appPoolEnvironment `
    -MachineEnvironment $machineEnvironment

if ($hasExistingRuntimeConfiguration) {
    Show-ITAdminExistingRuntimeConfiguration `
        -AppPoolEnvironment $appPoolEnvironment `
        -MachineEnvironment $machineEnvironment
}

$overwriteRuntimeConfig = Resolve-ITAdminRuntimeConfigOverwrite -HasExistingConfiguration $hasExistingRuntimeConfiguration
if (-not $overwriteRuntimeConfig -and $hasExistingRuntimeConfiguration) {
    Write-ITAdminMessage -Message "Existing runtime configuration will be preserved and migrated to app pool environment variables where needed."
}

$connectionString = Resolve-ITAdminDatabaseConnectionString `
    -Mode $DatabaseMode `
    -Port $PostgreSqlPort `
    -HostNameValue $DatabaseHost `
    -Name $DatabaseName `
    -User $DatabaseUser `
    -PasswordSecure $DatabasePassword `
    -AppPoolEnvironment $appPoolEnvironment `
    -MachineEnvironment $machineEnvironment `
    -OverwriteRuntimeConfig $overwriteRuntimeConfig

$dataProtectionApplicationName = "ITAdmin-$EnvironmentName"
$dataProtectionKeysPath = Join-Path $RuntimeRoot "DataProtection-Keys"
$setupKeyPlaintextToShow = $null
$setupKeyPreserved = $false

$runtimeVariables = Build-ITAdminRuntimeEnvironmentVariables `
    -AspNetCoreEnvironment $EnvironmentName `
    -ConnectionString $connectionString `
    -DataProtectionApplicationName $dataProtectionApplicationName `
    -DataProtectionKeysPath $dataProtectionKeysPath `
    -DataProtectionCertificateThumbprint $DataProtectionCertificateThumbprint `
    -AppPoolEnvironment $appPoolEnvironment `
    -MachineEnvironment $machineEnvironment `
    -OverwriteRuntimeConfig $overwriteRuntimeConfig `
    -SetupKeyPlaintextToShow ([ref]$setupKeyPlaintextToShow)

if ($null -eq $setupKeyPlaintextToShow) {
    $setupKeyPreserved = -not $overwriteRuntimeConfig -and $hasExistingRuntimeConfiguration
}

Write-ITAdminMessage -Message "Applying runtime configuration to app pool environment variables."
Set-ITAdminAppPoolEnvironmentVariables -PoolName $AppPoolName -Variables $runtimeVariables

$effectiveAppPoolEnvironment = Get-ITAdminAppPoolEnvironmentVariables -PoolName $AppPoolName
Write-ITAdminMessage -Message "Effective runtime configuration (app pool environment):"
foreach ($entry in ($effectiveAppPoolEnvironment.GetEnumerator() | Sort-Object Name)) {
    Write-ITAdminMessage -Message ("  {0}={1}" -f $entry.Key, (Format-ITAdminRuntimeVariableForDisplay -Name $entry.Key -Value $entry.Value))
}

$effectiveConnection = Get-ITAdminEffectiveRuntimeVariable `
    -Name "ITADMIN_ConnectionStrings__DefaultConnection" `
    -AppPoolEnvironment $effectiveAppPoolEnvironment `
    -MachineEnvironment @{}

Deploy-ITAdminPackage `
    -PackageArchivePath $PackagePath `
    -SiteRoot $PhysicalPath `
    -DeploySiteName $SiteName `
    -DeployAppPoolName $AppPoolName `
    -AppPoolIdentityName $appPoolIdentity

$migrationResult = Invoke-ITAdminDatabaseMigration `
    -Mode $MigrationMode `
    -SqlFilePath $MigrationSqlPath `
    -ConnectionString $effectiveConnection.Value

Remove-ITAdminAppOfflineFile -SiteRoot $PhysicalPath
Start-ITAdminWebStack -StartSiteName $SiteName -StartAppPoolName $AppPoolName

$baseScheme = if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) { "http" } else { "https" }
$baseUrl = "{0}://{1}" -f $baseScheme, $HostName
$setupUrl = "{0}/setup" -f $baseUrl

$smokeTestResult = "Skipped"
if (-not $SkipSmokeTest.IsPresent) {
    $smokeTest = Test-ITAdminSetupStatusEndpoint -BaseUrl $baseUrl -RuntimeRootPath $RuntimeRoot
    if ($smokeTest.Success) {
        $smokeTestResult = "Passed (HTTP 200)"
        Write-ITAdminMessage -Message "Smoke test passed for $($smokeTest.Uri)"
    }
    else {
        Write-ITAdminMessage -Message "Smoke test failed for $($smokeTest.Uri)" -Level Error
        if ($null -ne $smokeTest.StatusCode) {
            Write-ITAdminMessage -Message ("HTTP status: {0}" -f $smokeTest.StatusCode) -Level Error
        }

        if (-not [string]::IsNullOrWhiteSpace($smokeTest.Body)) {
            Write-ITAdminMessage -Message ("Response body: {0}" -f $smokeTest.Body) -Level Error
        }

        if (-not [string]::IsNullOrWhiteSpace($smokeTest.Error)) {
            Write-ITAdminMessage -Message ("Last error: {0}" -f $smokeTest.Error) -Level Error
        }

        Write-ITAdminMessage -Message "Recent application log tail:" -Level Error
        Write-Host $smokeTest.LogTail
        Write-ITAdminMessage -Message "Recent Windows application event log entries:" -Level Error
        Write-Host $smokeTest.EventLogTail

        $migrationHint = ""
        if ($MigrationMode -eq "Manual") {
            $migrationHint = " Verify that database migration was applied manually before retrying."
        }

        throw "Smoke test failed.$migrationHint Installation did not complete successfully."
    }
}
else {
    Write-ITAdminMessage -Message "Smoke test skipped because -SkipSmokeTest was specified." -Level Warning
}

$databaseSummary = $null
if (-not [string]::IsNullOrWhiteSpace($effectiveConnection.Value)) {
    $dbParts = ConvertFrom-ITAdminPostgreSqlConnectionString -ConnectionString $effectiveConnection.Value
    $databaseSummary = "Database: Host=$($dbParts.Host); Port=$($dbParts.Port); Database=$($dbParts.Database); Username=$($dbParts.Username); Password=[REDACTED]"
}

Show-ITAdminFinalSummary -Summary @{
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    HostName = $HostName
    PhysicalPath = $PhysicalPath
    RuntimeRoot = $RuntimeRoot
    SetupUrl = $setupUrl
    DatabaseSummary = $databaseSummary
    MigrationResult = $migrationResult
    SmokeTestResult = $smokeTestResult
    SetupKeyPlaintext = $setupKeyPlaintextToShow
    SetupKeyPreserved = $setupKeyPreserved
}

Write-ITAdminMessage -Message "== ITAdmin installation completed =="
