#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess = $true)]
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
    [ValidateSet("Existing", "InstallLocalPostgreSql", "Skip")]
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
    $hashBytes = [System.Security.Cryptography.SHA256]::Create().ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes($plaintext)
    )
    $hash = "sha256:{0}" -f (ConvertTo-ITAdminBase64Url -Bytes $hashBytes)

    return [PSCustomObject]@{
        PlaintextSetupKey = $plaintext
        SetupKeyHash = $hash
    }
}

function Write-ITAdminBootstrapMessage {
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

function Test-ITAdminAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Hide-ITAdminSecretValue {
    param(
        [AllowNull()]
        [string]$Value,

        [Parameter()]
        [string]$Placeholder = "[REDACTED]"
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Placeholder
    }

    return $Placeholder
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

    if ($PSCmdlet.ShouldProcess($PoolName, "Set app pool environment variables")) {
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
}

function Set-ITAdminAppPoolEnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName,

        [Parameter(Mandatory = $true)]
        [string]$VariableName,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$VariableValue
    )

    $update = @{}
    $update[$VariableName] = $VariableValue
    Set-ITAdminAppPoolEnvironmentVariables -PoolName $PoolName -Variables $update
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
        return [string]$AppPoolEnvironment[$Name]
    }

    if ($MachineEnvironment.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace($MachineEnvironment[$Name])) {
        return [string]$MachineEnvironment[$Name]
    }

    return $null
}

function Show-ITAdminExistingRuntimeConfiguration {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$AppPoolEnvironment,

        [Parameter(Mandatory = $true)]
        [hashtable]$MachineEnvironment
    )

    Write-ITAdminBootstrapMessage -Message "Existing runtime configuration detected."

    Write-ITAdminBootstrapMessage -Message "App pool environment variables:"
    if ($AppPoolEnvironment.Count -eq 0) {
        Write-ITAdminBootstrapMessage -Message "  [none]"
    }
    else {
        foreach ($entry in ($AppPoolEnvironment.GetEnumerator() | Sort-Object Name)) {
            Write-ITAdminBootstrapMessage -Message ("  {0}={1}" -f $entry.Key, (Format-ITAdminRuntimeVariableForDisplay -Name $entry.Key -Value $entry.Value))
        }
    }

    Write-ITAdminBootstrapMessage -Message "Machine environment variables (legacy ITADMIN_* / ASPNETCORE_ENVIRONMENT — visibility only, not primary runtime source):"
    if ($MachineEnvironment.Count -eq 0) {
        Write-ITAdminBootstrapMessage -Message "  [none]"
    }
    else {
        foreach ($entry in ($MachineEnvironment.GetEnumerator() | Sort-Object Name)) {
            Write-ITAdminBootstrapMessage -Message ("  {0}={1}" -f $entry.Key, (Format-ITAdminRuntimeVariableForDisplay -Name $entry.Key -Value $entry.Value))
        }

        Write-ITAdminBootstrapMessage -Message "Legacy machine environment values are not used as the primary runtime source. New configuration is written to app pool environment variables."
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
        Write-ITAdminBootstrapMessage -Message "ForceRuntimeConfig specified. Existing runtime configuration will be overwritten."
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
            Write-ITAdminBootstrapMessage -Message "Windows feature not found on this server: $featureName" -Level Warning
            continue
        }

        if (-not $feature.Installed) {
            $missingFeatures += $featureName
        }
    }

    if ($missingFeatures.Count -eq 0) {
        Write-ITAdminBootstrapMessage -Message "Required IIS Windows features are installed."
        return
    }

    Write-ITAdminBootstrapMessage -Message ("Installing missing IIS features: {0}" -f ($missingFeatures -join ", "))
    if ($PSCmdlet.ShouldProcess($env:COMPUTERNAME, "Install Windows features")) {
        $result = Install-WindowsFeature -Name $missingFeatures -IncludeManagementTools
        if ($null -ne $result -and $result.RestartNeeded -eq "Yes") {
            Write-ITAdminBootstrapMessage -Message "A server restart may be required to complete IIS feature installation." -Level Warning
        }
    }
}

function Test-ITAdminAspNetCoreHostingBundle {
    $hostingDllPath = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    if (Test-Path -LiteralPath $hostingDllPath) {
        Write-ITAdminBootstrapMessage -Message "ASP.NET Core Hosting Bundle appears to be installed."
        return
    }

    throw "ASP.NET Core Hosting Bundle was not detected (aspnetcorev2.dll missing). Install the .NET Hosting Bundle on this server before running ITAdmin installation."
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

    $paths = @(
        $RuntimeRootPath,
        $dataProtectionPath,
        $logsPath,
        $PhysicalSitePath
    )

    foreach ($path in $paths) {
        if (-not (Test-Path -LiteralPath $path)) {
            if ($PSCmdlet.ShouldProcess($path, "Create directory")) {
                New-Item -ItemType Directory -Path $path -Force | Out-Null
                Write-ITAdminBootstrapMessage -Message "Created directory: $path"
            }
        }
    }

    if ($PSCmdlet.ShouldProcess($RuntimeRootPath, "Set directory ACLs")) {
        foreach ($path in @($dataProtectionPath, $logsPath)) {
            & icacls $path /grant "${AppPoolIdentityName}:(OI)(CI)M" /T | Out-Null
        }

        & icacls $PhysicalSitePath /grant "${AppPoolIdentityName}:(OI)(CI)RX" /T | Out-Null
    }
}

function Ensure-ITAdminAppPool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName
    )

    if (-not (Test-Path "IIS:\AppPools\$PoolName")) {
        Write-ITAdminBootstrapMessage -Message "Creating app pool: $PoolName"
        if ($PSCmdlet.ShouldProcess($PoolName, "Create app pool")) {
            New-WebAppPool -Name $PoolName | Out-Null
        }
    }
    else {
        Write-ITAdminBootstrapMessage -Message "App pool exists: $PoolName"
    }

    if ($PSCmdlet.ShouldProcess($PoolName, "Configure app pool")) {
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name managedRuntimeVersion -Value ""
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name enable32BitAppOnWin64 -Value $false
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name startMode -Value "AlwaysRunning"
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name autoStart -Value $true
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
        Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.loadUserProfile -Value $true
    }
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
        [string]$HttpsCertificateThumbprint
    )

    $existingSite = Get-Website -Name $Site -ErrorAction SilentlyContinue
    if ($null -eq $existingSite) {
        Write-ITAdminBootstrapMessage -Message "Creating IIS site: $Site"
        if ($PSCmdlet.ShouldProcess($Site, "Create IIS site")) {
            New-Website `
                -Name $Site `
                -PhysicalPath $SitePhysicalPath `
                -ApplicationPool $PoolName `
                -Port 80 `
                -HostHeader $SiteHostName | Out-Null
        }
    }
    else {
        Write-ITAdminBootstrapMessage -Message "IIS site exists: $Site"
        if ($PSCmdlet.ShouldProcess($Site, "Update IIS site")) {
            Set-ItemProperty "IIS:\Sites\$Site" -Name physicalPath -Value $SitePhysicalPath
            Set-ItemProperty "IIS:\Sites\$Site" -Name applicationPool -Value $PoolName
        }
    }

    $httpBinding = Get-WebBinding -Name $Site -Protocol "http" -HostHeader $SiteHostName -Port 80 -ErrorAction SilentlyContinue
    if ($null -eq $httpBinding) {
        if ($PSCmdlet.ShouldProcess($Site, "Create HTTP binding")) {
            New-WebBinding -Name $Site -Protocol "http" -Port 80 -HostHeader $SiteHostName | Out-Null
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($HttpsCertificateThumbprint)) {
        $httpsBinding = Get-WebBinding -Name $Site -Protocol "https" -HostHeader $SiteHostName -Port 443 -ErrorAction SilentlyContinue
        if ($null -eq $httpsBinding) {
            if ($PSCmdlet.ShouldProcess($Site, "Create HTTPS binding")) {
                New-WebBinding `
                    -Name $Site `
                    -Protocol "https" `
                    -Port 443 `
                    -HostHeader $SiteHostName `
                    -SslFlags 1 | Out-Null
            }
        }

        $certInfo = Get-ITAdminCertificateInfoByThumbprint -Thumbprint $HttpsCertificateThumbprint
        $httpsBinding = Get-WebBinding -Name $Site -Protocol "https" -Port 443 -HostHeader $SiteHostName -ErrorAction SilentlyContinue
        if ($null -eq $httpsBinding) {
            throw "HTTPS web binding could not be found for ${SiteHostName}:443"
        }

        Write-ITAdminBootstrapMessage -Message "Applying HTTPS certificate binding using store $($certInfo.StoreName)"
        if ($PSCmdlet.ShouldProcess($Site, "Apply HTTPS certificate")) {
            $httpsBinding.AddSslCertificate($certInfo.Thumbprint, $certInfo.StoreName)
        }
    }
}

function Find-ITAdminPsqlExecutable {
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

function New-ITAdminDatabasePassword {
    return New-ITAdminCryptographicSecret -ByteLength 32 -Format "Base64Url"
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

    Write-ITAdminBootstrapMessage -Message "Effective database target:"
    Write-ITAdminBootstrapMessage -Message "  Host: $HostNameValue"
    Write-ITAdminBootstrapMessage -Message "  Port: $Port"
    Write-ITAdminBootstrapMessage -Message "  Database: $Name"
    Write-ITAdminBootstrapMessage -Message "  Username: $User"
    Write-ITAdminBootstrapMessage -Message "  Password: [REDACTED]"
}

function Resolve-ITAdminDatabaseConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Mode,

        [Parameter()]
        [string]$InstallerPath,

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

            if ([string]::IsNullOrWhiteSpace($existing)) {
                Write-ITAdminBootstrapMessage -Message "DatabaseMode is Skip and no existing connection string was found in app pool or machine environment." -Level Warning
            }
            else {
                Write-ITAdminBootstrapMessage -Message ("Keeping existing connection string: {0}" -f (Hide-ITAdminConnectionString -ConnectionString $existing))
            }

            return $existing
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

            $passwordPlain = $null
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

            if (-not $OverwriteRuntimeConfig -or -not (Test-ITAdminParameterWasBound -Name "DatabaseName")) {
                $confirmed = Read-ITAdminYesNoPrompt -Prompt "Confirm database name '$Name'?" -DefaultValue $true
                if (-not $confirmed) {
                    throw "Database configuration was not confirmed."
                }
            }

            return New-ITAdminPostgreSqlConnectionString `
                -HostNameValue $HostNameValue `
                -Port $Port `
                -Name $Name `
                -User $User `
                -Password $passwordPlain
        }

        "InstallLocalPostgreSql" {
            if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
                $InstallerPath = Read-Host -Prompt "PostgreSQL 18 installer path (local .exe)"
            }

            if (-not (Test-Path -LiteralPath $InstallerPath)) {
                throw "PostgreSQL installer not found: $InstallerPath"
            }

            Write-ITAdminBootstrapMessage -Message "Starting PostgreSQL silent installation from local installer."
            $superPassword = New-ITAdminDatabasePassword
            $servicePassword = New-ITAdminDatabasePassword

            $arguments = @(
                "--mode", "unattended",
                "--unattendedmodeui", "none",
                "--superpassword", $superPassword,
                "--servicename", "postgresql-x64-18",
                "--servicepassword", $servicePassword,
                "--serverport", $Port
            )

            if ($PSCmdlet.ShouldProcess($InstallerPath, "Install PostgreSQL")) {
                $process = Start-Process -FilePath $InstallerPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
                if ($process.ExitCode -ne 0) {
                    throw "PostgreSQL installer exited with code $($process.ExitCode)."
                }
            }

            $appPassword = New-ITAdminDatabasePassword
            $psqlPath = Find-ITAdminPsqlExecutable
            if ($null -eq $psqlPath) {
                throw "psql.exe was not found after PostgreSQL installation. Create database/user manually, then rerun with DatabaseMode Existing."
            }

            $escapedAppPassword = $appPassword.Replace("'", "''")
            $createRoleSql = @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$User') THEN
        CREATE ROLE $User LOGIN PASSWORD '$escapedAppPassword';
    END IF;
END
`$`$;
"@

            $env:PGPASSWORD = $superPassword
            try {
                & $psqlPath -U postgres -h $HostNameValue -p $Port -d postgres -v ON_ERROR_STOP=1 -c $createRoleSql | Out-Null

                $databaseExists = (& $psqlPath -U postgres -h $HostNameValue -p $Port -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$Name'").Trim()
                if ($databaseExists -ne "1") {
                    & $psqlPath -U postgres -h $HostNameValue -p $Port -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $Name OWNER $User;" | Out-Null
                }
            }
            finally {
                Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
            }

            return New-ITAdminPostgreSqlConnectionString `
                -HostNameValue $HostNameValue `
                -Port $Port `
                -Name $Name `
                -User $User `
                -Password $appPassword
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
        $existingJwtKey = Get-ITAdminEffectiveRuntimeVariable `
            -Name "ITADMIN_Jwt__Key" `
            -AppPoolEnvironment $AppPoolEnvironment `
            -MachineEnvironment $MachineEnvironment
        $existingSetupKeyHash = Get-ITAdminEffectiveRuntimeVariable `
            -Name "ITADMIN_Setup__SetupKeyHash" `
            -AppPoolEnvironment $AppPoolEnvironment `
            -MachineEnvironment $MachineEnvironment
    }

    $variables["ASPNETCORE_ENVIRONMENT"] = $AspNetCoreEnvironment

    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
        $variables["ITADMIN_ConnectionStrings__DefaultConnection"] = $ConnectionString
        Write-ITAdminBootstrapMessage -Message ("Configured ITADMIN_ConnectionStrings__DefaultConnection: {0}" -f (Hide-ITAdminConnectionString -ConnectionString $ConnectionString))
    }
    elseif (-not $OverwriteRuntimeConfig) {
        $existingConnectionString = Get-ITAdminEffectiveRuntimeVariable `
            -Name "ITADMIN_ConnectionStrings__DefaultConnection" `
            -AppPoolEnvironment $AppPoolEnvironment `
            -MachineEnvironment $MachineEnvironment
        if (-not [string]::IsNullOrWhiteSpace($existingConnectionString)) {
            $variables["ITADMIN_ConnectionStrings__DefaultConnection"] = $existingConnectionString
            $connectionSource = if ($AppPoolEnvironment.ContainsKey("ITADMIN_ConnectionStrings__DefaultConnection")) { "AppPool" } else { "MachineLegacy" }
            Write-ITAdminBootstrapMessage -Message ("Preserved ITADMIN_ConnectionStrings__DefaultConnection from {0}: {1}" -f $connectionSource, (Hide-ITAdminConnectionString -ConnectionString $existingConnectionString))
        }
    }

    if ($OverwriteRuntimeConfig -or [string]::IsNullOrWhiteSpace($existingJwtKey)) {
        $jwtKey = New-ITAdminCryptographicSecret -ByteLength 64 -Format "Base64Url"
        $variables["ITADMIN_Jwt__Key"] = $jwtKey
        Write-ITAdminBootstrapMessage -Message "Configured new ITADMIN_Jwt__Key."
    }
    else {
        $variables["ITADMIN_Jwt__Key"] = $existingJwtKey
        $jwtSource = if ($AppPoolEnvironment.ContainsKey("ITADMIN_Jwt__Key")) { "AppPool" } else { "MachineLegacy" }
        Write-ITAdminBootstrapMessage -Message "Preserved existing ITADMIN_Jwt__Key from $jwtSource."
    }

    $variables["ITADMIN_Jwt__Issuer"] = "ITAdmin"
    $variables["ITADMIN_Jwt__Audience"] = "ITAdmin.Client"

    if ($OverwriteRuntimeConfig -or [string]::IsNullOrWhiteSpace($existingSetupKeyHash)) {
        $setupMaterial = New-ITAdminSetupKeyMaterial
        $variables["ITADMIN_Setup__SetupKeyHash"] = $setupMaterial.SetupKeyHash
        if ($null -ne $SetupKeyPlaintextToShow) {
            $SetupKeyPlaintextToShow.Value = $setupMaterial.PlaintextSetupKey
        }

        Write-ITAdminBootstrapMessage -Message "Configured new ITADMIN_Setup__SetupKeyHash."
    }
    else {
        $variables["ITADMIN_Setup__SetupKeyHash"] = $existingSetupKeyHash
        $setupSource = if ($AppPoolEnvironment.ContainsKey("ITADMIN_Setup__SetupKeyHash")) { "AppPool" } else { "MachineLegacy" }
        Write-ITAdminBootstrapMessage -Message "Preserved existing ITADMIN_Setup__SetupKeyHash from $setupSource. Plaintext setup key is not available."
    }

    $variables["ITADMIN_DataProtection__ApplicationName"] = $DataProtectionApplicationName
    $variables["ITADMIN_DataProtection__KeysPath"] = $DataProtectionKeysPath

    if (-not [string]::IsNullOrWhiteSpace($DataProtectionCertificateThumbprint)) {
        $variables["ITADMIN_DataProtection__CertificateThumbprint"] = $DataProtectionCertificateThumbprint.Replace(" ", "").ToUpperInvariant()
        Write-ITAdminBootstrapMessage -Message "Configured ITADMIN_DataProtection__CertificateThumbprint."
    }
    else {
        $variables["ITADMIN_DataProtection__CertificateThumbprint"] = ""
        Write-ITAdminBootstrapMessage -Message "DataProtection certificate thumbprint was not provided. ITADMIN_DataProtection__CertificateThumbprint will not be set."
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
        [string]$PhysicalPath
    )

    $webConfigPath = Join-Path $PhysicalPath "web.config"
    $apiDllPath = Join-Path $PhysicalPath "ITAdmin.Api.dll"
    $apiExePath = Join-Path $PhysicalPath "ITAdmin.Api.exe"
    $indexPath = Join-Path $PhysicalPath "wwwroot\index.html"

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
        [string]$PhysicalPath,

        [Parameter(Mandatory = $true)]
        [string]$SiteName,

        [Parameter(Mandatory = $true)]
        [string]$AppPoolName,

        [Parameter(Mandatory = $true)]
        [string]$AppPoolIdentityName
    )

    Write-ITAdminBootstrapMessage -Message "Starting package deployment from: $PackageArchivePath"

    if ($PSCmdlet.ShouldProcess($PhysicalPath, "Deploy ITAdmin package")) {
        if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
            Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
        }

        if (Test-Path "IIS:\AppPools\$AppPoolName") {
            Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
        }

        if (-not (Test-Path -LiteralPath $PhysicalPath)) {
            New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
        }

        $offlineFile = Join-Path $PhysicalPath "app_offline.htm"
        Set-Content -LiteralPath $offlineFile -Value "<html><body><h1>ITAdmin deployment in progress</h1></body></html>" -Encoding UTF8

        Get-ChildItem -LiteralPath $PhysicalPath -Force |
            Where-Object { $_.Name -ne "app_offline.htm" } |
            Remove-Item -Recurse -Force

        Expand-Archive -LiteralPath $PackageArchivePath -DestinationPath $PhysicalPath -Force

        Test-ITAdminDeployedPackage -PhysicalPath $PhysicalPath

        & icacls $PhysicalPath /grant "${AppPoolIdentityName}:(OI)(CI)RX" /T | Out-Null

        Write-ITAdminBootstrapMessage -Message "Package deployment completed."
    }
}

function Invoke-ITAdminDatabaseMigration {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Manual", "SqlFile", "Skip")]
        [string]$Mode,

        [Parameter()]
        [string]$SqlFilePath,

        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [string]$ConnectionString
    )

    switch ($Mode) {
        "Skip" {
            Write-ITAdminBootstrapMessage -Message "Database migration skipped."
            return "Skipped"
        }

        "Manual" {
            Write-ITAdminBootstrapMessage -Message "Database migration is expected to be applied manually."
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

            Write-ITAdminBootstrapMessage -Message "Applying database migration from SQL file: $SqlFilePath"
            if ($PSCmdlet.ShouldProcess($SqlFilePath, "Apply SQL migration script")) {
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
        return "[no recent IIS/.NET application event log entries found]"
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

        Write-ITAdminBootstrapMessage -Message "Smoke test attempt $attempt/$MaxAttempts failed for $uri. Retrying in $DelaySeconds second(s)..."
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

function Start-ITAdminWebStack {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SiteName,

        [Parameter(Mandatory = $true)]
        [string]$AppPoolName
    )

    if ($PSCmdlet.ShouldProcess($AppPoolName, "Start app pool")) {
        Start-WebAppPool -Name $AppPoolName
    }

    if ($PSCmdlet.ShouldProcess($SiteName, "Start website")) {
        Start-Website -Name $SiteName
    }
}

function Remove-ITAdminAppOfflineFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PhysicalPath
    )

    $offlineFile = Join-Path $PhysicalPath "app_offline.htm"
    if (Test-Path -LiteralPath $offlineFile) {
        Remove-Item -LiteralPath $offlineFile -Force
    }
}

function Show-ITAdminFinalSummary {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Summary
    )

    Write-ITAdminBootstrapMessage -Message "== ITAdmin installation summary =="
    Write-ITAdminBootstrapMessage -Message ("Site name: {0}" -f $Summary.SiteName)
    Write-ITAdminBootstrapMessage -Message ("App pool name: {0}" -f $Summary.AppPoolName)
    Write-ITAdminBootstrapMessage -Message ("Hostname: {0}" -f $Summary.HostName)
    Write-ITAdminBootstrapMessage -Message ("Physical path: {0}" -f $Summary.PhysicalPath)
    Write-ITAdminBootstrapMessage -Message ("Runtime root: {0}" -f $Summary.RuntimeRoot)
    Write-ITAdminBootstrapMessage -Message ("Setup URL: {0}" -f $Summary.SetupUrl)

    if ($null -ne $Summary.DatabaseSummary) {
        Write-ITAdminBootstrapMessage -Message $Summary.DatabaseSummary
    }

    Write-ITAdminBootstrapMessage -Message ("Migration: {0}" -f $Summary.MigrationResult)
    Write-ITAdminBootstrapMessage -Message ("Smoke test: {0}" -f $Summary.SmokeTestResult)

    if (-not [string]::IsNullOrWhiteSpace($Summary.SetupKeyPlaintext)) {
        Write-Host ""
        Write-Host "IMPORTANT: Save the setup key below securely. It is shown once and is not stored on the server."
        Write-Host ("Setup key: {0}" -f $Summary.SetupKeyPlaintext)
        Write-Host ""
    }
    elseif ($Summary.SetupKeyPreserved) {
        Write-ITAdminBootstrapMessage -Message "Existing setup key hash was preserved. Plaintext setup key is not available."
    }
}

if ($PSVersionTable.PSVersion.Major -lt 5) {
    throw "PowerShell 5.1 or later is required."
}

if (-not (Test-ITAdminAdministrator)) {
    throw "This script must be run from an elevated PowerShell session."
}

Write-ITAdminBootstrapMessage -Message "== ITAdmin installation started =="

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
    $HostName = Read-ITAdminPromptValue -Prompt "Host name (e.g. itadmin.domain.local)" -DefaultValue $null
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
    Write-ITAdminBootstrapMessage -Message "Database mode:"
    Write-ITAdminBootstrapMessage -Message "  1. Existing PostgreSQL"
    Write-ITAdminBootstrapMessage -Message "  2. Skip database configuration"
    $databaseChoice = Read-ITAdminPromptValue -Prompt "Select database mode [1/2]" -DefaultValue "2"
    switch ($databaseChoice) {
        "1" { $DatabaseMode = "Existing" }
        default { $DatabaseMode = "Skip" }
    }
}

if ($SkipMigration.IsPresent) {
    $MigrationMode = "Skip"
}

if (-not (Test-ITAdminParameterWasBound -Name "MigrationMode") -or [string]::IsNullOrWhiteSpace($MigrationMode)) {
    Write-ITAdminBootstrapMessage -Message "Migration mode:"
    Write-ITAdminBootstrapMessage -Message "  1. Manual - SQL migration applied or will be applied manually on the database"
    Write-ITAdminBootstrapMessage -Message "  2. SqlFile - Apply SQL file from this server"
    Write-ITAdminBootstrapMessage -Message "  3. Skip - Skip migration step entirely"
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
    Write-ITAdminBootstrapMessage -Message "Existing runtime configuration will be preserved and migrated to app pool environment variables where needed."
}

$connectionString = Resolve-ITAdminDatabaseConnectionString `
    -Mode $DatabaseMode `
    -InstallerPath $PostgreSqlInstallerPath `
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

Write-ITAdminBootstrapMessage -Message "Applying runtime configuration to app pool environment variables."
Set-ITAdminAppPoolEnvironmentVariables -PoolName $AppPoolName -Variables $runtimeVariables

$effectiveAppPoolEnvironment = Get-ITAdminAppPoolEnvironmentVariables -PoolName $AppPoolName
Write-ITAdminBootstrapMessage -Message "Effective runtime configuration (app pool environment):"
foreach ($entry in ($effectiveAppPoolEnvironment.GetEnumerator() | Sort-Object Name)) {
    Write-ITAdminBootstrapMessage -Message ("  {0}={1}" -f $entry.Key, (Format-ITAdminRuntimeVariableForDisplay -Name $entry.Key -Value $entry.Value))
}

$effectiveConnectionString = Get-ITAdminEffectiveRuntimeVariable `
    -Name "ITADMIN_ConnectionStrings__DefaultConnection" `
    -AppPoolEnvironment $effectiveAppPoolEnvironment `
    -MachineEnvironment @{}

Deploy-ITAdminPackage `
    -PackageArchivePath $PackagePath `
    -PhysicalPath $PhysicalPath `
    -SiteName $SiteName `
    -AppPoolName $AppPoolName `
    -AppPoolIdentityName $appPoolIdentity

$migrationResult = Invoke-ITAdminDatabaseMigration `
    -Mode $MigrationMode `
    -SqlFilePath $MigrationSqlPath `
    -ConnectionString $effectiveConnectionString

Remove-ITAdminAppOfflineFile -PhysicalPath $PhysicalPath
Start-ITAdminWebStack -SiteName $SiteName -AppPoolName $AppPoolName

$baseScheme = if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) { "http" } else { "https" }
$baseUrl = "{0}://{1}" -f $baseScheme, $HostName
$setupUrl = "{0}/setup" -f $baseUrl

$smokeTestResult = "Skipped"
if (-not $SkipSmokeTest.IsPresent) {
    $smokeTest = Test-ITAdminSetupStatusEndpoint -BaseUrl $baseUrl -RuntimeRootPath $RuntimeRoot
    if ($smokeTest.Success) {
        $smokeTestResult = "Passed (HTTP 200)"
        Write-ITAdminBootstrapMessage -Message "Smoke test passed for $($smokeTest.Uri)"
    }
    else {
        Write-ITAdminBootstrapMessage -Message "Smoke test failed for $($smokeTest.Uri)" -Level Error
        if ($null -ne $smokeTest.StatusCode) {
            Write-ITAdminBootstrapMessage -Message ("HTTP status: {0}" -f $smokeTest.StatusCode) -Level Error
        }

        if (-not [string]::IsNullOrWhiteSpace($smokeTest.Body)) {
            Write-ITAdminBootstrapMessage -Message ("Response body: {0}" -f $smokeTest.Body) -Level Error
        }

        if (-not [string]::IsNullOrWhiteSpace($smokeTest.Error)) {
            Write-ITAdminBootstrapMessage -Message ("Last error: {0}" -f $smokeTest.Error) -Level Error
        }

        Write-ITAdminBootstrapMessage -Message "Recent application log tail:" -Level Error
        Write-Host $smokeTest.LogTail
        Write-ITAdminBootstrapMessage -Message "Recent Windows application event log entries:" -Level Error
        Write-Host $smokeTest.EventLogTail

        $migrationHint = ""
        if ($MigrationMode -eq "Manual") {
            $migrationHint = " Verify that database migration was applied manually before retrying."
        }

        throw "Smoke test failed.$migrationHint Installation did not complete successfully."
    }
}
else {
    Write-ITAdminBootstrapMessage -Message "Smoke test skipped because -SkipSmokeTest was specified." -Level Warning
}

$databaseSummary = $null
if (-not [string]::IsNullOrWhiteSpace($effectiveConnectionString)) {
    $dbParts = ConvertFrom-ITAdminPostgreSqlConnectionString -ConnectionString $effectiveConnectionString
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

Write-ITAdminBootstrapMessage -Message "== ITAdmin installation completed =="
