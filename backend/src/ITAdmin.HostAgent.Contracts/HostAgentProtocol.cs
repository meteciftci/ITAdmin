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
/// configuration; DNS connection values can only be used by the fixed capability probe.
/// </para>
/// </summary>
public static class HostAgentProtocol
{
    public const int ProtocolVersion = 4;

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
            or HostAgentOperation.ReadDnsServerInventoryPage)
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

        return problems;
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
