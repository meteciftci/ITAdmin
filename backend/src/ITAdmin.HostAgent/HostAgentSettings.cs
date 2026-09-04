using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.HostAgent;

/// <summary>
/// The agent's own configuration, written by <c>Deploy-ITAdmin.ps1</c> under
/// <c>%ProgramData%\ITAdmin\config\hostagent.json</c>.
///
/// <para>
/// It records where the public repository is, which branch this host follows, and the machine
/// layout roots. There is no credential here: the repository is public and cloned over anonymous
/// HTTPS, and the deployment script the agent runs is the one already checked out under
/// <c>&lt;InstallRoot&gt;\src</c>.
/// </para>
/// </summary>
public sealed record HostAgentSettings
{
    public const int CurrentSchemaVersion = 2;
    public const string FileName = "hostagent.json";

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Public HTTPS URL of the ITAdmin repository.</summary>
    [JsonPropertyName("repositoryUrl")]
    public string RepositoryUrl { get; init; } = string.Empty;

    /// <summary>Branch this host tracks. Its tip is what an update deploys.</summary>
    [JsonPropertyName("branch")]
    public string Branch { get; init; } = "main";

    /// <summary>Root that holds <c>src\</c>, <c>app\</c>, <c>hostagent\</c>, <c>update-coordinator\</c>.</summary>
    [JsonPropertyName("installRoot")]
    public string InstallRoot { get; init; } = @"C:\ITAdmin";

    /// <summary>Root that holds <c>config\</c>, <c>secrets\</c>, <c>state\</c>, <c>logs\</c>.</summary>
    [JsonPropertyName("dataRoot")]
    public string DataRoot { get; init; } = @"C:\ProgramData\ITAdmin";

    [JsonPropertyName("siteName")]
    public string SiteName { get; init; } = "ITAdmin";

    [JsonPropertyName("appPoolName")]
    public string AppPoolName { get; init; } = "ITAdmin";

    /// <summary>
    /// Whether the agent may act on an update request at all. When the repository is public this
    /// defaults to on; a host that must never self-update can set it to false.
    /// </summary>
    [JsonPropertyName("updatesEnabled")]
    public bool UpdatesEnabled { get; init; } = true;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Working tree the agent fetches into and builds from.</summary>
    public string SourceRoot => Path.Combine(InstallRoot, "src");

    /// <summary>Deployment script the agent runs to apply an update. Always inside the checked-out source.</summary>
    public string DeployScriptPath =>
        Path.Combine(SourceRoot, "scripts", "deploy", "Deploy-ITAdmin.ps1");

    public string ConfigRoot => Path.Combine(DataRoot, "config");
    public string StateRoot => Path.Combine(DataRoot, "state");
    public string LogsRoot => Path.Combine(DataRoot, "logs");

    /// <summary>Where <c>Deploy-ITAdmin.ps1</c> records the active/previous build.</summary>
    public string DeployStatePath => Path.Combine(StateRoot, "deploy.json");

    /// <summary>Where the agent and the Update Coordinator record update progress.</summary>
    public string UpdateOperationPath => Path.Combine(StateRoot, "update-operation.json");

    public string HostAgentBuildsRoot => Path.Combine(InstallRoot, "hostagent");
    public string CoordinatorBuildsRoot => Path.Combine(InstallRoot, "update-coordinator");

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
            problems.Add($"Unsupported hostagent.json schemaVersion {SchemaVersion}; this agent expects {CurrentSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(RepositoryUrl))
        {
            problems.Add("repositoryUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(Branch))
        {
            problems.Add("branch is required.");
        }

        if (string.IsNullOrWhiteSpace(InstallRoot))
        {
            problems.Add("installRoot is required.");
        }

        if (string.IsNullOrWhiteSpace(DataRoot))
        {
            problems.Add("dataRoot is required.");
        }

        if (string.IsNullOrWhiteSpace(AppPoolName))
        {
            problems.Add("appPoolName is required.");
        }

        return problems;
    }

    /// <summary>
    /// Fields that must never appear in this file. Even though the repository is public, an inlined
    /// credential of any kind would still be a leak in every backup of ProgramData.
    /// </summary>
    public IReadOnlyList<string> FindDisallowedSecretFields()
    {
        var json = ToJson();
        return new[] { "privateKey", "BEGIN OPENSSH", "BEGIN RSA", "passphrase", "password", "token" }
            .Where(forbidden => json.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
