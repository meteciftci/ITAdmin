<#
.SYNOPSIS
    Read-only Active Directory / network readiness check for an ITAdmin application server.

.DESCRIPTION
    Verifies, from the machine ITAdmin will run on, that everything the Primary Authentication
    Directory and the AD Management module depend on is reachable and healthy:

      * the machine's domain secure channel
      * DC locator for the domain
      * DNS resolution for each preferred domain controller
      * TCP 636 (LDAPS) reachability per domain controller
      * the LDAPS certificate each DC presents: trust chain, hostname match, validity window
      * optionally, a real LDAPS bind + Base DN read using a supplied service credential
      * TCP reachability of the PostgreSQL endpoint

    The script only reads. It never writes registry, IIS, or domain configuration, never changes
    the trust relationship, and never prints a password. Credentials are taken as PSCredential
    objects so nothing sensitive appears on the command line or in the output.

    This script carries no organization-specific values. Domain, domain controllers, and Base DN
    are discovered from the machine's own Active Directory membership unless you override them.
    Everything site-specific (PostgreSQL host, application FQDN, credentials) is supplied at run
    time by the operator.

.PARAMETER DomainFqdn
    AD DNS domain to check. Defaults to the domain this computer is joined to.

.PARAMETER DomainControllers
    Domain controllers to check. Defaults to those advertised by DC locator for -DomainFqdn.

.PARAMETER BaseDn
    Directory Base DN. Defaults to the defaultNamingContext derived from -DomainFqdn.

.PARAMETER PostgreSqlHost
    PostgreSQL host to test reachability against. Omit to skip the database check.

.PARAMETER WebHost
    Application FQDN to test HTTPS against. Omit (or use -SkipWebHostCheck) to skip - before the
    site is deployed the check would fail simply because nothing is listening yet.

.EXAMPLE
    # Domain-joined server, everything discovered, AD/network checks only.
    .\Test-ItAdminAdReadiness.ps1

.EXAMPLE
    # Explicit environment, including a real service-account LDAPS bind against both DCs.
    .\Test-ItAdminAdReadiness.ps1 `
        -DomainFqdn corp.example.com `
        -DomainControllers dc1.corp.example.com, dc2.corp.example.com `
        -PostgreSqlHost db.corp.example.com `
        -WebHost itadmin.example.com `
        -ServiceCredential (Get-Credential CORP\svc_itadmin)
#>
[CmdletBinding()]
param(
    [string]$DomainFqdn,
    [string[]]$DomainControllers,
    [string]$BaseDn,
    [string]$PostgreSqlHost,
    [int]$PostgreSqlPort = 5432,
    [string]$WebHost,
    [switch]$SkipWebHostCheck,
    [PSCredential]$ServiceCredential,
    [PSCredential]$TestUserCredential
)

# Deliberately not StrictMode: Resolve-DnsName returns mixed record types where IPAddress is
# absent on some, and every check below is individually guarded and reported rather than fatal.
$ErrorActionPreference = "Stop"


function New-CheckResult {
    param([string]$Check, [bool]$Success, [string]$Detail)
    [pscustomobject]@{
        Check  = $Check
        Status = if ($Success) { "OK" } else { "FAILED" }
        Detail = $Detail
    }
}

function Get-HostAddresses {
    param([string]$Name)
    $records = Resolve-DnsName -Name $Name -ErrorAction Stop
    return (@($records | Where-Object { $_.PSObject.Properties.Match('IPAddress').Count -gt 0 -and $_.IPAddress } |
        Select-Object -ExpandProperty IPAddress) -join ", ")
}

function Test-TcpPort {
    param([string]$ComputerName, [int]$Port, [string]$Label)
    $tcp = Test-NetConnection -ComputerName $ComputerName -Port $Port `
        -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    $succeeded = ($null -ne $tcp) -and $tcp.TcpTestSucceeded
    $detail = if ($null -eq $tcp) { "No response (name could not be resolved)" } else { "RemoteAddress=$($tcp.RemoteAddress)" }
    return [pscustomobject]@{
        Succeeded = $succeeded
        Result    = New-CheckResult $Label $succeeded $detail
    }
}

function Get-JoinedDomainFqdn {
    <#
        The AD DNS domain this computer is joined to. Discovery only - never a baked-in default,
        so the script behaves correctly in any forest without repository changes.
    #>
    try {
        $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop
        if ($computerSystem.PartOfDomain -and -not [string]::IsNullOrWhiteSpace($computerSystem.Domain)) {
            return $computerSystem.Domain
        }
    }
    catch {
        Write-Verbose "Domain discovery via Win32_ComputerSystem failed: $($_.Exception.Message)"
    }

    return $null
}

function Get-LocatedDomainControllers {
    <#
        Domain controllers advertised by DC locator for the given domain. Returns an empty array
        when discovery is unavailable, so the caller can ask the operator instead of guessing.
    #>
    param([string]$Domain)

    $located = New-Object System.Collections.Generic.List[string]
    try {
        $records = Resolve-DnsName -Name "_ldap._tcp.dc._msdcs.$Domain" -Type SRV -ErrorAction Stop
        foreach ($record in $records) {
            if ($record.PSObject.Properties.Match('NameTarget').Count -gt 0 -and $record.NameTarget) {
                if (-not $located.Contains($record.NameTarget)) {
                    $located.Add($record.NameTarget)
                }
            }
        }
    }
    catch {
        Write-Verbose "SRV-record DC discovery failed: $($_.Exception.Message)"
    }

    return $located.ToArray()
}

function ConvertTo-BaseDnFromDomain {
    <# corp.example.com -> DC=corp,DC=example,DC=com #>
    param([string]$Domain)

    if ([string]::IsNullOrWhiteSpace($Domain)) {
        return $null
    }

    return (($Domain -split '\.' | Where-Object { $_ } | ForEach-Object { "DC=$_" }) -join ',')
}

function Initialize-LdapAssembly {
    <#
        Windows PowerShell 5.1 does not load System.DirectoryServices.Protocols on demand the way
        PowerShell 7 does, so a clean 5.1 session cannot resolve LdapConnection without an explicit
        Add-Type. Loading it here means the operator never has to run Add-Type by hand.
        Returns $true when the LDAP types are usable.
    #>
    if ('System.DirectoryServices.Protocols.LdapConnection' -as [type]) {
        return $true
    }

    try {
        Add-Type -AssemblyName System.DirectoryServices.Protocols -ErrorAction Stop
    }
    catch {
        Write-Verbose "Add-Type for System.DirectoryServices.Protocols failed: $($_.Exception.Message)"
        return $false
    }

    return [bool]('System.DirectoryServices.Protocols.LdapConnection' -as [type])
}

function Test-TlsEndpoint {
    param([string]$HostName, [int]$Port)

    $client = [System.Net.Sockets.TcpClient]::new()
    $stream = $null
    # Captured by the validation callback below; must be script-scoped to survive the closure.
    $script:TlsPolicyErrors = [System.Net.Security.SslPolicyErrors]::None
    $script:TlsCertificate = $null

    try {
        $client.Connect($HostName, $Port)

        $callback = {
            param($tlsSender, $remoteCertificate, $chain, $errors)
            $script:TlsPolicyErrors = $errors
            if ($null -ne $remoteCertificate) {
                $script:TlsCertificate =
                    [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($remoteCertificate)
            }
            # Report the platform's verdict as-is. No trust-all, no hostname bypass: this must
            # answer the same question the application's LDAPS connection will ask.
            return $errors -eq [System.Net.Security.SslPolicyErrors]::None
        }

        $stream = [System.Net.Security.SslStream]::new($client.GetStream(), $false, $callback)
        $stream.AuthenticateAsClient($HostName)

        $certificate = $script:TlsCertificate
        $detail = "Protocol=$($stream.SslProtocol); Subject=$($certificate.Subject); " +
                  "Issuer=$($certificate.Issuer); NotAfter=$($certificate.NotAfter.ToString('u')); " +
                  "DaysLeft=$([int]($certificate.NotAfter - (Get-Date)).TotalDays)"
        New-CheckResult "TLS $HostName`:$Port" $true $detail
    }
    catch {
        $reason = switch ($script:TlsPolicyErrors) {
            ([System.Net.Security.SslPolicyErrors]::RemoteCertificateNameMismatch) { "Certificate hostname mismatch" }
            ([System.Net.Security.SslPolicyErrors]::RemoteCertificateChainErrors)  { "Certificate chain not trusted (or expired)" }
            ([System.Net.Security.SslPolicyErrors]::RemoteCertificateNotAvailable) { "No certificate presented" }
            default { "Handshake failed" }
        }
        $subject = if ($null -ne $script:TlsCertificate) { "; Subject=$($script:TlsCertificate.Subject)" } else { "" }
        New-CheckResult "TLS $HostName`:$Port" $false "$reason; PolicyErrors=$($script:TlsPolicyErrors)$subject"
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
        $client.Dispose()
    }
}

function Test-LdapsBind {
    param([string]$Label, [string]$HostName, [PSCredential]$Credential, [string]$SearchBase)

    $connection = $null
    try {
        $identifier = [System.DirectoryServices.Protocols.LdapDirectoryIdentifier]::new($HostName, 636)
        $connection = [System.DirectoryServices.Protocols.LdapConnection]::new($identifier)
        $connection.AuthType = [System.DirectoryServices.Protocols.AuthType]::Basic
        $connection.Credential = $Credential.GetNetworkCredential()
        $connection.SessionOptions.ProtocolVersion = 3
        $connection.SessionOptions.SecureSocketLayer = $true
        $connection.Timeout = [TimeSpan]::FromSeconds(10)
        $connection.Bind()

        $request = [System.DirectoryServices.Protocols.SearchRequest]::new(
            $SearchBase,
            "(objectClass=*)",
            [System.DirectoryServices.Protocols.SearchScope]::Base,
            @("distinguishedName"))
        $response = [System.DirectoryServices.Protocols.SearchResponse]$connection.SendRequest($request)
        New-CheckResult "LDAPS $Label $HostName" ($response.Entries.Count -gt 0) `
            "Bind succeeded; Base DN '$SearchBase' entries=$($response.Entries.Count)"
    }
    catch {
        # Typed catch clauses are avoided here on purpose: a `catch [LdapException]` needs the
        # System.DirectoryServices.Protocols type to resolve, which fails outright on a clean
        # Windows PowerShell 5.1 session. Inspecting the caught exception instead keeps the
        # LDAP error code (49=bad credentials, 81=server down, 91=connect/TLS) without that
        # dependency.
        $exception = $_.Exception.GetBaseException()
        $errorCode = $exception.PSObject.Properties.Match('ErrorCode')
        if ($errorCode.Count -gt 0) {
            New-CheckResult "LDAPS $Label $HostName" $false `
                "LdapErrorCode=$($exception.ErrorCode); $($exception.Message)"
        }
        else {
            New-CheckResult "LDAPS $Label $HostName" $false $exception.Message
        }
    }
    finally {
        if ($null -ne $connection) { $connection.Dispose() }
    }
}

$results = [System.Collections.Generic.List[object]]::new()

# --- Resolve the environment: explicit parameters win, discovery fills the gaps ----------------
if ([string]::IsNullOrWhiteSpace($DomainFqdn)) {
    $DomainFqdn = Get-JoinedDomainFqdn
    if ([string]::IsNullOrWhiteSpace($DomainFqdn)) {
        throw "This computer is not domain-joined and -DomainFqdn was not supplied. " +
              "Re-run with -DomainFqdn <your.ad.domain> (and -DomainControllers if DC locator is unavailable)."
    }
    Write-Verbose "Discovered domain: $DomainFqdn"
}

if ($null -eq $DomainControllers -or $DomainControllers.Count -eq 0) {
    $DomainControllers = Get-LocatedDomainControllers -Domain $DomainFqdn
    if ($DomainControllers.Count -eq 0) {
        throw "No domain controllers could be discovered for '$DomainFqdn'. " +
              "Re-run with -DomainControllers <dc1>,<dc2>."
    }
    Write-Verbose "Discovered domain controllers: $($DomainControllers -join ', ')"
}

if ([string]::IsNullOrWhiteSpace($BaseDn)) {
    $BaseDn = ConvertTo-BaseDnFromDomain -Domain $DomainFqdn
    Write-Verbose "Derived Base DN: $BaseDn"
}

Write-Host "Checking domain '$DomainFqdn' (Base DN '$BaseDn') via: $($DomainControllers -join ', ')"
Write-Host ""

# Fail fast, before any network work, when an LDAPS bind was requested but the directory types
# cannot be loaded - otherwise the run would do every other check and only collapse at the end.
$ldapTypesAvailable = Initialize-LdapAssembly
if (-not $ldapTypesAvailable -and ($null -ne $ServiceCredential -or $null -ne $TestUserCredential)) {
    throw "System.DirectoryServices.Protocols could not be loaded, so the credentialed LDAPS bind " +
          "checks cannot run. Use Windows PowerShell 5.1 or later on a machine with the .NET " +
          "Framework directory services components installed, or re-run without -ServiceCredential " +
          "and -TestUserCredential to perform the DNS/TCP/TLS checks only."
}

try {
    # Read-only: -Repair is deliberately never passed.
    $secureChannel = Test-ComputerSecureChannel -ErrorAction Stop
    $results.Add((New-CheckResult "Domain secure channel" $secureChannel "Computer account trust with $DomainFqdn"))
}
catch {
    $results.Add((New-CheckResult "Domain secure channel" $false $_.Exception.Message))
}

try {
    $discovered = & nltest.exe "/dsgetdc:$DomainFqdn" 2>&1
    $results.Add((New-CheckResult "DC locator $DomainFqdn" ($LASTEXITCODE -eq 0) (($discovered -join " ").Trim())))
}
catch {
    $results.Add((New-CheckResult "DC locator $DomainFqdn" $false $_.Exception.Message))
}

try {
    $domainAddresses = Get-HostAddresses -Name $DomainFqdn
    $results.Add((New-CheckResult "DNS $DomainFqdn" (-not [string]::IsNullOrWhiteSpace($domainAddresses)) $domainAddresses))
}
catch {
    $results.Add((New-CheckResult "DNS $DomainFqdn" $false $_.Exception.Message))
}

foreach ($dc in $DomainControllers) {
    try {
        $addresses = Get-HostAddresses -Name $dc
        $results.Add((New-CheckResult "DNS $dc" (-not [string]::IsNullOrWhiteSpace($addresses)) $addresses))
    }
    catch {
        $results.Add((New-CheckResult "DNS $dc" $false $_.Exception.Message))
    }

    $tcp = Test-TcpPort -ComputerName $dc -Port 636 -Label "TCP $dc`:636"
    $results.Add($tcp.Result)

    if ($tcp.Succeeded) {
        $results.Add((Test-TlsEndpoint -HostName $dc -Port 636))
        if ($null -ne $ServiceCredential) {
            $results.Add((Test-LdapsBind -Label "service bind" -HostName $dc -Credential $ServiceCredential -SearchBase $BaseDn))
        }
        if ($null -ne $TestUserCredential) {
            $results.Add((Test-LdapsBind -Label "test user bind" -HostName $dc -Credential $TestUserCredential -SearchBase $BaseDn))
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($PostgreSqlHost)) {
    $postgres = Test-TcpPort -ComputerName $PostgreSqlHost -Port $PostgreSqlPort `
        -Label "TCP $PostgreSqlHost`:$PostgreSqlPort (PostgreSQL)"
    $results.Add($postgres.Result)
}
else {
    Write-Host "PostgreSQL check skipped (-PostgreSqlHost not supplied)."
}

if (-not $SkipWebHostCheck -and -not [string]::IsNullOrWhiteSpace($WebHost)) {
    $results.Add((Test-TlsEndpoint -HostName $WebHost -Port 443))
}

$results | Format-Table -AutoSize -Wrap

$failed = @($results | Where-Object { $_.Status -eq "FAILED" })
if ($failed.Count -gt 0) {
    Write-Warning "$($failed.Count) readiness check(s) failed."
    exit 1
}

Write-Host "All readiness checks passed." -ForegroundColor Green
exit 0
