using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.HostAgent.Contracts;

/// <summary>
/// The wire contract between the ITAdmin web application and the privileged ITAdmin Host Agent.
///
/// <para>
/// <b>Why a separate privileged component at all.</b> Updating a release, repointing an IIS site,
/// binding a certificate, and recycling an app pool are machine-administrator operations. The web
/// application is internet-facing-shaped code that parses untrusted input all day; giving its app
/// pool the rights to do those things would mean any request-handling flaw becomes machine
/// compromise. So the app pool identity keeps exactly the rights it has today - read its release,
/// write its logs and key ring - and a separate service running as LocalSystem performs the small,
/// fixed set of operations that genuinely need privilege.
/// </para>
///
/// <para>
/// <b>Why named pipes.</b> The boundary has to authenticate the caller, and on Windows a named pipe
/// gives that for free: the server calls <c>GetImpersonationUserName</c> / impersonates to learn the
/// connecting principal, and a pipe ACL restricts who may connect at all - enforced by the kernel,
/// not by a token the application has to store, rotate, and protect. A localhost TCP listener would
/// have neither property: any local process could connect, and the agent would need its own
/// authentication scheme with its own secret. Pipes are also machine-local by construction, so
/// there is no port to accidentally expose.
/// </para>
///
/// <para>
/// <b>Why typed operations.</b> Every operation below is a named intent with a fixed payload. There
/// is no "run this command", no script path parameter, and no shell. That is the single most
/// important property of this contract: a compromised web application can ask for an update to a
/// release that the agent independently verifies against the repository, and nothing else. Adding a
/// generic execution operation later would silently undo the whole boundary.
/// </para>
/// </summary>
public static class HostAgentProtocol
{
    public const int ProtocolVersion = 1;

    /// <summary>
    /// Pipe name. Machine-local; the agent applies an ACL so only the app pool identity and
    /// administrators may connect.
    /// </summary>
    public const string PipeName = "ITAdmin.HostAgent";

    /// <summary>
    /// Cap on a single request or response frame. Bounded so a malformed or hostile length prefix
    /// cannot make the privileged process allocate arbitrarily.
    /// </summary>
    public const int MaxFrameBytes = 1 << 20;

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// The complete set of things the web application may ask the privileged agent to do.
///
/// <para>
/// This enum is the boundary. It is intentionally short, and every value is a specific intent whose
/// parameters the agent validates independently - it never trusts the caller's version numbers,
/// paths, or thumbprints without re-deriving them from the repository or the certificate store.
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentOperation
{
    /// <summary>Liveness and version of the agent itself.</summary>
    Ping = 0,

    /// <summary>Installed version, available version, and lifecycle phase. Read-only.</summary>
    GetInstallationStatus = 1,

    /// <summary>
    /// Ask the repository which releases exist on the configured channel. Read-only; performed by
    /// the agent using the deploy key, which the web application cannot read.
    /// </summary>
    CheckForUpdates = 2,

    /// <summary>
    /// Request that a specific release be fetched, verified, staged, migrated, and activated. The
    /// requested version must be one the agent itself resolved from an annotated stable tag.
    /// </summary>
    RequestUpdate = 3,

    /// <summary>Progress and outcome of the most recent update request.</summary>
    GetUpdateStatus = 4,

    /// <summary>
    /// Apply the host/HTTPS settings an administrator saved in ITAdmin Settings to the IIS site:
    /// host header, certificate selection, HTTP-to-HTTPS redirect.
    /// </summary>
    ReconcileWebBindings = 5,

    /// <summary>Recycle the ITAdmin application pool. The narrowest useful service operation.</summary>
    RecycleApplicationPool = 6,
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

    /// <summary>
    /// Release version for <see cref="HostAgentOperation.RequestUpdate"/>. Advisory: the agent
    /// resolves the release independently and refuses anything that does not match a real annotated
    /// stable tag, so a caller cannot name an arbitrary ref or path here.
    /// </summary>
    [JsonPropertyName("targetVersion")]
    public string? TargetVersion { get; init; }

    /// <summary>Desired public host name for <see cref="HostAgentOperation.ReconcileWebBindings"/>.</summary>
    [JsonPropertyName("hostName")]
    public string? HostName { get; init; }

    /// <summary>Certificate thumbprint from LocalMachine\My for binding reconciliation.</summary>
    [JsonPropertyName("certificateThumbprint")]
    public string? CertificateThumbprint { get; init; }

    [JsonPropertyName("enableHttps")]
    public bool? EnableHttps { get; init; }

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

    /// <summary>
    /// Shape validation performed by the agent before any privileged work begins. Returns every
    /// problem rather than the first, and never reflects caller-supplied text back into a message
    /// that could later reach a UI unescaped.
    /// </summary>
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

        switch (Operation)
        {
            case HostAgentOperation.RequestUpdate when string.IsNullOrWhiteSpace(TargetVersion):
                problems.Add("targetVersion is required for RequestUpdate.");
                break;

            case HostAgentOperation.RequestUpdate when !IsPlainVersion(TargetVersion):
                // A version is used to build a ref name and a directory name. Rejecting anything
                // that is not digits, dots, and a plain pre-release label keeps a caller from
                // steering either one.
                problems.Add("targetVersion must be a plain MAJOR.MINOR.PATCH[-prerelease] version.");
                break;

            case HostAgentOperation.ReconcileWebBindings:
                if (EnableHttps == true && string.IsNullOrWhiteSpace(CertificateThumbprint))
                {
                    problems.Add("certificateThumbprint is required when enabling HTTPS.");
                }

                if (!string.IsNullOrWhiteSpace(CertificateThumbprint) && !IsThumbprint(CertificateThumbprint))
                {
                    problems.Add("certificateThumbprint must be 40 hexadecimal characters.");
                }

                if (!string.IsNullOrWhiteSpace(HostName) && !IsPlainHostName(HostName))
                {
                    problems.Add("hostName must be a valid host name.");
                }

                if (RedirectHttpToHttps == true && EnableHttps != true)
                {
                    problems.Add("redirectHttpToHttps requires HTTPS to be enabled.");
                }

                break;
        }

        return problems;
    }

    internal static bool IsPlainVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return false;
        }

        var text = value.Trim();
        var hyphen = text.IndexOf('-');
        if (hyphen >= 0)
        {
            var label = text[(hyphen + 1)..];
            if (label.Length == 0 || !label.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-'))
            {
                return false;
            }

            text = text[..hyphen];
        }

        var parts = text.Split('.');
        return parts.Length == 3
            && parts.All(part => part.Length > 0 && part.Length <= 9 && part.All(char.IsAsciiDigit));
    }

    internal static bool IsThumbprint(string? value) =>
        value is { Length: 40 } && value.All(Uri.IsHexDigit);

    internal static bool IsPlainHostName(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 253
        && Uri.CheckHostName(value) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
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
/// One response across the pipe.
///
/// <para>
/// Everything here is safe to surface in the ITAdmin UI. There are no file-system paths beyond the
/// ones an administrator already sees, no repository URL, no key material, and no exception text -
/// the agent logs the detail locally and returns a message an operator can act on.
/// </para>
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

    [JsonPropertyName("availableReleases")]
    public IReadOnlyList<HostAgentAvailableRelease>? AvailableReleases { get; init; }

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

public sealed record HostAgentInstallationStatus
{
    [JsonPropertyName("phase")]
    public string Phase { get; init; } = string.Empty;

    [JsonPropertyName("activeVersion")]
    public string? ActiveVersion { get; init; }

    [JsonPropertyName("previousVersion")]
    public string? PreviousVersion { get; init; }

    [JsonPropertyName("channel")]
    public string Channel { get; init; } = "stable";

    [JsonPropertyName("healthy")]
    public bool Healthy { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostAgentUpdatePhase
{
    Idle = 0,
    Resolving = 1,
    Fetching = 2,
    Verifying = 3,
    Staging = 4,
    Migrating = 5,
    Activating = 6,
    Completed = 7,
    Failed = 8,
}

public sealed record HostAgentUpdateStatus
{
    [JsonPropertyName("phase")]
    public HostAgentUpdatePhase Phase { get; init; }

    [JsonPropertyName("targetVersion")]
    public string? TargetVersion { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public DateTimeOffset? StartedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTimeOffset? CompletedAtUtc { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// A release the agent found on the repository. Sanitised on purpose: the version and its source
/// commit are meaningful to an administrator, while the ref name, remote URL, and local paths are
/// deployment-authority detail the web application has no reason to see.
/// </summary>
public sealed record HostAgentAvailableRelease
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("sourceCommit")]
    public string SourceCommit { get; init; } = string.Empty;

    [JsonPropertyName("isInstalled")]
    public bool IsInstalled { get; init; }
}
