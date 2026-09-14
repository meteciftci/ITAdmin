using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.HostAgent.Contracts;

/// <summary>
/// The wire contract between the ITAdmin web application and the privileged ITAdmin Host Agent.
///
/// <para>
/// <b>Why a separate privileged component at all.</b> Fetching source, building it, running
/// migrations, repointing an IIS site, and opening authenticated management sessions to registered
/// DNS servers are privileged operations. The web application is internet-facing-shaped code that
/// parses untrusted input all day; giving its app pool those rights would mean any request-handling
/// flaw becomes machine compromise. So the app pool identity keeps exactly the rights it has today
/// - read its build, write its logs and key ring - and a separate service running as LocalSystem
/// performs a small, fixed set of operations.
/// </para>
///
/// <para>
/// <b>Why named pipes.</b> The boundary authenticates the caller for free: the server learns the
/// connecting principal from the pipe, and a pipe ACL restricts who may connect at all - enforced
/// by the kernel, not by a token the application has to store and protect.
/// </para>
///
/// <para>
/// <b>Why typed operations.</b> Every operation below is a named intent with a fixed payload. There
/// is no "run this command", no caller-supplied script, and no shell. Each operation validates its
/// bounded data and executes code compiled into the agent. Update arguments come from the agent's
/// configuration; DNS connection values can only be used by fixed, typed DNS operations.
/// </para>
/// </summary>
public static class HostAgentProtocol
{
    public const int ProtocolVersion = 14;

    /// <summary>Pipe name. Machine-local; the agent ACLs it to the app pool identity and administrators.</summary>
    public const string PipeName = "ITAdmin.HostAgent";

    /// <summary>Cap on a single request or response frame.</summary>
    public const int MaxFrameBytes = 1 << 20;

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// The complete set of things the web application may ask the privileged agent to do. This enum is
/// the boundary: it is intentionally short and carries no executable free-form parameters.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentOperation
{
    /// <summary>Liveness and version of the agent itself.</summary>
    Ping = 0,

    /// <summary>Active commit, branch, and health. Read-only.</summary>
    GetInstallationStatus = 1,

    /// <summary>
    /// Fetch the configured branch and report how far behind it the deployed build is. Read-only.
    /// </summary>
    CheckForUpdates = 2,

    /// <summary>
    /// Rebuild and redeploy from the current branch tip by running the checked-out deployment
    /// script. Carries no parameters - the agent builds the command line from its own configuration.
    /// </summary>
    RequestUpdate = 3,

    /// <summary>Progress and outcome of the most recent update request.</summary>
    GetUpdateStatus = 4,

    /// <summary>Recycle the ITAdmin application pool. The narrowest useful service operation.</summary>
    RecycleApplicationPool = 5,

    /// <summary>Current HTTPS binding state: enabled, port, redirect, and the bound certificate. Read-only.</summary>
    GetHttpsStatus = 6,

    /// <summary>
    /// Import an uploaded PFX into the machine store and bind it to the site. The certificate bytes
    /// and password are validated data, not a script or a path: the agent decodes, imports, and
    /// then runs the checked-out deployment script with a thumbprint it derived itself.
    /// </summary>
    ConfigureHttps = 7,

    /// <summary>Remove the HTTPS binding and the HTTP-to-HTTPS redirect. Back to HTTP-only.</summary>
    DisableHttps = 8,

    /// <summary>
    /// Test one registered Windows DNS endpoint and discover its fixed capability set. The request
    /// contains connection values only; it never carries PowerShell or command text.
    /// </summary>
    TestDnsServerConnection = 9,

    /// <summary>
    /// Read one bounded page of DNS zones or resource records. All query parameters are validated
    /// data for the fixed inventory script; callers cannot submit executable text.
    /// </summary>
    ReadDnsServerInventoryPage = 10,

    /// <summary>
    /// Create, update, or delete one supported DNS resource record. The request contains only
    /// validated record fields; the agent owns the fixed PowerShell implementation.
    /// </summary>
    MutateDnsServerResourceRecord = 11,

    /// <summary>
    /// Create, update, or delete one supported DNS zone. The request contains a typed zone
    /// configuration and never carries executable text.
    /// </summary>
    MutateDnsServerZone = 12,

    /// <summary>
    /// Read or update the allowlisted server-level forwarder and recursion settings, or clear the
    /// DNS server cache. The request carries typed values only; the agent owns the fixed script.
    /// </summary>
    ManageDnsServerSettings = 13,

    /// <summary>Read or mutate allowlisted DNS client subnets, zone scopes, and query policies.</summary>
    ManageDnsPolicyConfiguration = 14,

    /// <summary>Read or mutate authoritative signing and recursive resolver DNSSEC trust.</summary>
    ManageDnssecConfiguration = 15,
    /// <summary>Read or mutate DNS aging and scavenging configuration.</summary>
    ManageDnsScavenging = 16,
    /// <summary>Read or mutate DNS listening addresses and root hints.</summary>
    ManageDnsNetworkConfiguration = 17,
    /// <summary>Read or update primary-zone transfer and notification settings.</summary>
    ManageDnsZoneTransfers = 18,
    /// <summary>Read or mutate authoritative child-zone delegations.</summary>
    ManageDnsZoneDelegations = 19,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsAuthenticationMode
{
    Negotiate = 0,
    BasicOverTls = 1,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsInventoryKind
{
    Zones = 0,
    Records = 1,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsRecordMutationKind
{
    Create = 0,
    Update = 1,
    Delete = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsZoneMutationKind
{
    Create = 0,
    Update = 1,
    Delete = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsZoneKind
{
    Primary = 0,
    Secondary = 1,
    Stub = 2,
    Forwarder = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsServerSettingsAction
{
    Read = 0,
    Update = 1,
    ClearCache = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsPolicyAction
{
    Read = 0,
    SaveClientSubnet = 1,
    DeleteClientSubnet = 2,
    CreateZoneScope = 3,
    DeleteZoneScope = 4,
    SaveQueryPolicy = 5,
    DeleteQueryPolicy = 6,
    SetQueryPolicyEnabled = 7,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnssecAction
{
    Read = 0,
    SignWithDefaults = 1,
    Resign = 2,
    Unsign = 3,
    RolloverKeys = 4,
    SetValidationEnabled = 5,
    RetrieveRootTrustAnchor = 6,
    AddDsTrustAnchor = 7,
    AddDnsKeyTrustAnchor = 8,
    RemoveTrustAnchorType = 9,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsScavengingAction
{
    Read = 0,
    UpdateServer = 1,
    UpdateZone = 2,
    StartScavenging = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsNetworkAction
{
    Read = 0,
    UpdateListeningAddresses = 1,
    AddRootHint = 2,
    UpdateRootHint = 3,
    RemoveRootHint = 4,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsZoneTransferAction { Read = 0, Update = 1 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsZoneDelegationAction { Read = 0, AddNameServer = 1, UpdateNameServerAddresses = 2, RemoveNameServer = 3, DeleteDelegation = 4 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsZoneTransferMode { NoTransfer = 0, TransferAnyServer = 1, TransferToZoneNameServer = 2, TransferToSecureServers = 3 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsZoneNotifyMode { NoNotify = 0, Notify = 1, NotifyServers = 2 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsPolicyLevel { Server = 0, Zone = 1 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsPolicyDecision { Allow = 0, Deny = 1, Ignore = 2 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsPolicyCondition { And = 0, Or = 1 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentDnsPolicyMatchOperator { Eq = 0, Ne = 1 }

public sealed record HostAgentDnsPolicyCriterion
{
    [JsonPropertyName("operator")]
    public HostAgentDnsPolicyMatchOperator Operator { get; init; }
    [JsonPropertyName("values")]
    public IReadOnlyList<string> Values { get; init; } = [];
}

public sealed record HostAgentDnsZoneScopeWeight
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("weight")]
    public int Weight { get; init; } = 1;
}

public sealed record HostAgentDnsPolicyMutation
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("zoneName")]
    public string? ZoneName { get; init; }
    [JsonPropertyName("ipv4Subnets")]
    public IReadOnlyList<string> Ipv4Subnets { get; init; } = [];
    [JsonPropertyName("ipv6Subnets")]
    public IReadOnlyList<string> Ipv6Subnets { get; init; } = [];
    [JsonPropertyName("level")]
    public HostAgentDnsPolicyLevel Level { get; init; }
    [JsonPropertyName("decision")]
    public HostAgentDnsPolicyDecision Decision { get; init; }
    [JsonPropertyName("condition")]
    public HostAgentDnsPolicyCondition Condition { get; init; }
    [JsonPropertyName("processingOrder")]
    public int ProcessingOrder { get; init; } = 1;
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
    [JsonPropertyName("clientSubnet")]
    public HostAgentDnsPolicyCriterion? ClientSubnet { get; init; }
    [JsonPropertyName("fqdn")]
    public HostAgentDnsPolicyCriterion? Fqdn { get; init; }
    [JsonPropertyName("queryType")]
    public HostAgentDnsPolicyCriterion? QueryType { get; init; }
    [JsonPropertyName("transportProtocol")]
    public HostAgentDnsPolicyCriterion? TransportProtocol { get; init; }
    [JsonPropertyName("internetProtocol")]
    public HostAgentDnsPolicyCriterion? InternetProtocol { get; init; }
    [JsonPropertyName("serverInterfaceIp")]
    public HostAgentDnsPolicyCriterion? ServerInterfaceIp { get; init; }
    [JsonPropertyName("zoneScopes")]
    public IReadOnlyList<HostAgentDnsZoneScopeWeight> ZoneScopes { get; init; } = [];
}

/// <summary>One request across the pipe.</summary>
public sealed record HostAgentRequest
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; } = HostAgentProtocol.ProtocolVersion;

    [JsonPropertyName("operation")]
    public HostAgentOperation Operation { get; init; }

    /// <summary>Correlates the request with the agent's own logs and the caller's audit entry.</summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    /// <summary>Base64 of the uploaded PFX, for <see cref="HostAgentOperation.ConfigureHttps"/>.</summary>
    [JsonPropertyName("pfxBase64")]
    public string? PfxBase64 { get; init; }

    /// <summary>PFX password, for <see cref="HostAgentOperation.ConfigureHttps"/>. May be empty.</summary>
    [JsonPropertyName("pfxPassword")]
    public string? PfxPassword { get; init; }

    /// <summary>HTTPS port for <see cref="HostAgentOperation.ConfigureHttps"/>. Defaults to 443.</summary>
    [JsonPropertyName("httpsPort")]
    public int? HttpsPort { get; init; }

    /// <summary>Whether to enforce HTTP-to-HTTPS redirect, for <see cref="HostAgentOperation.ConfigureHttps"/>.</summary>
    [JsonPropertyName("redirectHttpToHttps")]
    public bool? RedirectHttpToHttps { get; init; }

    [JsonPropertyName("dnsHostName")]
    public string? DnsHostName { get; init; }

    [JsonPropertyName("dnsPort")]
    public int? DnsPort { get; init; }

    [JsonPropertyName("dnsAuthenticationMode")]
    public HostAgentDnsAuthenticationMode? DnsAuthenticationMode { get; init; }

    [JsonPropertyName("dnsUserName")]
    public string? DnsUserName { get; init; }

    [JsonPropertyName("dnsPassword")]
    public string? DnsPassword { get; init; }

    [JsonPropertyName("dnsTlsCertificateThumbprint")]
    public string? DnsTlsCertificateThumbprint { get; init; }

    [JsonPropertyName("dnsTimeoutSeconds")]
    public int? DnsTimeoutSeconds { get; init; }

    [JsonPropertyName("dnsInventoryKind")]
    public HostAgentDnsInventoryKind? DnsInventoryKind { get; init; }

    [JsonPropertyName("dnsZoneName")]
    public string? DnsZoneName { get; init; }

    [JsonPropertyName("dnsZoneScope")]
    public string? DnsZoneScope { get; init; }

    [JsonPropertyName("dnsVirtualizationInstance")]
    public string? DnsVirtualizationInstance { get; init; }

    [JsonPropertyName("dnsInventoryOffset")]
    public int? DnsInventoryOffset { get; init; }

    [JsonPropertyName("dnsInventoryPageSize")]
    public int? DnsInventoryPageSize { get; init; }

    [JsonPropertyName("dnsRecordMutationKind")]
    public HostAgentDnsRecordMutationKind? DnsRecordMutationKind { get; init; }

    [JsonPropertyName("dnsRecordRelativeName")]
    public string? DnsRecordRelativeName { get; init; }

    [JsonPropertyName("dnsRecordType")]
    public string? DnsRecordType { get; init; }

    [JsonPropertyName("dnsRecordValues")]
    public IReadOnlyList<string>? DnsRecordValues { get; init; }

    [JsonPropertyName("dnsRecordTimeToLiveSeconds")]
    public int? DnsRecordTimeToLiveSeconds { get; init; }

    [JsonPropertyName("dnsExpectedRecordHash")]
    public string? DnsExpectedRecordHash { get; init; }

    [JsonPropertyName("dnsExpectedRecordDataJson")]
    public string? DnsExpectedRecordDataJson { get; init; }

    [JsonPropertyName("dnsExpectedRecordTimeToLiveSeconds")]
    public int? DnsExpectedRecordTimeToLiveSeconds { get; init; }

    [JsonPropertyName("dnsZoneMutationKind")]
    public HostAgentDnsZoneMutationKind? DnsZoneMutationKind { get; init; }

    [JsonPropertyName("dnsZoneKind")]
    public HostAgentDnsZoneKind? DnsZoneKind { get; init; }

    [JsonPropertyName("dnsZoneIsDsIntegrated")]
    public bool? DnsZoneIsDsIntegrated { get; init; }

    [JsonPropertyName("dnsZoneDynamicUpdate")]
    public string? DnsZoneDynamicUpdate { get; init; }

    [JsonPropertyName("dnsZoneReplicationScope")]
    public string? DnsZoneReplicationScope { get; init; }

    [JsonPropertyName("dnsZonePartitionName")]
    public string? DnsZonePartitionName { get; init; }

    [JsonPropertyName("dnsZoneFile")]
    public string? DnsZoneFile { get; init; }

    [JsonPropertyName("dnsZoneMasterServers")]
    public IReadOnlyList<string>? DnsZoneMasterServers { get; init; }

    [JsonPropertyName("dnsZoneForwarderTimeoutSeconds")]
    public int? DnsZoneForwarderTimeoutSeconds { get; init; }

    [JsonPropertyName("dnsZoneUseRecursion")]
    public bool? DnsZoneUseRecursion { get; init; }

    [JsonPropertyName("dnsExpectedZoneStateJson")]
    public string? DnsExpectedZoneStateJson { get; init; }

    [JsonPropertyName("dnsServerSettingsAction")]
    public HostAgentDnsServerSettingsAction? DnsServerSettingsAction { get; init; }

    [JsonPropertyName("dnsForwarderAddresses")]
    public IReadOnlyList<string>? DnsForwarderAddresses { get; init; }

    [JsonPropertyName("dnsForwarderUseRootHint")]
    public bool? DnsForwarderUseRootHint { get; init; }

    [JsonPropertyName("dnsForwarderTimeoutSeconds")]
    public int? DnsForwarderTimeoutSeconds { get; init; }

    [JsonPropertyName("dnsForwarderEnableReordering")]
    public bool? DnsForwarderEnableReordering { get; init; }

    [JsonPropertyName("dnsRecursionEnabled")]
    public bool? DnsRecursionEnabled { get; init; }

    [JsonPropertyName("dnsRecursionAdditionalTimeoutSeconds")]
    public int? DnsRecursionAdditionalTimeoutSeconds { get; init; }

    [JsonPropertyName("dnsRecursionRetryIntervalSeconds")]
    public int? DnsRecursionRetryIntervalSeconds { get; init; }

    [JsonPropertyName("dnsRecursionTimeoutSeconds")]
    public int? DnsRecursionTimeoutSeconds { get; init; }

    [JsonPropertyName("dnsRecursionSecureResponse")]
    public bool? DnsRecursionSecureResponse { get; init; }

    [JsonPropertyName("dnsExpectedServerSettingsJson")]
    public string? DnsExpectedServerSettingsJson { get; init; }

    [JsonPropertyName("dnsPolicyAction")]
    public HostAgentDnsPolicyAction? DnsPolicyAction { get; init; }
    [JsonPropertyName("dnsPolicyMutation")]
    public HostAgentDnsPolicyMutation? DnsPolicyMutation { get; init; }
    [JsonPropertyName("dnsExpectedPolicyConfigurationJson")]
    public string? DnsExpectedPolicyConfigurationJson { get; init; }

    [JsonPropertyName("dnssecAction")]
    public HostAgentDnssecAction? DnssecAction { get; init; }
    [JsonPropertyName("dnssecZoneName")]
    public string? DnssecZoneName { get; init; }
    [JsonPropertyName("dnssecKeyIds")]
    public IReadOnlyList<Guid>? DnssecKeyIds { get; init; }
    [JsonPropertyName("dnssecValidationEnabled")]
    public bool? DnssecValidationEnabled { get; init; }
    [JsonPropertyName("dnssecTrustPointName")]
    public string? DnssecTrustPointName { get; init; }
    [JsonPropertyName("dnssecTrustAnchorType")]
    public string? DnssecTrustAnchorType { get; init; }
    [JsonPropertyName("dnssecCryptoAlgorithm")]
    public string? DnssecCryptoAlgorithm { get; init; }
    [JsonPropertyName("dnssecKeyTag")]
    public int? DnssecKeyTag { get; init; }
    [JsonPropertyName("dnssecDigestType")]
    public string? DnssecDigestType { get; init; }
    [JsonPropertyName("dnssecDigest")]
    public string? DnssecDigest { get; init; }
    [JsonPropertyName("dnssecBase64Data")]
    public string? DnssecBase64Data { get; init; }
    [JsonPropertyName("dnsExpectedDnssecConfigurationJson")]
    public string? DnsExpectedDnssecConfigurationJson { get; init; }
    [JsonPropertyName("dnsScavengingAction")]
    public HostAgentDnsScavengingAction? DnsScavengingAction { get; init; }
    [JsonPropertyName("dnsScavengingState")]
    public bool? DnsScavengingState { get; init; }
    [JsonPropertyName("dnsScavengingIntervalHours")]
    public int? DnsScavengingIntervalHours { get; init; }
    [JsonPropertyName("dnsAgingZoneName")]
    public string? DnsAgingZoneName { get; init; }
    [JsonPropertyName("dnsZoneAgingEnabled")]
    public bool? DnsZoneAgingEnabled { get; init; }
    [JsonPropertyName("dnsZoneNoRefreshIntervalHours")]
    public int? DnsZoneNoRefreshIntervalHours { get; init; }
    [JsonPropertyName("dnsZoneRefreshIntervalHours")]
    public int? DnsZoneRefreshIntervalHours { get; init; }
    [JsonPropertyName("dnsZoneScavengeServers")]
    public IReadOnlyList<string>? DnsZoneScavengeServers { get; init; }
    [JsonPropertyName("dnsExpectedScavengingConfigurationJson")]
    public string? DnsExpectedScavengingConfigurationJson { get; init; }
    [JsonPropertyName("dnsNetworkAction")]
    public HostAgentDnsNetworkAction? DnsNetworkAction { get; init; }
    [JsonPropertyName("dnsListeningIpAddresses")]
    public IReadOnlyList<string>? DnsListeningIpAddresses { get; init; }
    [JsonPropertyName("dnsRootHintNameServer")]
    public string? DnsRootHintNameServer { get; init; }
    [JsonPropertyName("dnsRootHintIpAddresses")]
    public IReadOnlyList<string>? DnsRootHintIpAddresses { get; init; }
    [JsonPropertyName("dnsOriginalRootHintNameServer")]
    public string? DnsOriginalRootHintNameServer { get; init; }
    [JsonPropertyName("dnsExpectedNetworkConfigurationJson")]
    public string? DnsExpectedNetworkConfigurationJson { get; init; }
    [JsonPropertyName("dnsZoneTransferAction")]
    public HostAgentDnsZoneTransferAction? DnsZoneTransferAction { get; init; }
    [JsonPropertyName("dnsZoneTransferMode")]
    public HostAgentDnsZoneTransferMode? DnsZoneTransferMode { get; init; }
    [JsonPropertyName("dnsZoneSecondaryServers")]
    public IReadOnlyList<string>? DnsZoneSecondaryServers { get; init; }
    [JsonPropertyName("dnsZoneNotifyMode")]
    public HostAgentDnsZoneNotifyMode? DnsZoneNotifyMode { get; init; }
    [JsonPropertyName("dnsZoneNotifyServers")]
    public IReadOnlyList<string>? DnsZoneNotifyServers { get; init; }
    [JsonPropertyName("dnsExpectedZoneTransferConfigurationJson")]
    public string? DnsExpectedZoneTransferConfigurationJson { get; init; }
    [JsonPropertyName("dnsZoneDelegationAction")]
    public HostAgentDnsZoneDelegationAction? DnsZoneDelegationAction { get; init; }
    [JsonPropertyName("dnsDelegationParentZoneName")]
    public string? DnsDelegationParentZoneName { get; init; }
    [JsonPropertyName("dnsDelegationChildZoneName")]
    public string? DnsDelegationChildZoneName { get; init; }
    [JsonPropertyName("dnsDelegationNameServer")]
    public string? DnsDelegationNameServer { get; init; }
    [JsonPropertyName("dnsDelegationIpAddresses")]
    public IReadOnlyList<string>? DnsDelegationIpAddresses { get; init; }
    [JsonPropertyName("dnsExpectedZoneDelegationConfigurationJson")]
    public string? DnsExpectedZoneDelegationConfigurationJson { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, HostAgentProtocol.Json);

    public static HostAgentRequest? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HostAgentRequest>(json, HostAgentProtocol.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (ProtocolVersion != HostAgentProtocol.ProtocolVersion)
        {
            problems.Add(
                $"Unsupported protocol version {ProtocolVersion}; this agent speaks "
                + $"{HostAgentProtocol.ProtocolVersion}.");
        }

        if (!Enum.IsDefined(Operation))
        {
            problems.Add("Unknown operation.");
            return problems;
        }

        if (Operation == HostAgentOperation.ConfigureHttps)
        {
            if (string.IsNullOrWhiteSpace(PfxBase64) || !TryDecodeBase64(PfxBase64, out var pfxLength))
            {
                problems.Add("pfxBase64 must be a base64-encoded PFX.");
            }
            else if (pfxLength is < 1 or > 400_000)
            {
                problems.Add("The PFX is empty or larger than 400 KB.");
            }

            if (PfxPassword is null)
            {
                problems.Add("pfxPassword is required (an empty string is allowed).");
            }

            if (HttpsPort is not null and (< 1 or > 65535))
            {
                problems.Add("httpsPort must be between 1 and 65535.");
            }
        }

        if (Operation is HostAgentOperation.TestDnsServerConnection
            or HostAgentOperation.ReadDnsServerInventoryPage
            or HostAgentOperation.MutateDnsServerResourceRecord
            or HostAgentOperation.MutateDnsServerZone
            or HostAgentOperation.ManageDnsServerSettings
            or HostAgentOperation.ManageDnsPolicyConfiguration
            or HostAgentOperation.ManageDnssecConfiguration
            or HostAgentOperation.ManageDnsScavenging
            or HostAgentOperation.ManageDnsNetworkConfiguration
            or HostAgentOperation.ManageDnsZoneTransfers
            or HostAgentOperation.ManageDnsZoneDelegations)
        {
            var hostName = DnsHostName?.Trim().TrimEnd('.');
            if (string.IsNullOrWhiteSpace(hostName) || hostName.Length > 253
                || Uri.CheckHostName(hostName) == UriHostNameType.Unknown)
            {
                problems.Add("dnsHostName must be a valid host name or IP address.");
            }
            if (DnsPort is null or < 1 or > 65535) problems.Add("dnsPort must be between 1 and 65535.");
            if (DnsAuthenticationMode is null || !Enum.IsDefined(DnsAuthenticationMode.Value))
                problems.Add("dnsAuthenticationMode is required.");
            if (string.IsNullOrWhiteSpace(DnsUserName) || DnsUserName.Length > 256)
                problems.Add("dnsUserName is required and may contain at most 256 characters.");
            if (DnsPassword is null || DnsPassword.Length > 2048)
                problems.Add("dnsPassword is required and may contain at most 2048 characters.");
            if (DnsTimeoutSeconds is null or < 5 or > 300)
                problems.Add("dnsTimeoutSeconds must be between 5 and 300 seconds.");
            var thumbprint = NormalizeThumbprint(DnsTlsCertificateThumbprint);
            if (thumbprint is not null && thumbprint.Length is not (40 or 64))
                problems.Add("dnsTlsCertificateThumbprint must be a SHA-1 or SHA-256 hexadecimal value.");
            if (DnsTlsCertificateThumbprint?.Any(x => !Uri.IsHexDigit(x) && !char.IsWhiteSpace(x) && x is not ':' and not '-') == true)
                problems.Add("dnsTlsCertificateThumbprint contains invalid characters.");
        }

        if (Operation == HostAgentOperation.ReadDnsServerInventoryPage)
        {
            if (DnsInventoryKind is null || !Enum.IsDefined(DnsInventoryKind.Value))
                problems.Add("dnsInventoryKind is required.");
            if (DnsInventoryOffset is null or < 0)
                problems.Add("dnsInventoryOffset must be zero or greater.");
            if (DnsInventoryPageSize is null or < 1 or > 500)
                problems.Add("dnsInventoryPageSize must be between 1 and 500.");
            if (DnsInventoryKind == HostAgentDnsInventoryKind.Records
                && (string.IsNullOrWhiteSpace(DnsZoneName) || DnsZoneName.Length > 253))
                problems.Add("dnsZoneName is required for record inventory and may contain at most 253 characters.");
            if (DnsZoneScope?.Length > 128)
                problems.Add("dnsZoneScope may contain at most 128 characters.");
            if (DnsVirtualizationInstance?.Length > 128)
                problems.Add("dnsVirtualizationInstance may contain at most 128 characters.");
        }


        if (Operation == HostAgentOperation.MutateDnsServerResourceRecord)
        {
            if (DnsRecordMutationKind is null || !Enum.IsDefined(DnsRecordMutationKind.Value))
                problems.Add("dnsRecordMutationKind is required.");
            if (string.IsNullOrWhiteSpace(DnsZoneName) || DnsZoneName.Length > 253 || DnsZoneName.Any(char.IsControl))
                problems.Add("dnsZoneName is required and may contain at most 253 characters.");
            if (string.IsNullOrWhiteSpace(DnsRecordRelativeName) || DnsRecordRelativeName.Length > 253
                || DnsRecordRelativeName.Any(char.IsControl))
                problems.Add("dnsRecordRelativeName is required and may contain at most 253 characters.");
            var recordType = DnsRecordType?.Trim().ToUpperInvariant();
            if (recordType is not ("A" or "AAAA" or "CNAME" or "MX" or "NS" or "PTR" or "SRV" or "TXT"))
                problems.Add("dnsRecordType is not supported for mutation.");
            if (DnsRecordTimeToLiveSeconds is null or < 0 or > 2_147_483)
                problems.Add("dnsRecordTimeToLiveSeconds must be between 0 and 2147483.");
            if (DnsRecordMutationKind != HostAgentDnsRecordMutationKind.Delete)
            {
                if (DnsRecordValues is null || DnsRecordValues.Count is < 1 or > 4
                    || DnsRecordValues.Any(x => string.IsNullOrEmpty(x) || x.Length > 2048 || x.Any(char.IsControl)))
                    problems.Add("dnsRecordValues must contain between 1 and 4 bounded values.");
                else if (!ValidateDnsRecordValues(recordType!, DnsRecordValues))
                    problems.Add("dnsRecordValues do not match dnsRecordType.");
            }
            if (DnsRecordMutationKind is HostAgentDnsRecordMutationKind.Update or HostAgentDnsRecordMutationKind.Delete)
            {
                var hash = DnsExpectedRecordHash?.Trim();
                if (hash is null || hash.Length != 64 || hash.Any(x => !Uri.IsHexDigit(x)))
                    problems.Add("dnsExpectedRecordHash must be a SHA-256 hexadecimal value.");
                if (string.IsNullOrWhiteSpace(DnsExpectedRecordDataJson)
                    || DnsExpectedRecordDataJson.Length > 256_000 || !IsJsonObject(DnsExpectedRecordDataJson))
                    problems.Add("dnsExpectedRecordDataJson must be a bounded JSON object.");
                if (DnsExpectedRecordTimeToLiveSeconds is null or < 0 or > 2_147_483)
                    problems.Add("dnsExpectedRecordTimeToLiveSeconds must be between 0 and 2147483.");
            }
            if (DnsZoneScope?.Length > 128) problems.Add("dnsZoneScope may contain at most 128 characters.");
            if (DnsVirtualizationInstance?.Length > 128)
                problems.Add("dnsVirtualizationInstance may contain at most 128 characters.");
        }


        if (Operation == HostAgentOperation.MutateDnsServerZone)
        {
            if (DnsZoneMutationKind is null || !Enum.IsDefined(DnsZoneMutationKind.Value))
                problems.Add("dnsZoneMutationKind is required.");
            if (DnsZoneKind is null || !Enum.IsDefined(DnsZoneKind.Value))
                problems.Add("dnsZoneKind is required.");
            if (string.IsNullOrWhiteSpace(DnsZoneName) || DnsZoneName.Length > 253
                || DnsZoneName.Any(char.IsControl))
                problems.Add("dnsZoneName is required and may contain at most 253 characters.");
            if (!string.IsNullOrWhiteSpace(DnsVirtualizationInstance))
                problems.Add("Virtualization-instance zone mutation is not supported.");
            if (DnsZoneIsDsIntegrated is null)
                problems.Add("dnsZoneIsDsIntegrated is required.");
            if (DnsZoneDynamicUpdate is not null
                && DnsZoneDynamicUpdate is not ("None" or "NonsecureAndSecure" or "Secure"))
                problems.Add("dnsZoneDynamicUpdate is invalid.");
            if (DnsZoneReplicationScope is not null
                && DnsZoneReplicationScope is not ("Forest" or "Domain" or "Legacy" or "Custom"))
                problems.Add("dnsZoneReplicationScope is invalid.");
            if (DnsZonePartitionName?.Length > 512)
                problems.Add("dnsZonePartitionName may contain at most 512 characters.");
            if (DnsZoneFile?.Length > 255 || DnsZoneFile?.Any(char.IsControl) == true)
                problems.Add("dnsZoneFile may contain at most 255 characters.");
            if (DnsZoneMasterServers is { Count: > 16 }
                || DnsZoneMasterServers?.Any(x => !IPAddress.TryParse(x, out _)) == true)
                problems.Add("dnsZoneMasterServers must contain at most 16 IP addresses.");
            if (DnsZoneKind is HostAgentDnsZoneKind.Secondary or HostAgentDnsZoneKind.Stub or HostAgentDnsZoneKind.Forwarder
                && DnsZoneMutationKind != HostAgentDnsZoneMutationKind.Delete
                && DnsZoneMasterServers is not { Count: > 0 })
                problems.Add("dnsZoneMasterServers is required for this zone type.");
            if (DnsZoneForwarderTimeoutSeconds is not null and (< 0 or > 15))
                problems.Add("dnsZoneForwarderTimeoutSeconds must be between 0 and 15.");
            if (DnsZoneKind == HostAgentDnsZoneKind.Secondary && DnsZoneIsDsIntegrated == true)
                problems.Add("Secondary zones cannot be Active Directory integrated.");
            if (DnsZoneMutationKind != HostAgentDnsZoneMutationKind.Delete
                && DnsZoneIsDsIntegrated == true && DnsZoneReplicationScope is null)
                problems.Add("dnsZoneReplicationScope is required for Active Directory-integrated zones.");
            if (DnsZoneMutationKind != HostAgentDnsZoneMutationKind.Delete
                && DnsZoneReplicationScope == "Custom" && string.IsNullOrWhiteSpace(DnsZonePartitionName))
                problems.Add("dnsZonePartitionName is required for custom replication.");
            if (DnsZoneMutationKind != HostAgentDnsZoneMutationKind.Delete
                && DnsZoneIsDsIntegrated == false && DnsZoneKind != HostAgentDnsZoneKind.Forwarder
                && (string.IsNullOrWhiteSpace(DnsZoneFile)
                    || !DnsZoneFile.EndsWith(".dns", StringComparison.OrdinalIgnoreCase)
                    || DnsZoneFile.IndexOfAny(['/', '\\', ':']) >= 0))
                problems.Add("dnsZoneFile must be a .dns file name without a path.");
            if (DnsZoneMutationKind != HostAgentDnsZoneMutationKind.Delete
                && DnsZoneKind == HostAgentDnsZoneKind.Primary
                && DnsZoneDynamicUpdate is not ("None" or "NonsecureAndSecure" or "Secure"))
                problems.Add("dnsZoneDynamicUpdate is required for primary zones.");
            if (DnsZoneMutationKind != HostAgentDnsZoneMutationKind.Delete
                && DnsZoneKind == HostAgentDnsZoneKind.Primary && DnsZoneIsDsIntegrated == false
                && DnsZoneDynamicUpdate == "Secure")
                problems.Add("Secure dynamic updates require an Active Directory-integrated zone.");
            if (DnsZoneMutationKind != HostAgentDnsZoneMutationKind.Delete
                && DnsZoneKind == HostAgentDnsZoneKind.Forwarder
                && (DnsZoneForwarderTimeoutSeconds is null || DnsZoneUseRecursion is null))
                problems.Add("Forwarder timeout and recursion settings are required.");
            if (DnsZoneMutationKind is HostAgentDnsZoneMutationKind.Update or HostAgentDnsZoneMutationKind.Delete)
            {
                if (string.IsNullOrWhiteSpace(DnsExpectedZoneStateJson)
                    || DnsExpectedZoneStateJson.Length > 32_768 || !IsJsonObject(DnsExpectedZoneStateJson))
                    problems.Add("dnsExpectedZoneStateJson must be a bounded JSON object.");
            }
        }


        if (Operation == HostAgentOperation.ManageDnsServerSettings)
        {
            if (DnsServerSettingsAction is null || !Enum.IsDefined(DnsServerSettingsAction.Value))
                problems.Add("dnsServerSettingsAction is required.");
            if (DnsServerSettingsAction == HostAgentDnsServerSettingsAction.Update)
            {
                if (DnsForwarderAddresses is { Count: > 16 }
                    || DnsForwarderAddresses?.Any(x => !IPAddress.TryParse(x, out _)) == true)
                    problems.Add("dnsForwarderAddresses must contain at most 16 IP addresses.");
                if (DnsForwarderUseRootHint is null || DnsForwarderEnableReordering is null
                    || DnsRecursionEnabled is null || DnsRecursionSecureResponse is null)
                    problems.Add("All DNS server boolean settings are required for update.");
                if (DnsForwarderTimeoutSeconds is null or < 0 or > 15)
                    problems.Add("dnsForwarderTimeoutSeconds must be between 0 and 15.");
                if (DnsRecursionAdditionalTimeoutSeconds is null or < 0 or > 15)
                    problems.Add("dnsRecursionAdditionalTimeoutSeconds must be between 0 and 15.");
                if (DnsRecursionRetryIntervalSeconds is null or < 1 or > 15)
                    problems.Add("dnsRecursionRetryIntervalSeconds must be between 1 and 15.");
                if (DnsRecursionTimeoutSeconds is null or < 1 or > 15)
                    problems.Add("dnsRecursionTimeoutSeconds must be between 1 and 15.");
                if (string.IsNullOrWhiteSpace(DnsExpectedServerSettingsJson)
                    || DnsExpectedServerSettingsJson.Length > 16_384
                    || !IsJsonObject(DnsExpectedServerSettingsJson))
                    problems.Add("dnsExpectedServerSettingsJson must be a bounded JSON object.");
            }
        }


        if (Operation == HostAgentOperation.ManageDnsPolicyConfiguration)
        {
            if (DnsPolicyAction is null || !Enum.IsDefined(DnsPolicyAction.Value))
                problems.Add("dnsPolicyAction is required.");
            if (DnsPolicyAction != HostAgentDnsPolicyAction.Read)
            {
                if (DnsPolicyMutation is null)
                    problems.Add("dnsPolicyMutation is required.");
                else
                    ValidatePolicyMutation(DnsPolicyAction!.Value, DnsPolicyMutation, problems);
                if (string.IsNullOrWhiteSpace(DnsExpectedPolicyConfigurationJson)
                    || DnsExpectedPolicyConfigurationJson.Length > 524_288
                    || !IsJsonObject(DnsExpectedPolicyConfigurationJson))
                    problems.Add("dnsExpectedPolicyConfigurationJson must be a bounded JSON object.");
            }
        }


        if (Operation == HostAgentOperation.ManageDnssecConfiguration)
        {
            if (DnssecAction is null || !Enum.IsDefined(DnssecAction.Value))
                problems.Add("dnssecAction is required.");
            if (DnssecAction != HostAgentDnssecAction.Read)
            {
                if (string.IsNullOrWhiteSpace(DnsExpectedDnssecConfigurationJson)
                    || DnsExpectedDnssecConfigurationJson.Length > 800_000
                    || !IsJsonObject(DnsExpectedDnssecConfigurationJson))
                    problems.Add("dnsExpectedDnssecConfigurationJson must be a bounded JSON object.");
            }
            var zoneAction = DnssecAction is HostAgentDnssecAction.SignWithDefaults or HostAgentDnssecAction.Resign
                or HostAgentDnssecAction.Unsign or HostAgentDnssecAction.RolloverKeys;
            var namedTrustAction = DnssecAction is HostAgentDnssecAction.AddDsTrustAnchor
                or HostAgentDnssecAction.AddDnsKeyTrustAnchor or HostAgentDnssecAction.RemoveTrustAnchorType;
            if (zoneAction
                && (string.IsNullOrWhiteSpace(DnssecZoneName) || DnssecZoneName.Length > 253
                    || DnssecZoneName.Any(char.IsControl)))
                problems.Add("dnssecZoneName is required and may contain at most 253 characters.");
            if (!zoneAction && !string.IsNullOrWhiteSpace(DnssecZoneName))
                problems.Add("dnssecZoneName is only valid for authoritative zone actions.");
            if (DnssecAction == HostAgentDnssecAction.RolloverKeys
                && (DnssecKeyIds is null || DnssecKeyIds.Count is < 1 or > 8
                    || DnssecKeyIds.Any(x => x == Guid.Empty)
                    || DnssecKeyIds.Distinct().Count() != DnssecKeyIds.Count))
                problems.Add("dnssecKeyIds must contain between 1 and 8 unique key identifiers for rollover.");
            if (DnssecAction != HostAgentDnssecAction.RolloverKeys && DnssecKeyIds is { Count: > 0 })
                problems.Add("dnssecKeyIds is only valid for key rollover.");
            if (DnssecAction == HostAgentDnssecAction.SetValidationEnabled && DnssecValidationEnabled is null)
                problems.Add("dnssecValidationEnabled is required for validation updates.");
            if (DnssecAction != HostAgentDnssecAction.SetValidationEnabled && DnssecValidationEnabled is not null)
                problems.Add("dnssecValidationEnabled is only valid for validation updates.");
            if (namedTrustAction
                && (string.IsNullOrWhiteSpace(DnssecTrustPointName) || DnssecTrustPointName.Length > 253
                    || DnssecTrustPointName.Any(char.IsControl)))
                problems.Add("dnssecTrustPointName is required and may contain at most 253 characters.");
            if (!namedTrustAction && !string.IsNullOrWhiteSpace(DnssecTrustPointName))
                problems.Add("dnssecTrustPointName is only valid for named trust-anchor actions.");
            var algorithms = new[] { "RsaSha1", "RsaSha256", "RsaSha512", "RsaSha1NSec3", "ECDsaP256Sha256", "ECDsaP384Sha384" };
            if (DnssecAction is HostAgentDnssecAction.AddDsTrustAnchor or HostAgentDnssecAction.AddDnsKeyTrustAnchor
                && !algorithms.Contains(DnssecCryptoAlgorithm, StringComparer.Ordinal))
                problems.Add("dnssecCryptoAlgorithm is not supported.");
            if (DnssecAction == HostAgentDnssecAction.AddDsTrustAnchor)
            {
                if (DnssecKeyTag is null or < 0 or > 65535) problems.Add("dnssecKeyTag must be between 0 and 65535.");
                var lengths = DnssecDigestType switch { "Sha1" => 40, "Sha256" => 64, "Sha384" => 96, _ => 0 };
                if (lengths == 0 || DnssecDigest?.Length != lengths || DnssecDigest.Any(x => !Uri.IsHexDigit(x)))
                    problems.Add("dnssecDigest must match the selected digest type.");
            }
            if (DnssecAction == HostAgentDnssecAction.AddDnsKeyTrustAnchor
                && (string.IsNullOrWhiteSpace(DnssecBase64Data) || DnssecBase64Data.Length > 16_384
                    || !TryDecodeBase64(DnssecBase64Data, out var keyLength) || keyLength is < 1 or > 12_000))
                problems.Add("dnssecBase64Data must be a bounded base64 DNSKEY value.");
            if (DnssecAction == HostAgentDnssecAction.RemoveTrustAnchorType
                && DnssecTrustAnchorType is not ("DnsKey" or "Ds"))
                problems.Add("dnssecTrustAnchorType must be DnsKey or Ds.");
            if (DnssecAction != HostAgentDnssecAction.RemoveTrustAnchorType && DnssecTrustAnchorType is not null)
                problems.Add("dnssecTrustAnchorType is only valid for trust-anchor removal.");
            if (DnssecAction is not (HostAgentDnssecAction.AddDsTrustAnchor or HostAgentDnssecAction.AddDnsKeyTrustAnchor)
                && DnssecCryptoAlgorithm is not null)
                problems.Add("dnssecCryptoAlgorithm is only valid for trust-anchor creation.");
            if (DnssecAction != HostAgentDnssecAction.AddDsTrustAnchor
                && (DnssecKeyTag is not null || DnssecDigestType is not null || DnssecDigest is not null))
                problems.Add("DS fields are only valid for DS trust-anchor creation.");
            if (DnssecAction != HostAgentDnssecAction.AddDnsKeyTrustAnchor && DnssecBase64Data is not null)
                problems.Add("dnssecBase64Data is only valid for DNSKEY trust-anchor creation.");
        }

        if (Operation == HostAgentOperation.ManageDnsScavenging)
        {
            if (DnsScavengingAction is null || !Enum.IsDefined(DnsScavengingAction.Value))
                problems.Add("dnsScavengingAction is required.");
            if (DnsScavengingAction != HostAgentDnsScavengingAction.Read
                && (string.IsNullOrWhiteSpace(DnsExpectedScavengingConfigurationJson)
                    || DnsExpectedScavengingConfigurationJson.Length > 262_144
                    || !IsJsonObject(DnsExpectedScavengingConfigurationJson)))
                problems.Add("dnsExpectedScavengingConfigurationJson must be a bounded JSON object.");
            if (DnsScavengingAction == HostAgentDnsScavengingAction.UpdateServer
                && (DnsScavengingState is null || DnsScavengingIntervalHours is null or < 0 or > 8760
                    || DnsScavengingState == true && DnsScavengingIntervalHours == 0))
                problems.Add("Enabled server scavenging requires an interval between 1 and 8760 hours.");
            if (DnsScavengingAction == HostAgentDnsScavengingAction.UpdateZone)
            {
                if (string.IsNullOrWhiteSpace(DnsAgingZoneName) || DnsAgingZoneName.Length > 253 || DnsAgingZoneName.Any(char.IsControl))
                    problems.Add("dnsAgingZoneName is required and may contain at most 253 characters.");
                if (DnsZoneAgingEnabled is null || DnsZoneNoRefreshIntervalHours is null or < 0 or > 8760
                    || DnsZoneRefreshIntervalHours is null or < 0 or > 8760)
                    problems.Add("Zone aging state and bounded refresh intervals are required.");
                if (DnsZoneScavengeServers is { Count: > 16 }
                    || DnsZoneScavengeServers?.Any(x => !IPAddress.TryParse(x, out _)) == true)
                    problems.Add("dnsZoneScavengeServers must contain at most 16 IP addresses.");
            }
        }

        if (Operation == HostAgentOperation.ManageDnsNetworkConfiguration)
        {
            if (DnsNetworkAction is null || !Enum.IsDefined(DnsNetworkAction.Value))
                problems.Add("dnsNetworkAction is required.");
            if (DnsNetworkAction != HostAgentDnsNetworkAction.Read
                && (string.IsNullOrWhiteSpace(DnsExpectedNetworkConfigurationJson)
                    || DnsExpectedNetworkConfigurationJson.Length > 131_072
                    || !IsJsonObject(DnsExpectedNetworkConfigurationJson)))
                problems.Add("dnsExpectedNetworkConfigurationJson must be a bounded JSON object.");
            if (DnsNetworkAction == HostAgentDnsNetworkAction.UpdateListeningAddresses
                && (DnsListeningIpAddresses is not { Count: > 0 and <= 64 }
                    || DnsListeningIpAddresses.Any(x => !IPAddress.TryParse(x, out _))))
                problems.Add("dnsListeningIpAddresses must contain between 1 and 64 IP addresses.");
            if (DnsNetworkAction is HostAgentDnsNetworkAction.AddRootHint
                or HostAgentDnsNetworkAction.UpdateRootHint or HostAgentDnsNetworkAction.RemoveRootHint)
            {
                if (!IsBoundedDnsName(DnsRootHintNameServer))
                    problems.Add("dnsRootHintNameServer must be a valid bounded DNS name.");
                if (DnsNetworkAction != HostAgentDnsNetworkAction.RemoveRootHint
                    && (DnsRootHintIpAddresses is not { Count: > 0 and <= 16 }
                        || DnsRootHintIpAddresses.Any(x => !IPAddress.TryParse(x, out _))))
                    problems.Add("dnsRootHintIpAddresses must contain between 1 and 16 IP addresses.");
                if (DnsNetworkAction == HostAgentDnsNetworkAction.UpdateRootHint
                    && !IsBoundedDnsName(DnsOriginalRootHintNameServer))
                    problems.Add("dnsOriginalRootHintNameServer must be a valid bounded DNS name.");
            }
        }

        if (Operation == HostAgentOperation.ManageDnsZoneTransfers)
        {
            if (DnsZoneTransferAction is null || !Enum.IsDefined(DnsZoneTransferAction.Value))
                problems.Add("dnsZoneTransferAction is required.");
            if (DnsZoneTransferAction == HostAgentDnsZoneTransferAction.Update)
            {
                if (!IsBoundedDnsName(DnsZoneName)) problems.Add("dnsZoneName must be a valid bounded DNS name.");
                if (DnsZoneTransferMode is null || !Enum.IsDefined(DnsZoneTransferMode.Value))
                    problems.Add("dnsZoneTransferMode is required.");
                if (DnsZoneNotifyMode is null || !Enum.IsDefined(DnsZoneNotifyMode.Value))
                    problems.Add("dnsZoneNotifyMode is required.");
                if (DnsZoneSecondaryServers is { Count: > 32 }
                    || DnsZoneSecondaryServers?.Any(x => !IPAddress.TryParse(x, out _)) == true)
                    problems.Add("dnsZoneSecondaryServers must contain at most 32 IP addresses.");
                if (DnsZoneNotifyServers is { Count: > 32 }
                    || DnsZoneNotifyServers?.Any(x => !IPAddress.TryParse(x, out _)) == true)
                    problems.Add("dnsZoneNotifyServers must contain at most 32 IP addresses.");
                if (DnsZoneTransferMode == HostAgentDnsZoneTransferMode.TransferToSecureServers
                    && DnsZoneSecondaryServers is not { Count: > 0 })
                    problems.Add("dnsZoneSecondaryServers is required for restricted transfers.");
                if (DnsZoneNotifyMode == HostAgentDnsZoneNotifyMode.NotifyServers
                    && DnsZoneNotifyServers is not { Count: > 0 })
                    problems.Add("dnsZoneNotifyServers is required for selective notifications.");
                if (string.IsNullOrWhiteSpace(DnsExpectedZoneTransferConfigurationJson)
                    || DnsExpectedZoneTransferConfigurationJson.Length > 262_144
                    || !IsJsonObject(DnsExpectedZoneTransferConfigurationJson))
                    problems.Add("dnsExpectedZoneTransferConfigurationJson must be a bounded JSON object.");
            }
        }

        if (Operation == HostAgentOperation.ManageDnsZoneDelegations)
        {
            if (DnsZoneDelegationAction is null || !Enum.IsDefined(DnsZoneDelegationAction.Value))
                problems.Add("dnsZoneDelegationAction is required.");
            if (DnsZoneDelegationAction != HostAgentDnsZoneDelegationAction.Read)
            {
                if (!IsBoundedDnsName(DnsDelegationParentZoneName))
                    problems.Add("dnsDelegationParentZoneName must be a valid bounded DNS name.");
                if (!IsBoundedDnsName(DnsDelegationChildZoneName))
                    problems.Add("dnsDelegationChildZoneName must be a valid bounded DNS name.");
                if (DnsZoneDelegationAction is HostAgentDnsZoneDelegationAction.AddNameServer or HostAgentDnsZoneDelegationAction.UpdateNameServerAddresses or HostAgentDnsZoneDelegationAction.RemoveNameServer
                    && !IsBoundedDnsName(DnsDelegationNameServer))
                    problems.Add("dnsDelegationNameServer must be a valid bounded DNS name.");
                if (DnsZoneDelegationAction is HostAgentDnsZoneDelegationAction.AddNameServer or HostAgentDnsZoneDelegationAction.UpdateNameServerAddresses
                    && (DnsDelegationIpAddresses is not { Count: > 0 and <= 16 }
                        || DnsDelegationIpAddresses.Any(x => !IPAddress.TryParse(x, out _))))
                    problems.Add("dnsDelegationIpAddresses must contain between 1 and 16 IP addresses.");
                if (string.IsNullOrWhiteSpace(DnsExpectedZoneDelegationConfigurationJson)
                    || DnsExpectedZoneDelegationConfigurationJson.Length > 262_144
                    || !IsJsonObject(DnsExpectedZoneDelegationConfigurationJson))
                    problems.Add("dnsExpectedZoneDelegationConfigurationJson must be a bounded JSON object.");
            }
        }

        return problems;
    }

    private static void ValidatePolicyMutation(HostAgentDnsPolicyAction action, HostAgentDnsPolicyMutation value, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 256 || value.Name.Any(char.IsControl)
            || value.Name.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*', ',', ';']) >= 0)
            problems.Add("DNS policy object name is required and may contain at most 256 characters.");
        if (value.ZoneName?.Length > 253 || value.ZoneName?.Any(char.IsControl) == true)
            problems.Add("zoneName may contain at most 253 characters.");
        if (action is HostAgentDnsPolicyAction.CreateZoneScope or HostAgentDnsPolicyAction.DeleteZoneScope
            && string.IsNullOrWhiteSpace(value.ZoneName))
            problems.Add("zoneName is required for a zone scope.");
        if (action is HostAgentDnsPolicyAction.SaveClientSubnet
            && value.Ipv4Subnets.Count + value.Ipv6Subnets.Count == 0)
            problems.Add("At least one client subnet is required.");
        if (value.Ipv4Subnets.Count > 64 || value.Ipv6Subnets.Count > 64
            || value.Ipv4Subnets.Any(x => !IsCidr(x, AddressFamily.InterNetwork, 32))
            || value.Ipv6Subnets.Any(x => !IsCidr(x, AddressFamily.InterNetworkV6, 128)))
            problems.Add("Client subnets must be bounded CIDR values.");
        if (action is HostAgentDnsPolicyAction.SaveQueryPolicy)
        {
            if (value.Level == HostAgentDnsPolicyLevel.Zone && string.IsNullOrWhiteSpace(value.ZoneName))
                problems.Add("zoneName is required for a zone-level policy.");
            if (value.Level == HostAgentDnsPolicyLevel.Server && value.Decision == HostAgentDnsPolicyDecision.Allow)
                problems.Add("Server-level query processing policies cannot use Allow.");
            if (value.ProcessingOrder is < 1 or > 100_000)
                problems.Add("processingOrder must be between 1 and 100000.");
            var criteria = new[] { value.ClientSubnet, value.Fqdn, value.QueryType, value.TransportProtocol, value.InternetProtocol, value.ServerInterfaceIp };
            if (criteria.All(x => x is null or { Values.Count: 0 }))
                problems.Add("At least one query policy criterion is required.");
            if (criteria.Where(x => x is not null).Any(x => !Enum.IsDefined(x!.Operator)
                || x.Values.Count is < 1 or > 64 || x.Values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 256
                    || v.Any(char.IsControl) || v.Contains(',') || v.Contains(';'))))
                problems.Add("Query policy criteria contain invalid values.");
            if (value.ZoneScopes.Count > 32 || value.ZoneScopes.Any(x => string.IsNullOrWhiteSpace(x.Name)
                || x.Name.Length > 256 || x.Weight is < 1 or > 10_000))
                problems.Add("Zone scope weights are invalid.");
            if (value.ZoneScopes.Count > 0 && (value.Level != HostAgentDnsPolicyLevel.Zone
                || value.Decision != HostAgentDnsPolicyDecision.Allow))
                problems.Add("Zone scopes require a zone-level Allow policy.");
        }
    }

    private static bool IsCidr(string value, AddressFamily family, int maxPrefix)
    {
        var parts = value.Trim().Split('/');
        return parts.Length == 2 && IPAddress.TryParse(parts[0], out var address)
            && address.AddressFamily == family && int.TryParse(parts[1], out var prefix)
            && prefix >= 0 && prefix <= maxPrefix;
    }

    private static bool IsBoundedDnsName(string? value)
    {
        var normalized = value?.Trim().TrimEnd('.');
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= 253
            && !normalized.Any(char.IsControl) && Uri.CheckHostName(normalized) == UriHostNameType.Dns;
    }

    private static string? NormalizeThumbprint(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : string.Concat(value.Where(Uri.IsHexDigit));

    private static bool TryDecodeBase64(string value, out int decodedLength)
    {
        decodedLength = 0;
        var buffer = new byte[((value.Length * 3) + 3) / 4];
        if (!Convert.TryFromBase64String(value, buffer, out decodedLength))
        {
            return false;
        }

        return true;
    }

    private static bool ValidateDnsRecordValues(string type, IReadOnlyList<string> values)
    {
        static bool UInt16(string value) => ushort.TryParse(value, out _);
        static bool Address(string value, AddressFamily family) =>
            IPAddress.TryParse(value, out var address) && address.AddressFamily == family;
        return type switch
        {
            "A" => values.Count == 1 && Address(values[0], AddressFamily.InterNetwork),
            "AAAA" => values.Count == 1 && Address(values[0], AddressFamily.InterNetworkV6),
            "CNAME" or "NS" or "PTR" or "TXT" => values.Count == 1,
            "MX" => values.Count == 2 && UInt16(values[0]),
            "SRV" => values.Count == 4 && values.Take(3).All(UInt16),
            _ => false,
        };
    }

    private static bool IsJsonObject(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 16 });
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentResponseStatus
{
    Ok = 0,

    /// <summary>The request was malformed or asked for something outside the contract.</summary>
    Rejected = 1,

    /// <summary>The caller is not permitted to invoke this operation.</summary>
    Denied = 2,

    /// <summary>The operation was attempted and failed. Details are in the agent's own log.</summary>
    Failed = 3,

    /// <summary>Accepted and running; poll <see cref="HostAgentOperation.GetUpdateStatus"/>.</summary>
    Accepted = 4,
}

/// <summary>
/// One response across the pipe. Everything here is safe to surface in the ITAdmin UI: no
/// file-system paths beyond the ones an administrator already sees, no repository internals, no
/// key material, and no exception text.
/// </summary>
public sealed record HostAgentResponse
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; } = HostAgentProtocol.ProtocolVersion;

    [JsonPropertyName("status")]
    public HostAgentResponseStatus Status { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("installation")]
    public HostAgentInstallationStatus? Installation { get; init; }

    [JsonPropertyName("update")]
    public HostAgentUpdateStatus? Update { get; init; }

    [JsonPropertyName("availability")]
    public HostAgentUpdateAvailability? Availability { get; init; }

    [JsonPropertyName("https")]
    public HostAgentHttpsStatus? Https { get; init; }

    [JsonPropertyName("dnsProbe")]
    public HostAgentDnsProbeResult? DnsProbe { get; init; }

    [JsonPropertyName("dnsInventoryPage")]
    public HostAgentDnsInventoryPage? DnsInventoryPage { get; init; }

    [JsonPropertyName("dnsRecordMutation")]
    public HostAgentDnsRecordMutationResult? DnsRecordMutation { get; init; }

    [JsonPropertyName("dnsZoneMutation")]
    public HostAgentDnsZoneMutationResult? DnsZoneMutation { get; init; }

    [JsonPropertyName("dnsServerSettings")]
    public HostAgentDnsServerSettingsResult? DnsServerSettings { get; init; }

    [JsonPropertyName("dnsPolicyConfiguration")]
    public HostAgentDnsPolicyConfigurationResult? DnsPolicyConfiguration { get; init; }

    [JsonPropertyName("dnssecConfiguration")]
    public HostAgentDnssecConfigurationResult? DnssecConfiguration { get; init; }
    [JsonPropertyName("dnsScavengingConfiguration")]
    public HostAgentDnsScavengingConfigurationResult? DnsScavengingConfiguration { get; init; }
    [JsonPropertyName("dnsNetworkConfiguration")]
    public HostAgentDnsNetworkConfigurationResult? DnsNetworkConfiguration { get; init; }
    [JsonPropertyName("dnsZoneTransferConfiguration")]
    public HostAgentDnsZoneTransferConfigurationResult? DnsZoneTransferConfiguration { get; init; }
    [JsonPropertyName("dnsZoneDelegationConfiguration")]
    public HostAgentDnsZoneDelegationConfigurationResult? DnsZoneDelegationConfiguration { get; init; }

    [JsonPropertyName("repositoryStatus")]
    public HostAgentRepositoryStatus RepositoryStatus { get; init; } = HostAgentRepositoryStatus.Unknown;

    public string ToJson() => JsonSerializer.Serialize(this, HostAgentProtocol.Json);

    public static HostAgentResponse? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HostAgentResponse>(json, HostAgentProtocol.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static HostAgentResponse Ok(string message, string? correlationId = null) =>
        new() { Status = HostAgentResponseStatus.Ok, Message = message, CorrelationId = correlationId };

    public static HostAgentResponse Rejected(string message, string? correlationId = null) =>
        new() { Status = HostAgentResponseStatus.Rejected, Message = message, CorrelationId = correlationId };

    public static HostAgentResponse Denied(string message, string? correlationId = null) =>
        new() { Status = HostAgentResponseStatus.Denied, Message = message, CorrelationId = correlationId };

    public static HostAgentResponse Failed(string message, string? correlationId = null) =>
        new() { Status = HostAgentResponseStatus.Failed, Message = message, CorrelationId = correlationId };
}

public sealed record HostAgentDnsProbeResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("failureKind")]
    public string? FailureKind { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("networkReachable")]
    public bool NetworkReachable { get; init; }

    [JsonPropertyName("tlsValidated")]
    public bool TlsValidated { get; init; }

    [JsonPropertyName("authenticationSucceeded")]
    public bool AuthenticationSucceeded { get; init; }

    [JsonPropertyName("dnsModuleAvailable")]
    public bool DnsModuleAvailable { get; init; }

    [JsonPropertyName("dnsServiceReachable")]
    public bool DnsServiceReachable { get; init; }

    [JsonPropertyName("operatingSystemVersion")]
    public string? OperatingSystemVersion { get; init; }

    [JsonPropertyName("powerShellVersion")]
    public string? PowerShellVersion { get; init; }

    [JsonPropertyName("dnsModuleVersion")]
    public string? DnsModuleVersion { get; init; }

    [JsonPropertyName("dnsServerVersion")]
    public string? DnsServerVersion { get; init; }

    [JsonPropertyName("zoneCount")]
    public int? ZoneCount { get; init; }

    [JsonPropertyName("capabilities")]
    public HostAgentDnsCapabilities? Capabilities { get; init; }
}

public sealed record HostAgentDnsCapabilities
{
    [JsonPropertyName("zones")]
    public bool Zones { get; init; }
    [JsonPropertyName("records")]
    public bool Records { get; init; }
    [JsonPropertyName("serverSettings")]
    public bool ServerSettings { get; init; }
    [JsonPropertyName("dnssec")]
    public bool Dnssec { get; init; }
    [JsonPropertyName("policies")]
    public bool Policies { get; init; }
    [JsonPropertyName("scopes")]
    public bool Scopes { get; init; }
    [JsonPropertyName("cache")]
    public bool Cache { get; init; }
    [JsonPropertyName("networkConfiguration")]
    public bool NetworkConfiguration { get; init; }
    [JsonPropertyName("zoneTransfers")]
    public bool ZoneTransfers { get; init; }
    [JsonPropertyName("zoneDelegations")]
    public bool ZoneDelegations { get; init; }
}

public sealed record HostAgentDnsInventoryPage
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    [JsonPropertyName("failureKind")]
    public string? FailureKind { get; init; }
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; init; }
    [JsonPropertyName("zones")]
    public IReadOnlyList<HostAgentDnsZoneInventoryItem> Zones { get; init; } = [];
    [JsonPropertyName("records")]
    public IReadOnlyList<HostAgentDnsRecordInventoryItem> Records { get; init; } = [];
}

public sealed record HostAgentDnsZoneInventoryItem
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("zoneType")]
    public string ZoneType { get; init; } = string.Empty;
    [JsonPropertyName("isReverseLookupZone")]
    public bool IsReverseLookupZone { get; init; }
    [JsonPropertyName("isDsIntegrated")]
    public bool IsDsIntegrated { get; init; }
    [JsonPropertyName("isSigned")]
    public bool IsSigned { get; init; }
    [JsonPropertyName("isPaused")]
    public bool IsPaused { get; init; }
    [JsonPropertyName("dynamicUpdate")]
    public string? DynamicUpdate { get; init; }
    [JsonPropertyName("replicationScope")]
    public string? ReplicationScope { get; init; }
    [JsonPropertyName("directoryPartitionName")]
    public string? DirectoryPartitionName { get; init; }
    [JsonPropertyName("zoneFile")]
    public string? ZoneFile { get; init; }
    [JsonPropertyName("virtualizationInstance")]
    public string? VirtualizationInstance { get; init; }
    [JsonPropertyName("zoneScopes")]
    public IReadOnlyList<string> ZoneScopes { get; init; } = [];
    [JsonPropertyName("isAutoCreated")]
    public bool IsAutoCreated { get; init; }
    [JsonPropertyName("masterServers")]
    public IReadOnlyList<string> MasterServers { get; init; } = [];
    [JsonPropertyName("forwarderTimeoutSeconds")]
    public int? ForwarderTimeoutSeconds { get; init; }
    [JsonPropertyName("useRecursion")]
    public bool? UseRecursion { get; init; }
}

public sealed record HostAgentDnsRecordInventoryItem
{
    [JsonPropertyName("relativeName")]
    public string RelativeName { get; init; } = string.Empty;
    [JsonPropertyName("recordType")]
    public string RecordType { get; init; } = string.Empty;
    [JsonPropertyName("recordDataJson")]
    public string RecordDataJson { get; init; } = "{}";
    [JsonPropertyName("timeToLiveSeconds")]
    public int TimeToLiveSeconds { get; init; }
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }
    [JsonPropertyName("zoneScope")]
    public string? ZoneScope { get; init; }
    [JsonPropertyName("virtualizationInstance")]
    public string? VirtualizationInstance { get; init; }
}

public sealed record HostAgentDnsRecordMutationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    [JsonPropertyName("failureKind")]
    public string? FailureKind { get; init; }
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")]
    public HostAgentDnsRecordInventoryItem? Before { get; init; }
    [JsonPropertyName("after")]
    public HostAgentDnsRecordInventoryItem? After { get; init; }
}

public sealed record HostAgentDnsZoneMutationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    [JsonPropertyName("failureKind")]
    public string? FailureKind { get; init; }
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")]
    public HostAgentDnsZoneInventoryItem? Before { get; init; }
    [JsonPropertyName("after")]
    public HostAgentDnsZoneInventoryItem? After { get; init; }
}

public sealed record HostAgentDnsServerSettings
{
    [JsonPropertyName("forwarderAddresses")]
    public IReadOnlyList<string> ForwarderAddresses { get; init; } = [];
    [JsonPropertyName("forwarderUseRootHint")]
    public bool ForwarderUseRootHint { get; init; }
    [JsonPropertyName("forwarderTimeoutSeconds")]
    public int ForwarderTimeoutSeconds { get; init; }
    [JsonPropertyName("forwarderEnableReordering")]
    public bool ForwarderEnableReordering { get; init; }
    [JsonPropertyName("recursionEnabled")]
    public bool RecursionEnabled { get; init; }
    [JsonPropertyName("recursionAdditionalTimeoutSeconds")]
    public int RecursionAdditionalTimeoutSeconds { get; init; }
    [JsonPropertyName("recursionRetryIntervalSeconds")]
    public int RecursionRetryIntervalSeconds { get; init; }
    [JsonPropertyName("recursionTimeoutSeconds")]
    public int RecursionTimeoutSeconds { get; init; }
    [JsonPropertyName("recursionSecureResponse")]
    public bool RecursionSecureResponse { get; init; }
}

public sealed record HostAgentDnsServerSettingsResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    [JsonPropertyName("failureKind")]
    public string? FailureKind { get; init; }
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")]
    public HostAgentDnsServerSettings? Before { get; init; }
    [JsonPropertyName("after")]
    public HostAgentDnsServerSettings? After { get; init; }
}

public sealed record HostAgentDnsClientSubnet
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("ipv4Subnets")] public IReadOnlyList<string> Ipv4Subnets { get; init; } = [];
    [JsonPropertyName("ipv6Subnets")] public IReadOnlyList<string> Ipv6Subnets { get; init; } = [];
}

public sealed record HostAgentDnsZoneScope
{
    [JsonPropertyName("zoneName")] public string ZoneName { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
}

public sealed record HostAgentDnsQueryPolicy
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("level")] public string Level { get; init; } = string.Empty;
    [JsonPropertyName("zoneName")] public string? ZoneName { get; init; }
    [JsonPropertyName("action")] public string Action { get; init; } = string.Empty;
    [JsonPropertyName("condition")] public string Condition { get; init; } = string.Empty;
    [JsonPropertyName("processingOrder")] public int ProcessingOrder { get; init; }
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("clientSubnet")] public string? ClientSubnet { get; init; }
    [JsonPropertyName("fqdn")] public string? Fqdn { get; init; }
    [JsonPropertyName("queryType")] public string? QueryType { get; init; }
    [JsonPropertyName("transportProtocol")] public string? TransportProtocol { get; init; }
    [JsonPropertyName("internetProtocol")] public string? InternetProtocol { get; init; }
    [JsonPropertyName("serverInterfaceIp")] public string? ServerInterfaceIp { get; init; }
    [JsonPropertyName("zoneScope")] public string? ZoneScope { get; init; }
}

public sealed record HostAgentDnsPolicyConfiguration
{
    [JsonPropertyName("clientSubnets")] public IReadOnlyList<HostAgentDnsClientSubnet> ClientSubnets { get; init; } = [];
    [JsonPropertyName("zoneScopes")] public IReadOnlyList<HostAgentDnsZoneScope> ZoneScopes { get; init; } = [];
    [JsonPropertyName("queryPolicies")] public IReadOnlyList<HostAgentDnsQueryPolicy> QueryPolicies { get; init; } = [];
}

public sealed record HostAgentDnsPolicyConfigurationResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("failureKind")] public string? FailureKind { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")] public HostAgentDnsPolicyConfiguration? Before { get; init; }
    [JsonPropertyName("after")] public HostAgentDnsPolicyConfiguration? After { get; init; }
}

public sealed record HostAgentDnssecSigningKey
{
    [JsonPropertyName("keyId")] public Guid KeyId { get; init; }
    [JsonPropertyName("keyType")] public string KeyType { get; init; } = string.Empty;
    [JsonPropertyName("cryptoAlgorithm")] public string? CryptoAlgorithm { get; init; }
    [JsonPropertyName("keyLength")] public int? KeyLength { get; init; }
    [JsonPropertyName("keyStatus")] public string? KeyStatus { get; init; }
    [JsonPropertyName("keyStorageProvider")] public string? KeyStorageProvider { get; init; }
    [JsonPropertyName("isRolloverEnabled")] public bool? IsRolloverEnabled { get; init; }
    [JsonPropertyName("rolloverPeriodSeconds")] public long? RolloverPeriodSeconds { get; init; }
    [JsonPropertyName("nextRolloverAction")] public string? NextRolloverAction { get; init; }
    [JsonPropertyName("nextRolloverTime")] public DateTimeOffset? NextRolloverTime { get; init; }
}

public sealed record HostAgentDnssecZone
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("zoneType")] public string ZoneType { get; init; } = string.Empty;
    [JsonPropertyName("isDsIntegrated")] public bool IsDsIntegrated { get; init; }
    [JsonPropertyName("isAutoCreated")] public bool IsAutoCreated { get; init; }
    [JsonPropertyName("isSigned")] public bool IsSigned { get; init; }
    [JsonPropertyName("isEligibleForSigning")] public bool IsEligibleForSigning { get; init; }
    [JsonPropertyName("ineligibilityReason")] public string? IneligibilityReason { get; init; }
    [JsonPropertyName("isKeyMasterServer")] public bool? IsKeyMasterServer { get; init; }
    [JsonPropertyName("keyMasterServer")] public string? KeyMasterServer { get; init; }
    [JsonPropertyName("keyMasterStatus")] public string? KeyMasterStatus { get; init; }
    [JsonPropertyName("denialOfExistence")] public string? DenialOfExistence { get; init; }
    [JsonPropertyName("nsec3Iterations")] public int? Nsec3Iterations { get; init; }
    [JsonPropertyName("nsec3OptOut")] public bool? Nsec3OptOut { get; init; }
    [JsonPropertyName("dnsKeyRecordSetTtlSeconds")] public long? DnsKeyRecordSetTtlSeconds { get; init; }
    [JsonPropertyName("dsRecordSetTtlSeconds")] public long? DsRecordSetTtlSeconds { get; init; }
    [JsonPropertyName("dsRecordGenerationAlgorithms")] public IReadOnlyList<string> DsRecordGenerationAlgorithms { get; init; } = [];
    [JsonPropertyName("parentHasSecureDelegation")] public bool? ParentHasSecureDelegation { get; init; }
    [JsonPropertyName("signingKeys")] public IReadOnlyList<HostAgentDnssecSigningKey> SigningKeys { get; init; } = [];
}

public sealed record HostAgentDnssecConfiguration
{
    [JsonPropertyName("zones")] public IReadOnlyList<HostAgentDnssecZone> Zones { get; init; } = [];
    [JsonPropertyName("resolver")] public HostAgentDnssecResolverConfiguration Resolver { get; init; } = new();
}

public sealed record HostAgentDnssecTrustAnchor
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("data")] public string? Data { get; init; }
}

public sealed record HostAgentDnssecTrustPoint
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("lastActiveRefreshTime")] public DateTimeOffset? LastActiveRefreshTime { get; init; }
    [JsonPropertyName("nextActiveRefreshTime")] public DateTimeOffset? NextActiveRefreshTime { get; init; }
    [JsonPropertyName("anchors")] public IReadOnlyList<HostAgentDnssecTrustAnchor> Anchors { get; init; } = [];
}

public sealed record HostAgentDnssecResolverConfiguration
{
    [JsonPropertyName("validationEnabled")] public bool ValidationEnabled { get; init; }
    [JsonPropertyName("isReadOnlyDomainController")] public bool IsReadOnlyDomainController { get; init; }
    [JsonPropertyName("directoryServicesAvailable")] public bool DirectoryServicesAvailable { get; init; }
    [JsonPropertyName("rootTrustAnchorsUrl")] public string? RootTrustAnchorsUrl { get; init; }
    [JsonPropertyName("trustPoints")] public IReadOnlyList<HostAgentDnssecTrustPoint> TrustPoints { get; init; } = [];
}

public sealed record HostAgentDnssecConfigurationResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("failureKind")] public string? FailureKind { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")] public HostAgentDnssecConfiguration? Before { get; init; }
    [JsonPropertyName("after")] public HostAgentDnssecConfiguration? After { get; init; }
}

public sealed record HostAgentDnsZoneAging
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("zoneType")] public string ZoneType { get; init; } = string.Empty;
    [JsonPropertyName("agingEnabled")] public bool AgingEnabled { get; init; }
    [JsonPropertyName("isEligible")] public bool IsEligible { get; init; }
    [JsonPropertyName("ineligibilityReason")] public string? IneligibilityReason { get; init; }
    [JsonPropertyName("noRefreshIntervalSeconds")] public long NoRefreshIntervalSeconds { get; init; }
    [JsonPropertyName("refreshIntervalSeconds")] public long RefreshIntervalSeconds { get; init; }
    [JsonPropertyName("availableForScavengeTime")] public DateTimeOffset? AvailableForScavengeTime { get; init; }
    [JsonPropertyName("scavengeServers")] public IReadOnlyList<string> ScavengeServers { get; init; } = [];
}

public sealed record HostAgentDnsScavengingConfiguration
{
    [JsonPropertyName("scavengingEnabled")]
    public bool ScavengingEnabled { get; init; }
    [JsonPropertyName("scavengingIntervalSeconds")]
    public long ScavengingIntervalSeconds { get; init; }
    [JsonPropertyName("defaultNoRefreshIntervalSeconds")]
    public long DefaultNoRefreshIntervalSeconds { get; init; }
    [JsonPropertyName("defaultRefreshIntervalSeconds")]
    public long DefaultRefreshIntervalSeconds { get; init; }
    [JsonPropertyName("lastScavengeTime")]
    public DateTimeOffset? LastScavengeTime { get; init; }
    [JsonPropertyName("zones")]
    public IReadOnlyList<HostAgentDnsZoneAging> Zones { get; init; } = [];
}

public sealed record HostAgentDnsScavengingConfigurationResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("failureKind")] public string? FailureKind { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")] public HostAgentDnsScavengingConfiguration? Before { get; init; }
    [JsonPropertyName("after")] public HostAgentDnsScavengingConfiguration? After { get; init; }
}

public sealed record HostAgentDnsRootHint
{
    [JsonPropertyName("nameServer")] public string NameServer { get; init; } = string.Empty;
    [JsonPropertyName("ipAddresses")] public IReadOnlyList<string> IpAddresses { get; init; } = [];
}

public sealed record HostAgentDnsNetworkConfiguration
{
    [JsonPropertyName("listeningIpAddresses")] public IReadOnlyList<string> ListeningIpAddresses { get; init; } = [];
    [JsonPropertyName("availableIpAddresses")] public IReadOnlyList<string> AvailableIpAddresses { get; init; } = [];
    [JsonPropertyName("rootHints")] public IReadOnlyList<HostAgentDnsRootHint> RootHints { get; init; } = [];
}

public sealed record HostAgentDnsNetworkConfigurationResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("failureKind")] public string? FailureKind { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")] public HostAgentDnsNetworkConfiguration? Before { get; init; }
    [JsonPropertyName("after")] public HostAgentDnsNetworkConfiguration? After { get; init; }
}

public sealed record HostAgentDnsZoneTransferSetting
{
    [JsonPropertyName("zoneName")] public string ZoneName { get; init; } = string.Empty;
    [JsonPropertyName("isDsIntegrated")] public bool IsDsIntegrated { get; init; }
    [JsonPropertyName("transferMode")] public HostAgentDnsZoneTransferMode TransferMode { get; init; }
    [JsonPropertyName("secondaryServers")] public IReadOnlyList<string> SecondaryServers { get; init; } = [];
    [JsonPropertyName("notifyMode")] public HostAgentDnsZoneNotifyMode NotifyMode { get; init; }
    [JsonPropertyName("notifyServers")] public IReadOnlyList<string> NotifyServers { get; init; } = [];
}

public sealed record HostAgentDnsZoneTransferConfiguration
{
    [JsonPropertyName("zones")] public IReadOnlyList<HostAgentDnsZoneTransferSetting> Zones { get; init; } = [];
}

public sealed record HostAgentDnsZoneTransferConfigurationResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("failureKind")] public string? FailureKind { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")] public HostAgentDnsZoneTransferConfiguration? Before { get; init; }
    [JsonPropertyName("after")] public HostAgentDnsZoneTransferConfiguration? After { get; init; }
}

public sealed record HostAgentDnsZoneDelegationNameServer
{
    [JsonPropertyName("nameServer")] public string NameServer { get; init; } = string.Empty;
    [JsonPropertyName("ipAddresses")] public IReadOnlyList<string> IpAddresses { get; init; } = [];
}

public sealed record HostAgentDnsZoneDelegation
{
    [JsonPropertyName("parentZoneName")] public string ParentZoneName { get; init; } = string.Empty;
    [JsonPropertyName("childZoneName")] public string ChildZoneName { get; init; } = string.Empty;
    [JsonPropertyName("nameServers")] public IReadOnlyList<HostAgentDnsZoneDelegationNameServer> NameServers { get; init; } = [];
}

public sealed record HostAgentDnsZoneDelegationConfiguration
{
    [JsonPropertyName("parentZones")] public IReadOnlyList<string> ParentZones { get; init; } = [];
    [JsonPropertyName("delegations")] public IReadOnlyList<HostAgentDnsZoneDelegation> Delegations { get; init; } = [];
}

public sealed record HostAgentDnsZoneDelegationConfigurationResult
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("failureKind")] public string? FailureKind { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("before")] public HostAgentDnsZoneDelegationConfiguration? Before { get; init; }
    [JsonPropertyName("after")] public HostAgentDnsZoneDelegationConfiguration? After { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentRepositoryStatus
{
    Unknown = 0,
    Verified = 1,
    RepositoryRejected = 2,
    HostUnreachable = 3,
}

public sealed record HostAgentInstallationStatus
{
    [JsonPropertyName("phase")]
    public string Phase { get; init; } = string.Empty;

    /// <summary>Short commit the live build was produced from.</summary>
    [JsonPropertyName("activeCommit")]
    public string? ActiveCommit { get; init; }

    [JsonPropertyName("previousCommit")]
    public string? PreviousCommit { get; init; }

    [JsonPropertyName("branch")]
    public string Branch { get; init; } = "main";

    [JsonPropertyName("builtAtUtc")]
    public DateTimeOffset? BuiltAtUtc { get; init; }

    [JsonPropertyName("healthy")]
    public bool Healthy { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentUpdatePhase
{
    Idle = 0,
    Pulling = 1,
    Building = 2,
    Migrating = 3,
    Activating = 4,
    Completed = 5,
    Failed = 6,
    RequiresOperatorReview = 7,
}

public sealed record HostAgentUpdateStatus
{
    [JsonPropertyName("operationId")]
    public string? OperationId { get; init; }

    [JsonPropertyName("phase")]
    public HostAgentUpdatePhase Phase { get; init; }

    [JsonPropertyName("targetCommit")]
    public string? TargetCommit { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public DateTimeOffset? StartedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTimeOffset? CompletedAtUtc { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// How far the deployed build is behind the configured branch. Sanitised: the short commits and the
/// latest commit subject are meaningful to an administrator; the remote URL and local paths are not.
/// </summary>
public sealed record HostAgentUpdateAvailability
{
    [JsonPropertyName("upToDate")]
    public bool UpToDate { get; init; }

    [JsonPropertyName("commitsBehind")]
    public int CommitsBehind { get; init; }

    [JsonPropertyName("currentCommit")]
    public string? CurrentCommit { get; init; }

    [JsonPropertyName("latestCommit")]
    public string? LatestCommit { get; init; }

    [JsonPropertyName("latestSubject")]
    public string? LatestSubject { get; init; }

    [JsonPropertyName("branch")]
    public string Branch { get; init; } = "main";
}

/// <summary>
/// The site's HTTPS state. Certificate subject and expiry are useful to an administrator; the
/// private key, the PFX password, and file-system paths never appear here.
/// </summary>
public sealed record HostAgentHttpsStatus
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("redirectHttpToHttps")]
    public bool RedirectHttpToHttps { get; init; }

    [JsonPropertyName("certificateThumbprint")]
    public string? CertificateThumbprint { get; init; }

    [JsonPropertyName("certificateSubject")]
    public string? CertificateSubject { get; init; }

    [JsonPropertyName("certificateNotAfterUtc")]
    public DateTimeOffset? CertificateNotAfterUtc { get; init; }
}
