namespace ITAdmin.Deployment;

/// <summary>
/// Where an installed ITAdmin lives on a Windows host.
///
/// <para>
/// Two roots, with a hard rule between them:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>%ProgramFiles%\ITAdmin</c> — installer-owned and effectively immutable. Each release
///     gets its own directory and is never written to again after it is staged and verified.
///   </description></item>
///   <item><description>
///     <c>%ProgramData%\ITAdmin</c> — machine state: environment config, secrets, installation
///     state, DataProtection key ring, logs, backups. Survives every release change.
///   </description></item>
/// </list>
///
/// <para>
/// Keeping them apart is what makes a release replaceable. The old model extracted a zip over the
/// live IIS directory, so machine configuration and application files shared a directory and a
/// deploy could destroy both. Here, replacing a release cannot touch configuration, and resetting
/// configuration cannot corrupt a release.
/// </para>
/// </summary>
public sealed class DeploymentLayout
{
    public const string DefaultProgramFilesRoot = @"C:\Program Files\ITAdmin";
    public const string DefaultProgramDataRoot = @"C:\ProgramData\ITAdmin";

    /// <summary>Payload subdirectory inside a release: the ASP.NET publish output including wwwroot.</summary>
    public const string PayloadDirectoryName = "app";

    /// <summary>
    /// Host Agent subdirectory inside a release, a SIBLING of the payload. It is never inside
    /// <see cref="PayloadDirectoryName"/>, because that directory becomes the IIS physicalPath -
    /// privileged binaries under the web root would defeat the boundary they exist to enforce.
    /// </summary>
    public const string HostAgentDirectoryName = "hostagent";

    public const string DeploymentToolingDirectoryName = "deployment-tooling";

    public const string UpdateCoordinatorDirectoryName = "update-coordinator";

    /// <summary>
    /// Root of the runtime-prerequisite components inside a distribution. Each prerequisite gets its
    /// own subdirectory of chunk files, so one oversized redistributable cannot collide with another
    /// and a prerequisite can be retired by dropping a single directory.
    /// </summary>
    public const string PrerequisitesDirectoryName = "prerequisites";

    /// <summary>
    /// Component path for a named prerequisite, e.g. <c>prerequisites/aspnetcore-hosting-bundle</c>.
    /// The name is slugged rather than used verbatim: it becomes a directory name on Windows and a
    /// path inside a manifest, and an operator-facing product name is not safe as either.
    /// </summary>
    public static string PrerequisiteComponentPath(string prerequisiteName) =>
        PrerequisitesDirectoryName + "/" + Slug(prerequisiteName);

    /// <summary>Lowercase, hyphen-separated, ASCII alphanumerics only.</summary>
    public static string Slug(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0
            ? throw new ArgumentException($"'{value}' does not slug to a usable directory name.", nameof(value))
            : slug;
    }

    public DeploymentLayout(string programFilesRoot, string programDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programFilesRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(programDataRoot);

        ProgramFilesRoot = programFilesRoot;
        ProgramDataRoot = programDataRoot;
    }

    public static DeploymentLayout Default() => new(DefaultProgramFilesRoot, DefaultProgramDataRoot);

    public string ProgramFilesRoot { get; }
    public string ProgramDataRoot { get; }

    /// <summary>All versioned releases: <c>&lt;ProgramFiles&gt;\ITAdmin\releases</c>.</summary>
    public string ReleasesRoot => Combine(ProgramFilesRoot, "releases");

    /// <summary>Non-secret machine configuration.</summary>
    public string ConfigRoot => Combine(ProgramDataRoot, "config");

    /// <summary>Secret material, ACL'd to SYSTEM/Administrators and the app pool identity only.</summary>
    public string SecretsRoot => Combine(ProgramDataRoot, "secrets");

    /// <summary>Installation state (non-secret).</summary>
    public string StateRoot => Combine(ProgramDataRoot, "state");

    /// <summary>DataProtection key ring. Losing this makes existing encrypted DB values unreadable.</summary>
    public string DataProtectionKeysRoot => Combine(ProgramDataRoot, "DataProtection-Keys");

    public string LogsRoot => Combine(ProgramDataRoot, "logs");

    public string BackupsRoot => Combine(ProgramDataRoot, "backups");

    public string InstallationStatePath => Combine(StateRoot, InstallationState.FileName);

    public string EnvironmentConfigPath => Combine(ConfigRoot, EnvironmentConfig.FileName);

    /// <summary>Directory for one release, e.g. <c>...\releases\2.0.0</c>.</summary>
    public string ReleaseDirectory(ReleaseVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return Combine(ReleasesRoot, version.ToString());
    }

    /// <summary>
    /// The directory IIS is pointed at for a release. IIS serves the payload directory directly;
    /// the manifest sits alongside it, outside the web root, so it is never web-reachable.
    /// </summary>
    public string ReleasePayloadDirectory(ReleaseVersion version) =>
        Combine(ReleaseDirectory(version), PayloadDirectoryName);

    public string ReleaseManifestPath(ReleaseVersion version) =>
        Combine(ReleaseDirectory(version), ReleaseManifest.FileName);

    /// <summary>
    /// Where the privileged Host Agent is installed: outside every release directory, so an
    /// activation or rollback never swaps the running service out from under itself.
    /// </summary>
    public string HostAgentRoot => Combine(ProgramFilesRoot, HostAgentDirectoryName);

    /// <summary>Deployment tooling taken from the release tag, outside the web-reachable tree.</summary>
    public string ToolingRoot => Combine(ProgramFilesRoot, "tooling");

    /// <summary>
    /// Every directory the installer must create on a fresh machine, in creation order.
    /// </summary>
    public IReadOnlyList<string> RequiredDirectories() =>
    [
        ProgramFilesRoot,
        ReleasesRoot,
        ProgramDataRoot,
        ConfigRoot,
        SecretsRoot,
        StateRoot,
        DataProtectionKeysRoot,
        LogsRoot,
        BackupsRoot,
    ];

    /// <summary>
    /// Directories that must never be deleted or overwritten when a release is replaced. Used to
    /// assert that release operations cannot reach machine state.
    /// </summary>
    public IReadOnlyList<string> PreservedAcrossReleases() =>
    [
        ConfigRoot,
        SecretsRoot,
        StateRoot,
        DataProtectionKeysRoot,
        LogsRoot,
        BackupsRoot,
    ];

    /// <summary>
    /// Guard for release-removal operations: true only for paths genuinely inside the releases
    /// root. Prevents a bad version string from turning cleanup into deletion of an unrelated tree.
    /// </summary>
    public bool IsWithinReleasesRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var releases = NormaliseForComparison(ReleasesRoot);
        var candidate = NormaliseForComparison(path);

        return candidate.Length > releases.Length + 1
            && candidate.StartsWith(releases, StringComparison.OrdinalIgnoreCase)
            && candidate[releases.Length] == '\\';
    }

    // Windows-style joining, kept explicit so layout strings are identical whether the build runs
    // on Windows or on a developer's macOS/Linux machine.
    private static string Combine(string left, string right) =>
        string.Concat(left.TrimEnd('\\'), "\\", right);

    private static string NormaliseForComparison(string path) =>
        path.Replace('/', '\\').TrimEnd('\\');
}
