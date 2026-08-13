using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Deployment;

namespace ITAdmin.HostAgent;

/// <summary>
/// The agent's own configuration, written by the installer under
/// <c>%ProgramData%\ITAdmin\config\hostagent.json</c>.
///
/// <para>
/// It records where the repository is and which channel this host follows - but not the deploy key
/// itself. The key stays a file on disk whose ACL grants SYSTEM and Administrators only; this file
/// merely says where to look for it. That distinction is what keeps the boundary honest: an
/// attacker who can read the agent's configuration still cannot read the key, and the web
/// application can read neither.
/// </para>
/// </summary>
public sealed record HostAgentSettings
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "hostagent.json";

    /// <summary>Deploy key file name under the machine key directory.</summary>
    public const string DeployKeyFileName = RepositoryAccessContract.DeployKeyFileName;

    /// <summary>Machine-owned known-hosts file name under the same directory.</summary>
    public const string KnownHostsFileName = RepositoryAccessContract.KnownHostsFileName;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>SSH remote of the ITAdmin repository, as discovered from the bootstrap clone.</summary>
    [JsonPropertyName("repositoryUrl")]
    public string RepositoryUrl { get; init; } = string.Empty;

    [JsonPropertyName("channel")]
    public ReleaseChannel Channel { get; init; } = ReleaseChannel.Stable;

    /// <summary>
    /// Directory holding the read-only deploy key and the verified host-key entries. Never inside
    /// the web root, and ACL'd to SYSTEM and Administrators only.
    ///
    /// <para>
    /// Both files are machine-owned copies. The agent must not depend on an interactive
    /// administrator's user profile: that account may be disabled, its profile may be removed, and
    /// LocalSystem cannot read it in any case.
    /// </para>
    /// </summary>
    [JsonPropertyName("deployKeyDirectory")]
    public string DeployKeyDirectory { get; init; } = string.Empty;

    [JsonPropertyName("programFilesRoot")]
    public string ProgramFilesRoot { get; init; } = DeploymentLayout.DefaultProgramFilesRoot;

    [JsonPropertyName("programDataRoot")]
    public string ProgramDataRoot { get; init; } = DeploymentLayout.DefaultProgramDataRoot;

    [JsonPropertyName("siteName")]
    public string SiteName { get; init; } = "ITAdmin";

    [JsonPropertyName("appPoolName")]
    public string AppPoolName { get; init; } = "ITAdmin";

    /// <summary>
    /// Whether the agent may act on an update request at all. Defaults to false so a freshly
    /// installed host cannot be talked into replacing its own release before an administrator has
    /// deliberately turned in-app updates on.
    /// </summary>
    [JsonPropertyName("updatesEnabled")]
    public bool UpdatesEnabled { get; init; }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string DeployKeyPath => Path.Combine(DeployKeyDirectory, DeployKeyFileName);

    /// <summary>
    /// Machine-owned known-hosts file. Used with <c>StrictHostKeyChecking=yes</c>, so the agent
    /// trusts exactly the host keys the operator verified during preparation and nothing else.
    /// </summary>
    public string KnownHostsPath => Path.Combine(DeployKeyDirectory, KnownHostsFileName);

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static HostAgentSettings? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HostAgentSettings>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (SchemaVersion != CurrentSchemaVersion)
        {
            problems.Add($"Unsupported hostagent.json schemaVersion {SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(RepositoryUrl))
        {
            problems.Add("repositoryUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(DeployKeyDirectory))
        {
            problems.Add("deployKeyDirectory is required.");
        }

        if (string.IsNullOrWhiteSpace(AppPoolName))
        {
            problems.Add("appPoolName is required.");
        }

        return problems;
    }

    /// <summary>
    /// Fields that must never appear in this file. The deploy key is a file with an ACL, not a
    /// configuration value, and inlining it would make every backup of ProgramData a key leak.
    /// </summary>
    public IReadOnlyList<string> FindDisallowedSecretFields()
    {
        var json = ToJson();
        return new[] { "privateKey", "BEGIN OPENSSH", "BEGIN RSA", "passphrase", "password" }
            .Where(forbidden => json.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
