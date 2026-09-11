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
    Task<HostAgentDnsInventoryPage> ReadInventoryPageAsync(
        HostAgentRequest request, CancellationToken cancellationToken);
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

internal static class DnsRemoteInventoryProbe
{
    // Values enter only through AddParameter. Neither the API nor request data can alter this code.
    internal const string Script = """
        param(
            [Parameter(Mandatory=$true)][ValidateSet('Zones','Records')][string]$InventoryKind,
            [Parameter(Mandatory=$true)][int]$Offset,
            [Parameter(Mandatory=$true)][ValidateRange(1,500)][int]$PageSize,
            [string]$ZoneName,
            [string]$ZoneScope,
            [string]$VirtualizationInstance
        )
        $ErrorActionPreference = 'Stop'
        Import-Module DnsServer -ErrorAction Stop

        if ($InventoryKind -eq 'Zones') {
            $items = [System.Collections.Generic.List[object]]::new()
            $instances = [System.Collections.Generic.List[string]]::new()
            $instances.Add('')
            if (Get-Command Get-DnsServerVirtualizationInstance -ErrorAction SilentlyContinue) {
                foreach ($instance in @(Get-DnsServerVirtualizationInstance -ErrorAction Stop)) {
                    $instanceName = if ($instance.VirtualizationInstance) {
                        "$($instance.VirtualizationInstance)"
                    } elseif ($instance.Name) { "$($instance.Name)" } else { '' }
                    if ($instanceName -and -not $instances.Contains($instanceName)) {
                        $instances.Add($instanceName)
                    }
                }
            }

            $zoneScopeCommand = Get-Command Get-DnsServerZoneScope -ErrorAction SilentlyContinue
            foreach ($instanceName in $instances) {
                $zoneParameters = @{}
                if ($instanceName) { $zoneParameters.VirtualizationInstance = $instanceName }
                foreach ($zone in @(Get-DnsServerZone @zoneParameters -ErrorAction Stop)) {
                    $scopes = @()
                    if ($zoneScopeCommand -and
                        (-not $instanceName -or $zoneScopeCommand.Parameters.ContainsKey('VirtualizationInstance'))) {
                        $scopeParameters = @{ ZoneName = "$($zone.ZoneName)" }
                        if ($instanceName) { $scopeParameters.VirtualizationInstance = $instanceName }
                        $scopes = @(Get-DnsServerZoneScope @scopeParameters -ErrorAction Stop | ForEach-Object {
                            $scopeName = if ($_.ZoneScope) { "$($_.ZoneScope)" } elseif ($_.Name) { "$($_.Name)" } else { '' }
                            if ($scopeName -and $scopeName -ne "$($zone.ZoneName)") { $scopeName }
                        } | Sort-Object -Unique)
                    }
                    $items.Add([pscustomobject]@{
                        Name = "$($zone.ZoneName)"
                        ZoneType = "$($zone.ZoneType)"
                        IsReverseLookupZone = [bool]$zone.IsReverseLookupZone
                        IsDsIntegrated = [bool]$zone.IsDsIntegrated
                        IsSigned = [bool]$zone.IsSigned
                        IsPaused = [bool]$zone.IsPaused
                        DynamicUpdate = if ($null -ne $zone.DynamicUpdate) { "$($zone.DynamicUpdate)" } else { $null }
                        ReplicationScope = if ($null -ne $zone.ReplicationScope) { "$($zone.ReplicationScope)" } else { $null }
                        DirectoryPartitionName = if ($zone.DirectoryPartitionName) { "$($zone.DirectoryPartitionName)" } else { $null }
                        ZoneFile = if ($zone.ZoneFile) { "$($zone.ZoneFile)" } else { $null }
                        VirtualizationInstance = if ($instanceName) { $instanceName } else { $null }
                        ZoneScopes = $scopes
                    })
                }
            }
            $items | Sort-Object VirtualizationInstance, Name | Select-Object -Skip $Offset -First ($PageSize + 1)
            return
        }

        $recordParameters = @{ ZoneName = $ZoneName }
        if ($ZoneScope) { $recordParameters.ZoneScope = $ZoneScope }
        if ($VirtualizationInstance) { $recordParameters.VirtualizationInstance = $VirtualizationInstance }
        $recordItems = @(Get-DnsServerResourceRecord @recordParameters -ErrorAction Stop | ForEach-Object {
                $record = $_
                $data = [ordered]@{}
                foreach ($property in @($record.RecordData.CimInstanceProperties) | Sort-Object Name) {
                    $value = $property.Value
                    if ($null -eq $value) {
                        $data[$property.Name] = $null
                    } elseif ($value -is [System.Array]) {
                        $data[$property.Name] = @($value | ForEach-Object { "$_" })
                    } elseif ($value -is [System.Net.IPAddress]) {
                        $data[$property.Name] = $value.IPAddressToString
                    } elseif ($value -is [TimeSpan]) {
                        $data[$property.Name] = [long]$value.TotalSeconds
                    } elseif ($value -is [DateTime]) {
                        $data[$property.Name] = $value.ToUniversalTime().ToString('O')
                    } else {
                        $data[$property.Name] = "$value"
                    }
                }
                [pscustomobject]@{
                    RelativeName = if ($record.HostName) { "$($record.HostName)" } else { '@' }
                    RecordType = "$($record.RecordType)".ToUpperInvariant()
                    RecordDataJson = $data | ConvertTo-Json -Compress -Depth 8
                    TimeToLiveSeconds = [int][Math]::Max(0, [Math]::Min([int]::MaxValue, [Math]::Round($record.TimeToLive.TotalSeconds)))
                    Timestamp = if ($record.Timestamp -is [DateTime]) { $record.Timestamp.ToUniversalTime().ToString('O') } else { $null }
                    ZoneScope = if ($ZoneScope) { $ZoneScope } else { $null }
                    VirtualizationInstance = if ($VirtualizationInstance) { $VirtualizationInstance } else { $null }
                }
            })
        $recordItems | Sort-Object RelativeName, RecordType, RecordDataJson, TimeToLiveSeconds, Timestamp |
            Select-Object -Skip $Offset -First ($PageSize + 1)
        """;
}

[SupportedOSPlatform("windows")]
public sealed class PowerShellDnsRemoteProbeExecutor(ILogger<PowerShellDnsRemoteProbeExecutor> logger)
    : IDnsRemoteProbeExecutor
{
    private const string MicrosoftPowerShellShellUri = "http://schemas.microsoft.com/powershell/Microsoft.PowerShell";
    private const int InventoryPayloadBudgetBytes = 700_000;

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

    public async Task<HostAgentDnsInventoryPage> ReadInventoryPageAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.DnsTimeoutSeconds!.Value));
        try
        {
            return await ReadInventoryPageCoreAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return InventoryFailure("Timeout", "The DNS inventory page timed out.");
        }
    }

    private async Task<HostAgentDnsInventoryPage> ReadInventoryPageCoreAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        var host = request.DnsHostName!.Trim().TrimEnd('.');
        var tls = await ValidateTlsAsync(host, request.DnsPort!.Value,
            request.DnsTlsCertificateThumbprint, cancellationToken);
        if (!tls.NetworkReachable || !tls.Valid)
        {
            return InventoryFailure(tls.NetworkReachable ? "TlsValidationFailed" : "NetworkUnreachable",
                tls.NetworkReachable
                    ? "The WinRM HTTPS certificate could not be validated."
                    : "The WinRM HTTPS endpoint could not be reached.");
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
            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(DnsRemoteInventoryProbe.Script, useLocalScope: true)
                .AddParameter("InventoryKind", request.DnsInventoryKind!.Value.ToString())
                .AddParameter("Offset", request.DnsInventoryOffset!.Value)
                .AddParameter("PageSize", request.DnsInventoryPageSize!.Value)
                .AddParameter("ZoneName", request.DnsZoneName)
                .AddParameter("ZoneScope", request.DnsZoneScope)
                .AddParameter("VirtualizationInstance", request.DnsVirtualizationInstance);
            var output = await Task.Run(powerShell.Invoke, cancellationToken);
            if (powerShell.HadErrors)
            {
                return InventoryFailure("DnsInventoryReadFailed", "The DNS inventory page could not be read.");
            }

            if (request.DnsInventoryKind == HostAgentDnsInventoryKind.Zones)
            {
                var values = FitPayload(output.Take(request.DnsInventoryPageSize.Value).Select(MapZone));
                return values.Count == 0 && output.Count > 0
                    ? InventoryFailure("InventoryItemTooLarge", "A DNS zone inventory item exceeded the response limit.")
                    : new HostAgentDnsInventoryPage
                    {
                        Success = true,
                        Message = "DNS zone inventory page read.",
                        HasMore = output.Count > values.Count,
                        Zones = values,
                    };
            }
            else
            {
                var values = FitPayload(output.Take(request.DnsInventoryPageSize.Value).Select(MapRecord));
                return values.Count == 0 && output.Count > 0
                    ? InventoryFailure("InventoryItemTooLarge", "A DNS record inventory item exceeded the response limit.")
                    : new HostAgentDnsInventoryPage
                    {
                        Success = true,
                        Message = "DNS record inventory page read.",
                        HasMore = output.Count > values.Count,
                        Records = values,
                    };
            }
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
            logger.LogWarning("DNS inventory read failed for {Host}:{Port} ({ExceptionType}).",
                host, request.DnsPort, exception.GetType().Name);
            return InventoryFailure("DnsInventoryReadFailed", "The DNS inventory page could not be read.");
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
            Zones = ReadBool(value, "Zones"),
            Records = ReadBool(value, "Records"),
            ServerSettings = ReadBool(value, "ServerSettings"),
            Dnssec = ReadBool(value, "Dnssec"),
            Policies = ReadBool(value, "Policies"),
            Scopes = ReadBool(value, "Scopes"),
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
    private static DateTime? ReadDateTime(PSObject value, string name)
    {
        var raw = value.Properties[name]?.Value;
        if (raw is DateTime timestamp) return timestamp.ToUniversalTime();
        return DateTime.TryParse(raw?.ToString(), null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed) ? parsed : null;
    }
    private static IReadOnlyList<string> ReadStrings(PSObject value, string name, int maxLength)
    {
        var raw = value.Properties[name]?.Value;
        if (raw is null) return [];
        var values = raw is string text ? [text] : raw is System.Collections.IEnumerable sequence
            ? sequence.Cast<object?>().Select(x => x?.ToString())
            : [raw.ToString()];
        return values.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()[..Math.Min(x.Trim().Length, maxLength)])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static HostAgentDnsZoneInventoryItem MapZone(PSObject value) => new()
    {
        Name = ReadString(value, "Name", 253) ?? string.Empty,
        ZoneType = ReadString(value, "ZoneType", 64) ?? "Unknown",
        IsReverseLookupZone = ReadBool(value, "IsReverseLookupZone"),
        IsDsIntegrated = ReadBool(value, "IsDsIntegrated"),
        IsSigned = ReadBool(value, "IsSigned"),
        IsPaused = ReadBool(value, "IsPaused"),
        DynamicUpdate = ReadString(value, "DynamicUpdate", 64),
        ReplicationScope = ReadString(value, "ReplicationScope", 64),
        DirectoryPartitionName = ReadString(value, "DirectoryPartitionName", 512),
        ZoneFile = ReadString(value, "ZoneFile", 512),
        VirtualizationInstance = ReadString(value, "VirtualizationInstance", 128),
        ZoneScopes = ReadStrings(value, "ZoneScopes", 128),
    };
    private static HostAgentDnsRecordInventoryItem MapRecord(PSObject value) => new()
    {
        RelativeName = ReadString(value, "RelativeName", 253) ?? "@",
        RecordType = ReadString(value, "RecordType", 32) ?? "Unknown",
        RecordDataJson = ReadString(value, "RecordDataJson", 256_000) ?? "{}",
        TimeToLiveSeconds = ReadInt(value, "TimeToLiveSeconds") ?? 0,
        Timestamp = ReadDateTime(value, "Timestamp"),
        ZoneScope = ReadString(value, "ZoneScope", 128),
        VirtualizationInstance = ReadString(value, "VirtualizationInstance", 128),
    };
    private static HostAgentDnsProbeResult Failure(string kind, string message, bool network, bool tls, bool authentication = false) =>
        new()
        {
            Success = false,
            FailureKind = kind,
            Message = message,
            NetworkReachable = network,
            TlsValidated = tls,
            AuthenticationSucceeded = authentication
        };
    private static HostAgentDnsInventoryPage InventoryFailure(string kind, string message) =>
        new() { Success = false, FailureKind = kind, Message = message };
    private static IReadOnlyList<T> FitPayload<T>(IEnumerable<T> source)
    {
        var result = new List<T>();
        var bytes = 0;
        foreach (var item in source)
        {
            var itemBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(item, HostAgentProtocol.Json).Length;
            if (bytes + itemBytes > InventoryPayloadBudgetBytes) break;
            result.Add(item);
            bytes += itemBytes;
        }
        return result;
    }
    private sealed record TlsProbeResult(bool NetworkReachable, bool Valid);
}
