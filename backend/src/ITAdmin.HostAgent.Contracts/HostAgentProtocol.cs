using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.HostAgent.Contracts;

/// <summary>
/// The wire contract between the ITAdmin web application and the privileged ITAdmin Host Agent.
///
/// <para>
/// <b>Why a separate privileged component at all.</b> Fetching source, building it, running
/// migrations, and repointing an IIS site are machine-administrator operations. The web application
/// is internet-facing-shaped code that parses untrusted input all day; giving its app pool the
/// rights to do those things would mean any request-handling flaw becomes machine compromise. So
/// the app pool identity keeps exactly the rights it has today - read its build, write its logs and
/// key ring - and a separate service running as LocalSystem performs a small, fixed set of
/// operations.
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
/// is no "run this command", no script path parameter, no version string, and no shell. A
/// compromised web application can ask for an update - which the agent applies by running the
/// deployment script that already lives in the checked-out source, with arguments the agent derives
/// entirely from its own configuration - and nothing else.
/// </para>
/// </summary>
public static class HostAgentProtocol
{
    public const int ProtocolVersion = 2;

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
/// the boundary: it is intentionally short and carries no free-form parameters.
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

        return problems;
    }

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
