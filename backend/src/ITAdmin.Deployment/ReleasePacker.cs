using System.IO.Compression;

namespace ITAdmin.Deployment;

/// <summary>
/// Builds a complete ITAdmin distribution from published output.
///
/// <para>
/// Runs on the build machine only. The result is a tree containing exactly
/// <c>release.manifest.json</c> plus the declared components - the application payload, the
/// privileged Host Agent, and any Windows runtime prerequisites - so a target server needs nothing
/// but repository access: no SDK, no Node, no EF tooling, and no human carrying a redistributable
/// across the network.
/// </para>
///
/// <para>
/// The packer is the only place that decides what a distribution contains, and it computes every
/// digest from the <em>staged</em> copy rather than from the source tree, so the manifest describes
/// the bytes that actually ship.
/// </para>
/// </summary>
public static class ReleasePacker
{
    public sealed record PackRequest(
        string PublishDirectory,
        string OutputDirectory,
        ReleaseVersion Version,
        string SourceCommit,
        DateTimeOffset BuildTimestampUtc,
        string? LatestMigration,
        int MigrationCount,
        string? HostAgentPublishDirectory = null,
        string? DeploymentToolingDirectory = null,
        string? UpdateCoordinatorPublishDirectory = null,
        IReadOnlyList<PrerequisiteSource>? Prerequisites = null,
        string? SourceTag = null,
        string? ReleaseDescription = null);

    /// <summary>
    /// A prerequisite file the publisher has already obtained AND verified against the vendor's
    /// published digest. The packer chunks it; it never fetches, and it never re-decides whether the
    /// bytes were acceptable.
    ///
    /// <para>
    /// The upstream digest and algorithm travel with it so they can be recorded in the distribution
    /// for later audit. Staging refuses a source that carries no upstream verification evidence -
    /// that would mean the internal digests were computed over bytes nobody vouched for.
    /// </para>
    /// </summary>
    public sealed record PrerequisiteSource(
        string Name,
        string Version,
        string FilePath,
        string SourceUrl,
        string UpstreamHash,
        UpstreamHashAlgorithm UpstreamHashAlgorithm,
        string UpstreamHashSource);

    public sealed record PackResult(string ArtifactPath, string ManifestPath, ReleaseManifest Manifest);

    /// <summary>
    /// Stages the distribution and zips it, for the retained offline install mode.
    /// </summary>
    public static PackResult Pack(PackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stagingRoot = Path.Combine(request.OutputDirectory, $"stage-{request.Version}");
        var manifest = StageReleaseTree(request, stagingRoot);
        var manifestPath = Path.Combine(stagingRoot, ReleaseManifest.FileName);

        Directory.CreateDirectory(request.OutputDirectory);
        var artifactPath = Path.Combine(request.OutputDirectory, $"itadmin-{request.Version}.zip");
        if (File.Exists(artifactPath))
        {
            File.Delete(artifactPath);
        }

        ZipFile.CreateFromDirectory(stagingRoot, artifactPath, CompressionLevel.Optimal, includeBaseDirectory: false);

        return new PackResult(artifactPath, manifestPath, manifest);
    }

    /// <summary>
    /// Stages the distribution tree without producing a zip. This is what gets committed to a
    /// distribution ref.
    ///
    /// <para>
    /// Publishing the tree file-by-file rather than as one archive matters for a Git-delivered
    /// payload: a single-file archive would be one enormous blob transferred and stored whole on
    /// every release, straight into the hosting provider's per-file size limit, whereas a tree of
    /// ordinary files lets Git deduplicate the many files that do not change between releases and
    /// keeps every individual object small.
    /// </para>
    /// </summary>
    public static ReleaseManifest StageReleaseTree(PackRequest request, string stagingRoot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);

        if (!Directory.Exists(request.PublishDirectory))
        {
            throw new DirectoryNotFoundException($"Publish directory not found: {request.PublishDirectory}");
        }

        if (string.IsNullOrWhiteSpace(request.HostAgentPublishDirectory)
            || string.IsNullOrWhiteSpace(request.DeploymentToolingDirectory)
            || string.IsNullOrWhiteSpace(request.UpdateCoordinatorPublishDirectory))
        {
            throw new InvalidOperationException(
                "A release must include Host Agent, deployment tooling, and Update Coordinator components.");
        }

        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }

        var components = new SortedDictionary<string, DistributionComponent>(StringComparer.Ordinal);

        // --- Application payload ------------------------------------------------------------
        var payloadRoot = Path.Combine(stagingRoot, DeploymentLayout.PayloadDirectoryName);
        CopyDirectory(request.PublishDirectory, payloadRoot);
        AssertPayloadIsComplete(payloadRoot);
        AssertPayloadCarriesNoDevelopmentConfiguration(payloadRoot);

        components[DeploymentLayout.PayloadDirectoryName] = new DistributionComponent
        {
            Kind = DistributionComponentKind.ApplicationPayload,
            Integrity = ReleaseIntegrity.Create(payloadRoot),
        };

        // --- Host Agent ----------------------------------------------------------------------
        // Staged beside the payload, never inside it: app/ becomes the IIS physicalPath, and the
        // whole point of the agent is that the web application cannot reach it.
        if (!Directory.Exists(request.HostAgentPublishDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Host Agent publish directory not found: {request.HostAgentPublishDirectory}");
        }

        var hostAgentRoot = Path.Combine(stagingRoot, DeploymentLayout.HostAgentDirectoryName);
        CopyDirectory(request.HostAgentPublishDirectory, hostAgentRoot);
        AssertHostAgentIsComplete(hostAgentRoot);

        components[DeploymentLayout.HostAgentDirectoryName] = new DistributionComponent
        {
            Kind = DistributionComponentKind.HostAgent,
            Integrity = ReleaseIntegrity.Create(hostAgentRoot),
        };

        AddDirectoryComponent(
            request.DeploymentToolingDirectory,
            stagingRoot,
            DeploymentLayout.DeploymentToolingDirectoryName,
            DistributionComponentKind.DeploymentTooling,
            requiredFile: "Install-ITAdmin.ps1",
            components);

        AddDirectoryComponent(
            request.UpdateCoordinatorPublishDirectory,
            stagingRoot,
            DeploymentLayout.UpdateCoordinatorDirectoryName,
            DistributionComponentKind.UpdateCoordinator,
            requiredFile: "ITAdmin.UpdateCoordinator.exe",
            components);

        // --- Runtime prerequisites ------------------------------------------------------------
        var prerequisites = new List<PrerequisitePayload>();
        foreach (var source in request.Prerequisites ?? [])
        {
            if (!File.Exists(source.FilePath))
            {
                throw new FileNotFoundException(
                    $"Prerequisite '{source.Name}' was not found at {source.FilePath}. The publisher must "
                    + "obtain and verify it before packing.",
                    source.FilePath);
            }

            // Refuse to stage bytes with no upstream verification evidence. Computing our own
            // integrity digests over an unverified download would produce a distribution that looks
            // fully verified while resting on nothing.
            if (string.IsNullOrWhiteSpace(source.UpstreamHash))
            {
                throw new InvalidOperationException(
                    $"Prerequisite '{source.Name}' carries no upstream verification digest. The publisher "
                    + "must verify a third-party download against the vendor's published hash before it "
                    + "is staged into a distribution.");
            }

            var componentPath = DeploymentLayout.PrerequisiteComponentPath(source.Name);
            var componentRoot = Path.Combine(stagingRoot, componentPath);

            // Internal integrity is computed here, from the file the publisher already verified
            // upstream - never from anything fetched during staging.
            var chunking = PrerequisiteChunking.Split(source.FilePath, componentRoot);

            prerequisites.Add(new PrerequisitePayload
            {
                Name = source.Name,
                Version = source.Version,
                FileName = chunking.FileName,
                ComponentPath = componentPath,
                Sha256 = chunking.Sha256,
                SizeBytes = chunking.SizeBytes,
                ChunkDigests = chunking.ChunkDigests,
                SourceUrl = source.SourceUrl,
                UpstreamHash = source.UpstreamHash.Trim().ToLowerInvariant(),
                UpstreamHashAlgorithm = source.UpstreamHashAlgorithm,
                UpstreamHashSource = source.UpstreamHashSource,
            });

            components[componentPath] = new DistributionComponent
            {
                Kind = DistributionComponentKind.RuntimePrerequisite,
                Integrity = ReleaseIntegrity.Create(componentRoot),
            };
        }

        var manifest = new ReleaseManifest
        {
            Source = new SourceReleaseIdentity
            {
                Version = request.Version.ToString(),
                Tag = string.IsNullOrWhiteSpace(request.SourceTag) ? "v" + request.Version : request.SourceTag,
                Commit = request.SourceCommit,
            },
            Distribution = new DistributionIdentity
            {
                Version = request.Version.ToString(),
                SourceCommit = request.SourceCommit,
                BuiltAtUtc = request.BuildTimestampUtc,
                Ref = GitReleaseRefs.DistributionRef(request.Version),
                Summary = request.ReleaseDescription?.Trim() ?? string.Empty,
            },
            Migrations = new ReleaseMigrationInfo
            {
                Latest = request.LatestMigration,
                Count = request.MigrationCount,
            },
            Components = components,
            Prerequisites = prerequisites,
        };

        var validation = manifest.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "Generated distribution manifest is invalid: " + string.Join("; ", validation.Errors));
        }

        File.WriteAllText(Path.Combine(stagingRoot, ReleaseManifest.FileName), manifest.ToJson());

        return manifest;
    }

    /// <summary>
    /// The payload must be able to serve on its own: the ASP.NET host, the IIS module config, and
    /// the built frontend. Catching this at build time avoids shipping a distribution that only
    /// fails once IIS is already pointed at it.
    /// </summary>
    private static void AssertPayloadIsComplete(string payloadRoot)
    {
        var missing = new List<string>();

        if (!File.Exists(Path.Combine(payloadRoot, "ITAdmin.Api.dll")))
        {
            missing.Add("ITAdmin.Api.dll");
        }

        if (!File.Exists(Path.Combine(payloadRoot, "web.config")))
        {
            missing.Add("web.config (required by the ASP.NET Core IIS module)");
        }

        if (!File.Exists(Path.Combine(payloadRoot, "wwwroot", "index.html")))
        {
            missing.Add("wwwroot/index.html (frontend build output)");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Release payload is incomplete: " + string.Join(", ", missing));
        }
    }

    /// <summary>
    /// The Host Agent must be able to start on its own. Catching a missing executable here beats
    /// discovering it when a server has already installed a release whose privileged half is absent.
    /// </summary>
    private static void AssertHostAgentIsComplete(string hostAgentRoot)
    {
        if (!File.Exists(Path.Combine(hostAgentRoot, "ITAdmin.HostAgent.exe"))
            && !File.Exists(Path.Combine(hostAgentRoot, "ITAdmin.HostAgent.dll")))
        {
            throw new InvalidOperationException(
                "Host Agent component is incomplete: ITAdmin.HostAgent executable not found.");
        }
    }

    private static void AddDirectoryComponent(
        string? source,
        string stagingRoot,
        string componentPath,
        DistributionComponentKind kind,
        string requiredFile,
        IDictionary<string, DistributionComponent> components)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Component directory not found: {source}");
        }

        var destination = Path.Combine(stagingRoot, componentPath);
        CopyDirectory(source, destination);
        if (!File.Exists(Path.Combine(destination, requiredFile)))
        {
            throw new InvalidOperationException(
                $"Component '{componentPath}' is incomplete: {requiredFile} was not found.");
        }

        components[componentPath] = new DistributionComponent
        {
            Kind = kind,
            Integrity = ReleaseIntegrity.Create(destination),
        };
    }

    /// <summary>
    /// A distribution must be environment-neutral. Development appsettings carry a local connection
    /// string and would also override production configuration if they shipped, so their presence
    /// fails the build rather than reaching a customer.
    /// </summary>
    private static void AssertPayloadCarriesNoDevelopmentConfiguration(string payloadRoot)
    {
        var offenders = Directory
            .EnumerateFiles(payloadRoot, "appsettings.*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null
                && !name.Equals("appsettings.Production.json", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("sample", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (offenders.Count > 0)
        {
            throw new InvalidOperationException(
                "Release payload contains environment-specific configuration files that must not ship: "
                + string.Join(", ", offenders));
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
        }
    }
}
