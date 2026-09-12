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
    public const int ProtocolVersion = 8;

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
            or HostAgentOperation.ManageDnsPolicyConfiguration)
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
