#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter()]
    [string]$SiteName = "ITAdmin",

    [Parameter()]
    [string]$AppPoolName = "ITAdmin",

    [Parameter(Mandatory = $true)]
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
    [string]$DatabaseMode = "Skip",

    [Parameter()]
    [string]$CertificateThumbprint,

    [Parameter()]
    [string]$PostgreSqlInstallerPath,

    [Parameter()]
    [int]$PostgreSqlPort = 5432,

    [Parameter()]
    [string]$DatabaseHost = "localhost",

    [Parameter()]
    [string]$DatabaseName = "itadmin",

    [Parameter()]
    [string]$DatabaseUser = "itadmin_app"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"


$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptRoot "new-itadmin-secret.ps1")

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

function Set-ITAdminMachineEnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    [Environment]::SetEnvironmentVariable($Name, $Value, "Machine")
}

function Get-ITAdminMachineEnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return [Environment]::GetEnvironmentVariable($Name, "Machine")
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
    $result = Install-WindowsFeature -Name $missingFeatures -IncludeManagementTools
    if ($null -ne $result -and $result.RestartNeeded -eq "Yes") {
        Write-ITAdminBootstrapMessage -Message "A server restart may be required to complete IIS feature installation." -Level Warning
    }
}

function Test-ITAdminAspNetCoreHostingBundle {
    $hostingDllPath = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    if (Test-Path -LiteralPath $hostingDllPath) {
        Write-ITAdminBootstrapMessage -Message "ASP.NET Core Hosting Bundle appears to be installed."
        return $true
    }

    Write-ITAdminBootstrapMessage -Message "ASP.NET Core Hosting Bundle was not detected (aspnetcorev2.dll missing). Install the bundle manually before publishing ITAdmin." -Level Warning
    return $false
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
    $backupsPath = Join-Path $RuntimeRootPath "Backups"

    $paths = @(
        $RuntimeRootPath,
        $dataProtectionPath,
        $logsPath,
        $backupsPath,
        $PhysicalSitePath
    )

    foreach ($path in $paths) {
        if (-not (Test-Path -LiteralPath $path)) {
            New-Item -ItemType Directory -Path $path -Force | Out-Null
            Write-ITAdminBootstrapMessage -Message "Created directory: $path"
        }
    }

    foreach ($path in @($dataProtectionPath, $logsPath)) {
        & icacls $path /grant "${AppPoolIdentityName}:(OI)(CI)M" /T | Out-Null
    }

    & icacls $PhysicalSitePath /grant "${AppPoolIdentityName}:(OI)(CI)RX" /T | Out-Null
}

function Set-ITAdminAppPoolEnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName,

        [Parameter(Mandatory = $true)]
        [string]$VariableName,

        [Parameter(Mandatory = $true)]
        [string]$VariableValue
    )

    $filterPath = "system.applicationHost/applicationPools/add[@name='$PoolName']/environmentVariables"
    $existing = Get-WebConfigurationProperty `
        -PSPath "MACHINE/WEBROOT/APPHOST" `
        -Filter $filterPath `
        -Name "." `
        -ErrorAction SilentlyContinue

    if ($null -ne $existing) {
        Clear-WebConfiguration `
            -PSPath "MACHINE/WEBROOT/APPHOST" `
            -Filter $filterPath `
            -ErrorAction SilentlyContinue
    }

    Add-WebConfigurationProperty `
        -PSPath "MACHINE/WEBROOT/APPHOST" `
        -Filter $filterPath `
        -Name "." `
        -Value @{
            name = $VariableName
            value = $VariableValue
        } | Out-Null
}

function Ensure-ITAdminAppPool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PoolName,

        [Parameter(Mandatory = $true)]
        [string]$AspNetCoreEnvironment
    )

    if (-not (Test-Path "IIS:\AppPools\$PoolName")) {
        Write-ITAdminBootstrapMessage -Message "Creating app pool: $PoolName"
        New-WebAppPool -Name $PoolName | Out-Null
    }
    else {
        Write-ITAdminBootstrapMessage -Message "App pool exists: $PoolName"
    }

    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name enable32BitAppOnWin64 -Value $false
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name startMode -Value "AlwaysRunning"
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name autoStart -Value $true
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.loadUserProfile -Value $true

    Set-ITAdminAppPoolEnvironmentVariable `
        -PoolName $PoolName `
        -VariableName "ASPNETCORE_ENVIRONMENT" `
        -VariableValue $AspNetCoreEnvironment
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
        New-Website `
            -Name $Site `
            -PhysicalPath $SitePhysicalPath `
            -ApplicationPool $PoolName `
            -Port 80 `
            -HostHeader $SiteHostName | Out-Null
    }
    else {
        Write-ITAdminBootstrapMessage -Message "IIS site exists: $Site"
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

        Write-ITAdminBootstrapMessage -Message "Applying HTTPS certificate binding using store $($certInfo.StoreName)"
        $httpsBinding.AddSslCertificate($certInfo.Thumbprint, $certInfo.StoreName)
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
        [string]$User
    )

    switch ($Mode) {
        "Skip" {
            $existing = Get-ITAdminMachineEnvironmentVariable -Name "ITADMIN_ConnectionStrings__DefaultConnection"
            if ([string]::IsNullOrWhiteSpace($existing)) {
                Write-ITAdminBootstrapMessage -Message "DatabaseMode is Skip and no existing ITADMIN_ConnectionStrings__DefaultConnection was found." -Level Warning
            }
            else {
                Write-ITAdminBootstrapMessage -Message ("Keeping existing connection string: {0}" -f (Hide-ITAdminConnectionString -ConnectionString $existing))
            }
            return $existing
        }

        "Existing" {
            $existingHost = Read-Host -Prompt "PostgreSQL host [$HostNameValue]"
            if (-not [string]::IsNullOrWhiteSpace($existingHost)) {
                $HostNameValue = $existingHost
            }

            $existingPortText = Read-Host -Prompt "PostgreSQL port [$Port]"
            if (-not [string]::IsNullOrWhiteSpace($existingPortText)) {
                $Port = [int]$existingPortText
            }

            $existingDatabase = Read-Host -Prompt "Database name [$Name]"
            if (-not [string]::IsNullOrWhiteSpace($existingDatabase)) {
                $Name = $existingDatabase
            }

            $existingUser = Read-Host -Prompt "Database user [$User]"
            if (-not [string]::IsNullOrWhiteSpace($existingUser)) {
                $User = $existingUser
            }

            $passwordSecure = Read-ITAdminSecurePrompt -Prompt "Database password"
            $password = ConvertFrom-ITAdminSecureString -SecureString $passwordSecure

            return New-ITAdminPostgreSqlConnectionString `
                -HostNameValue $HostNameValue `
                -Port $Port `
                -Name $Name `
                -User $User `
                -Password $password
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

            $process = Start-Process -FilePath $InstallerPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
            if ($process.ExitCode -ne 0) {
                throw "PostgreSQL installer exited with code $($process.ExitCode)."
            }

            $appPassword = New-ITAdminDatabasePassword
            $psqlPath = Find-ITAdminPsqlExecutable
            if ($null -eq $psqlPath) {
                Write-ITAdminBootstrapMessage -Message "psql.exe was not found after PostgreSQL installation. Create database/user manually, then rerun with DatabaseMode Existing." -Level Warning
                return $null
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

function Set-ITAdminRuntimeEnvironment {
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

        [Parameter()]
        [AllowNull()]
        [string]$SetupKeyHash
    )

    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
        $existingConnectionString = Get-ITAdminMachineEnvironmentVariable -Name "ITADMIN_ConnectionStrings__DefaultConnection"
        if ([string]::IsNullOrWhiteSpace($existingConnectionString)) {
            Set-ITAdminMachineEnvironmentVariable -Name "ITADMIN_ConnectionStrings__DefaultConnection" -Value $ConnectionString
            Write-ITAdminBootstrapMessage -Message ("Configured ITADMIN_ConnectionStrings__DefaultConnection: {0}" -f (Hide-ITAdminConnectionString -ConnectionString $ConnectionString))
        }
        else {
            Write-ITAdminBootstrapMessage -Message ("Keeping existing ITADMIN_ConnectionStrings__DefaultConnection: {0}" -f (Hide-ITAdminConnectionString -ConnectionString $existingConnectionString))
        }
    }

    $existingJwtKey = Get-ITAdminMachineEnvironmentVariable -Name "ITADMIN_Jwt__Key"
    if ([string]::IsNullOrWhiteSpace($existingJwtKey)) {
        $jwtKey = New-ITAdminCryptographicSecret -ByteLength 64 -Format "Base64Url"
        Set-ITAdminMachineEnvironmentVariable -Name "ITADMIN_Jwt__Key" -Value $jwtKey
        Write-ITAdminBootstrapMessage -Message ("Configured ITADMIN_Jwt__Key: {0}" -f (Hide-ITAdminSecretValue -Value $jwtKey))
    }
    else {
        Write-ITAdminBootstrapMessage -Message "Keeping existing ITADMIN_Jwt__Key."
    }

    Set-ITAdminMachineEnvironmentVariable -Name "ITADMIN_Jwt__Issuer" -Value "ITAdmin"
    Set-ITAdminMachineEnvironmentVariable -Name "ITADMIN_Jwt__Audience" -Value "ITAdmin.Client"
    Write-ITAdminBootstrapMessage -Message "Configured ITADMIN_Jwt__Issuer=ITAdmin"
    Write-ITAdminBootstrapMessage -Message "Configured ITADMIN_Jwt__Audience=ITAdmin.Client"

    $existingSetupKeyHash = Get-ITAdminMachineEnvironmentVariable -Name "ITADMIN_Setup__SetupKeyHash"
    if ([string]::IsNullOrWhiteSpace($existingSetupKeyHash)) {
        if ([string]::IsNullOrWhiteSpace($SetupKeyHash)) {
            throw "Setup key hash is required when ITADMIN_Setup__SetupKeyHash is not already configured."
        }

        Set-ITAdminMachineEnvironmentVariable -Name "ITADMIN_Setup__SetupKeyHash" -Value $SetupKeyHash
        Write-ITAdminBootstrapMessage -Message ("Configured ITADMIN_Setup__SetupKeyHash: {0}" -f (Hide-ITAdminSecretValue -Value $SetupKeyHash))
    }
    else {
        Write-ITAdminBootstrapMessage -Message "Keeping existing ITADMIN_Setup__SetupKeyHash."
    }

    Set-ITAdminMachineEnvironmentVariable -Name "ITADMIN_DataProtection__ApplicationName" -Value $DataProtectionApplicationName
    Set-ITAdminMachineEnvironmentVariable -Name "ITADMIN_DataProtection__KeysPath" -Value $DataProtectionKeysPath
    Write-ITAdminBootstrapMessage -Message "Configured ITADMIN_DataProtection__ApplicationName=$DataProtectionApplicationName"
    Write-ITAdminBootstrapMessage -Message "Configured ITADMIN_DataProtection__KeysPath=$DataProtectionKeysPath"

    if (-not [string]::IsNullOrWhiteSpace($DataProtectionCertificateThumbprint)) {
        $normalizedThumbprint = $DataProtectionCertificateThumbprint.Replace(" ", "").ToUpperInvariant()
        Set-ITAdminMachineEnvironmentVariable -Name "ITADMIN_DataProtection__CertificateThumbprint" -Value $normalizedThumbprint
    }
}

if (-not (Test-ITAdminAdministrator)) {
    throw "This script must be run from an elevated PowerShell session."
}

Write-ITAdminBootstrapMessage -Message "== ITAdmin production server bootstrap started =="

Import-Module WebAdministration -ErrorAction Stop

Ensure-ITAdminWindowsFeatures
Test-ITAdminAspNetCoreHostingBundle | Out-Null

$appPoolIdentity = "IIS AppPool\$AppPoolName"
Ensure-ITAdminRuntimeDirectories `
    -RuntimeRootPath $RuntimeRoot `
    -PhysicalSitePath $PhysicalPath `
    -AppPoolIdentityName $appPoolIdentity

Ensure-ITAdminAppPool -PoolName $AppPoolName -AspNetCoreEnvironment $EnvironmentName
Ensure-ITAdminSite `
    -Site $SiteName `
    -PoolName $AppPoolName `
    -SiteHostName $HostName `
    -SitePhysicalPath $PhysicalPath `
    -HttpsCertificateThumbprint $CertificateThumbprint

$connectionString = Resolve-ITAdminDatabaseConnectionString `
    -Mode $DatabaseMode `
    -InstallerPath $PostgreSqlInstallerPath `
    -Port $PostgreSqlPort `
    -HostNameValue $DatabaseHost `
    -Name $DatabaseName `
    -User $DatabaseUser

$setupMaterial = New-ITAdminSetupKeyMaterial
$dataProtectionApplicationName = "ITAdmin-$EnvironmentName"
$dataProtectionKeysPath = Join-Path $RuntimeRoot "DataProtection-Keys"
$existingSetupKeyHash = Get-ITAdminMachineEnvironmentVariable -Name "ITADMIN_Setup__SetupKeyHash"
$setupKeyHashToApply = $null
$setupKeyPlaintextToShow = $null

if ([string]::IsNullOrWhiteSpace($existingSetupKeyHash)) {
    $setupKeyHashToApply = $setupMaterial.SetupKeyHash
    $setupKeyPlaintextToShow = $setupMaterial.PlaintextSetupKey
}

Set-ITAdminRuntimeEnvironment `
    -AspNetCoreEnvironment $EnvironmentName `
    -ConnectionString $connectionString `
    -DataProtectionApplicationName $dataProtectionApplicationName `
    -DataProtectionKeysPath $dataProtectionKeysPath `
    -DataProtectionCertificateThumbprint $CertificateThumbprint `
    -SetupKeyHash $setupKeyHashToApply

$setupScheme = if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) { "http" } else { "https" }
$setupUrl = "{0}://{1}/setup" -f $setupScheme, $HostName

Write-ITAdminBootstrapMessage -Message "== ITAdmin production server bootstrap completed =="
Write-ITAdminBootstrapMessage -Message "Site: $SiteName"
Write-ITAdminBootstrapMessage -Message "App pool: $AppPoolName"
Write-ITAdminBootstrapMessage -Message "Physical path: $PhysicalPath"
Write-ITAdminBootstrapMessage -Message "Runtime root: $RuntimeRoot"
Write-ITAdminBootstrapMessage -Message "Setup URL: $setupUrl"

if (-not [string]::IsNullOrWhiteSpace($setupKeyPlaintextToShow)) {
    Write-Host ""
    Write-Host "IMPORTANT: Save the setup key below securely. It is shown once and is not stored on the server."
    Write-Host ("Setup key: {0}" -f $setupKeyPlaintextToShow)
    Write-Host ""
}
else {
    Write-ITAdminBootstrapMessage -Message "Existing setup key hash was preserved. Plaintext setup key is not available."
}

Write-ITAdminBootstrapMessage -Message "Publish/deploy is a separate step. Use deploy scripts only after this bootstrap completes."
