using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Remoting;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ITAdmin.HostAgent.Contracts;
using Microsoft.Extensions.Logging;

namespace ITAdmin.HostAgent;

public interface IDnsRemoteProbeExecutor
{
    Task<HostAgentDnsProbeResult> ProbeAsync(HostAgentRequest request, CancellationToken cancellationToken);
}

internal static class DnsRemoteCapabilityProbe
{
    // Fixed and owned by the agent. No request value is ever concatenated into this script.
    internal const string Script = """
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop
        $dnsServer = Get-DnsServer -ErrorAction Stop
        $zones = @(Get-DnsServerZone -ErrorAction Stop)
        $module = Get-Module DnsServer
        $versionParts = @(
            $dnsServer.ServerSetting.MajorVersion,
            $dnsServer.ServerSetting.MinorVersion,
            $dnsServer.ServerSetting.BuildNumber
        ) | Where-Object { $null -ne $_ -and "$_" -ne '' }
        [pscustomobject]@{
            OperatingSystemVersion = [Environment]::OSVersion.VersionString
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            DnsModuleVersion = if ($module) { $module.Version.ToString() } else { $null }
            DnsServerVersion = if ($versionParts.Count -gt 0) { $versionParts -join '.' } else { $null }
            ZoneCount = $zones.Count
            Zones = [bool](Get-Command Get-DnsServerZone -ErrorAction SilentlyContinue)
            Records = [bool](Get-Command Get-DnsServerResourceRecord -ErrorAction SilentlyContinue)
            ServerSettings = [bool](Get-Command Set-DnsServer -ErrorAction SilentlyContinue)
            Dnssec = [bool](Get-Command Get-DnsServerDnsSecZoneSetting -ErrorAction SilentlyContinue)
            Policies = [bool](Get-Command Get-DnsServerQueryResolutionPolicy -ErrorAction SilentlyContinue)
            Scopes = [bool](Get-Command Get-DnsServerZoneScope -ErrorAction SilentlyContinue)
            Cache = [bool](Get-Command Clear-DnsServerCache -ErrorAction SilentlyContinue)
        }
        """;
}

[SupportedOSPlatform("windows")]
public sealed class PowerShellDnsRemoteProbeExecutor(ILogger<PowerShellDnsRemoteProbeExecutor> logger)
    : IDnsRemoteProbeExecutor
{
    private const string MicrosoftPowerShellShellUri = "http://schemas.microsoft.com/powershell/Microsoft.PowerShell";

    public async Task<HostAgentDnsProbeResult> ProbeAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try
        {
            return await ProbeCoreAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("Timeout", "The DNS connection test timed out.", network: false, tls: false);
        }
    }

    private async Task<HostAgentDnsProbeResult> ProbeCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value,
            request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
        {
            return Failure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable
                    ? "The WinRM HTTPS certificate could not be validated."
                    : "The WinRM HTTPS endpoint could not be reached.",
                tls.NetworkReachable, tls.Valid);
        }

        using var securePassword = ToSecureString(request.DnsPassword!);
        var credential = new PSCredential(request.DnsUserName!, securePassword);
        var endpoint = new UriBuilder("https", host, request.DnsPort.Value, "wsman").Uri;
        var timeout = request.DnsTimeoutSeconds!.Value * 1000;
        var connection = new WSManConnectionInfo(endpoint, MicrosoftPowerShellShellUri, credential)
        {
            AuthenticationMechanism = request.DnsAuthenticationMode == HostAgentDnsAuthenticationMode.BasicOverTls
                ? AuthenticationMechanism.Basic
                : AuthenticationMechanism.Negotiate,
            OpenTimeout = timeout,
            OperationTimeout = timeout,
            CancelTimeout = Math.Min(timeout, 10_000),
            NoMachineProfile = true,
        };

        using var runspace = RunspaceFactory.CreateRunspace(connection);
        try
        {
            await Task.Run(runspace.Open, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is PSRemotingTransportException
                                          or RemoteException
                                          or RuntimeException
                                          or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS WinRM connection failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return Failure("AuthenticationOrRemotingFailed",
                "WinRM rejected the credentials or the remote PowerShell endpoint is unavailable.",
                network: true, tls: true);
        }

        try
        {
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteCapabilityProbe.Script, useLocalScope: true);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            if (powerShell.HadErrors || output.Count != 1)
            {
                return Failure("DnsCapabilityProbeFailed",
                    "The connection succeeded, but DNS Server capabilities could not be read.",
                    network: true, tls: true, authentication: true);
            }

            return MapSuccess(output[0]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is PSRemotingTransportException
                                          or RemoteException
                                          or RuntimeException
                                          or InvalidRunspaceStateException)
        {
            logger.LogWarning("DNS Server capability probe failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return Failure("DnsCapabilityProbeFailed",
                "The connection succeeded, but DNS Server capabilities could not be read.",
                network: true, tls: true, authentication: true);
        }
    }

    private static HostAgentDnsProbeResult MapSuccess(PSObject value) => new()
    {
        Success = true,
        Message = "The DNS server connection and capability probe succeeded.",
        NetworkReachable = true,
        TlsValidated = true,
        AuthenticationSucceeded = true,
        DnsModuleAvailable = true,
        DnsServiceReachable = true,
        OperatingSystemVersion = ReadString(value, "OperatingSystemVersion", 128),
        PowerShellVersion = ReadString(value, "PowerShellVersion", 64),
        DnsModuleVersion = ReadString(value, "DnsModuleVersion", 64),
        DnsServerVersion = ReadString(value, "DnsServerVersion", 128),
        ZoneCount = ReadInt(value, "ZoneCount"),
        Capabilities = new HostAgentDnsCapabilities
        {
            Zones = ReadBool(value, "Zones"), Records = ReadBool(value, "Records"),
            ServerSettings = ReadBool(value, "ServerSettings"), Dnssec = ReadBool(value, "Dnssec"),
            Policies = ReadBool(value, "Policies"), Scopes = ReadBool(value, "Scopes"),
            Cache = ReadBool(value, "Cache"),
        },
    };

    private static async Task<TlsProbeResult> ValidateTlsAsync(
        string host, int port, string? expectedThumbprint, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            return new(false, false);
        }

        var policyValid = false;
        var pinValid = string.IsNullOrWhiteSpace(expectedThumbprint);
        using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false,
            (_, certificate, _, errors) =>
            {
                policyValid = errors == SslPolicyErrors.None;
                if (certificate is not null && !string.IsNullOrWhiteSpace(expectedThumbprint))
                {
                    using var parsed = new X509Certificate2(certificate);
                    var expected = NormalizeThumbprint(expectedThumbprint);
                    var actual = expected.Length == 64
                        ? parsed.GetCertHashString(HashAlgorithmName.SHA256)
                        : parsed.GetCertHashString(HashAlgorithmName.SHA1);
                    pinValid = CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(expected), Convert.FromHexString(actual));
                }
                return policyValid && pinValid;
            });
        try
        {
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.Online,
            }, cancellationToken);
            return new(true, policyValid && pinValid);
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            return new(true, false);
        }
    }

    private static SecureString ToSecureString(string value)
    {
        var result = new SecureString();
        foreach (var character in value) result.AppendChar(character);
        result.MakeReadOnly();
        return result;
    }

    private static string NormalizeThumbprint(string value) =>
        string.Concat(value.Where(Uri.IsHexDigit)).ToUpperInvariant();
    private static string? ReadString(PSObject value, string name, int maxLength)
    {
        var text = value.Properties[name]?.Value?.ToString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, maxLength)];
    }
    private static bool ReadBool(PSObject value, string name) =>
        LanguagePrimitives.TryConvertTo(value.Properties[name]?.Value, out bool result) && result;
    private static int? ReadInt(PSObject value, string name) =>
        LanguagePrimitives.TryConvertTo(value.Properties[name]?.Value, out int result) && result >= 0 ? result : null;
    private static HostAgentDnsProbeResult Failure(string kind, string message, bool network, bool tls, bool authentication = false) =>
        new() { Success = false, FailureKind = kind, Message = message, NetworkReachable = network,
            TlsValidated = tls, AuthenticationSucceeded = authentication };
    private sealed record TlsProbeResult(bool NetworkReachable, bool Valid);
}
