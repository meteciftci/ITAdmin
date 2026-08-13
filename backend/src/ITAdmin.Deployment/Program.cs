using ITAdmin.Deployment;

// Build-machine CLI for producing, publishing, and inspecting ITAdmin distributions.
// Never runs on a target server.

return args.FirstOrDefault() switch
{
    "pack" => Pack(args),
    "verify" => Verify(args),
    "acquire-prerequisite" => await AcquirePrerequisite(args),
    "dist-stage" => DistStage(args),
    "dist-verify" => DistVerify(args),
    "resolve-release" => ResolveRelease(args),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("""
        itadmin-release - ITAdmin distribution tool (build machine only)

          acquire-prerequisite --requirement <file> --output <dir>
                     Downloads the prerequisite pinned by repository-controlled metadata from its
                     authoritative vendor URL and verifies it against the vendor's own published
                     digest, in the vendor's own algorithm (Microsoft publishes SHA-512). Fails
                     closed on a placeholder, an unsupported algorithm, or a mismatch. Prints the
                     local path, the verified upstream digest, and ITAdmin's distribution SHA-256.

          dist-stage --publish <dir> --output <dir> --version <x.y.z> --commit <sha>
                     [--tag <v-x.y.z>] [--host-agent <dir>]
                     [--prerequisite <name>|<version>|<file>|<url>|<upstreamAlgorithm>|<upstreamHash>|<upstreamHashSource>]
                       (repeatable; upstream digest required)
                     [--latest-migration <id>] [--migration-count <n>] [--timestamp <iso8601>]
                     Stages the complete distribution TREE for committing to the distribution ref.

          dist-verify --release-dir <dir> --version <x.y.z> --commit <sha>
                     Verifies an acquired distribution against the release identity requested.
                     This is the gate a target server runs before staging anything.

          pack       --publish <dir> --output <dir> --version <x.y.z> --commit <sha> [...]
                     Produces itadmin-<version>.zip for the retained offline install mode.

          verify     --artifact-dir <dir>
                     Validates an unpacked distribution directory against its own manifest.

          resolve-release [--channel stable|preview] [--exact <x.y.z>]
                     Reads `git ls-remote --tags` output on stdin and prints the annotated release
                     tag that should be installed, or the reasons nothing qualified.
        """);
    return 2;
}

static async Task<int> AcquirePrerequisite(string[] args)
{
    try
    {
        var options = ParseOptions(args);
        var requirementPath = Required(options, "requirement");
        var outputDirectory = Required(options, "output");

        var metadata = PrerequisiteRequirementMetadata.FromJson(await File.ReadAllTextAsync(requirementPath));
        if (metadata is null)
        {
            Console.Error.WriteLine($"acquire-prerequisite failed: {requirementPath} is not valid JSON.");
            return 1;
        }

        var problems = metadata.ValidateForPublishing();
        if (problems.Count > 0)
        {
            foreach (var problem in problems)
            {
                Console.Error.WriteLine($"acquire-prerequisite failed: {problem}");
            }

            return 1;
        }

        Directory.CreateDirectory(outputDirectory);
        var destination = Path.Combine(outputDirectory, metadata.Pinned.FileName);

        Console.Error.WriteLine(
            $"Downloading {metadata.ComponentName} {metadata.Pinned.Version} from {metadata.Pinned.SourceUrl}");

        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
        await using (var response = await client.GetStreamAsync(metadata.Pinned.SourceUrl))
        await using (var file = File.Create(destination))
        {
            await response.CopyToAsync(file);
        }

        // UPSTREAM verification, in the vendor's own algorithm. This answers exactly one question -
        // did we download what Microsoft published - and it is the only gate that lets these bytes
        // become part of a distribution at all.
        var upstream = metadata.Pinned.VerifyDownload(destination);
        if (!upstream.IsVerified)
        {
            File.Delete(destination);
            Console.Error.WriteLine($"acquire-prerequisite failed: {upstream.Message}");
            Console.Error.WriteLine("The download was discarded; no distribution was produced.");
            return 1;
        }

        Console.Error.WriteLine(
            $"Verified against the pinned {upstream.Algorithm} digest published by the vendor.");

        // ITAdmin's OWN distribution integrity, computed from the bytes just verified upstream. A
        // different question, in a different algorithm: did those bytes reach a server intact.
        var distributionDigest = PrerequisiteChunking.ComputeFileDigest(destination);

        Console.WriteLine($"name={metadata.ComponentName}");
        Console.WriteLine($"version={metadata.Pinned.Version}");
        Console.WriteLine($"path={destination}");
        Console.WriteLine($"sourceUrl={metadata.Pinned.SourceUrl}");
        Console.WriteLine($"upstreamHashAlgorithm={metadata.Pinned.HashAlgorithm}");
        Console.WriteLine($"upstreamHash={upstream.ActualDigest}");
        Console.WriteLine($"upstreamHashSource={metadata.Pinned.HashSource}");
        Console.WriteLine($"distributionSha256={distributionDigest}");
        Console.WriteLine($"sizeBytes={new FileInfo(destination).Length}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"acquire-prerequisite failed: {exception.Message}");
        return 1;
    }
}

static int DistStage(string[] args)
{
    try
    {
        var options = ParseOptions(args);
        var version = ReleaseVersion.Parse(Required(options, "version"));
        var outputDirectory = Required(options, "output");

        var manifest = ReleasePacker.StageReleaseTree(
            BuildPackRequest(options, version, outputDirectory, ParsePrerequisites(args)),
            outputDirectory);

        Console.WriteLine($"stagedTree={outputDirectory}");
        Console.WriteLine($"sourceVersion={manifest.Source.Version}");
        Console.WriteLine($"sourceTag={manifest.Source.Tag}");
        Console.WriteLine($"sourceCommit={manifest.Source.Commit}");
        Console.WriteLine($"distributionVersion={manifest.Distribution.Version}");
        Console.WriteLine($"distributionRef={manifest.Distribution.Ref}");

        foreach (var (path, component) in manifest.Components)
        {
            Console.WriteLine($"component={path} kind={component.Kind} files={component.Integrity.FileCount} bytes={component.Integrity.TotalBytes}");
        }

        foreach (var prerequisite in manifest.Prerequisites)
        {
            Console.WriteLine(
                $"prerequisite={prerequisite.Name} version={prerequisite.Version} "
                + $"chunks={prerequisite.ChunkCount} bytes={prerequisite.SizeBytes} "
                + $"distributionSha256={prerequisite.Sha256} "
                + $"upstream{prerequisite.UpstreamHashAlgorithm}={prerequisite.UpstreamHash}");
        }

        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"dist-stage failed: {exception.Message}");
        return 1;
    }
}

static int DistVerify(string[] args)
{
    try
    {
        var options = ParseOptions(args);
        var result = ReleaseAcquisition.Verify(
            Required(options, "release-dir"),
            ReleaseVersion.Parse(Required(options, "version")),
            Required(options, "commit"));

        if (!result.IsAcceptable)
        {
            Console.Error.WriteLine($"dist-verify failed [{result.Fault}]:");
            Console.Error.WriteLine(result.Describe());
            return 1;
        }

        var manifest = result.Manifest!;
        Console.WriteLine(
            $"ok version={manifest.Source.Version} components={manifest.Components.Count} "
            + $"prerequisites={manifest.Prerequisites.Count}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"dist-verify failed: {exception.Message}");
        return 1;
    }
}

static int ResolveRelease(string[] args)
{
    try
    {
        var options = ParseOptions(args);
        var channel = string.Equals(options.GetValueOrDefault("channel"), "preview", StringComparison.OrdinalIgnoreCase)
            ? ReleaseChannel.Preview
            : ReleaseChannel.Stable;

        var lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var resolution = options.TryGetValue("exact", out var exact) && !string.IsNullOrWhiteSpace(exact)
            ? ReleaseTagResolver.ResolveExact(lines, ReleaseVersion.Parse(exact), channel)
            : ReleaseTagResolver.Resolve(lines, channel);

        foreach (var rejection in resolution.Rejected)
        {
            Console.Error.WriteLine($"rejected: {rejection.Describe()}");
        }

        if (!resolution.IsResolved)
        {
            Console.Error.WriteLine(resolution.DescribeFailure(channel));
            return 1;
        }

        Console.WriteLine($"tag={resolution.Selected.TagName}");
        Console.WriteLine($"version={resolution.Selected.Version}");
        Console.WriteLine($"commit={resolution.Selected.SourceCommit}");
        Console.WriteLine($"distributionRef={resolution.Selected.DistributionRef}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"resolve-release failed: {exception.Message}");
        return 1;
    }
}

/// <summary>
/// Prerequisites arrive as repeatable pipe-delimited values so a shell script can pass several
/// without inventing a nested option syntax: --prerequisite "Name|version|path|url".
/// </summary>
static List<ReleasePacker.PrerequisiteSource> ParsePrerequisites(string[] args)
{
    var sources = new List<ReleasePacker.PrerequisiteSource>();

    for (var index = 1; index < args.Length - 1; index++)
    {
        if (!string.Equals(args[index], "--prerequisite", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var parts = args[index + 1].Split('|');
        if (parts.Length != 7)
        {
            throw new ArgumentException(
                "--prerequisite expects "
                + "\"<name>|<version>|<file>|<url>|<upstreamAlgorithm>|<upstreamHash>|<upstreamHashSource>\". "
                + "The upstream digest is required: staging refuses bytes with no evidence they were "
                + "verified against what the vendor published.");
        }

        if (!Enum.TryParse<UpstreamHashAlgorithm>(parts[4].Trim(), ignoreCase: true, out var algorithm))
        {
            throw new ArgumentException(
                $"--prerequisite declares an unsupported upstream hash algorithm '{parts[4].Trim()}'.");
        }

        sources.Add(new ReleasePacker.PrerequisiteSource(
            Name: parts[0].Trim(),
            Version: parts[1].Trim(),
            FilePath: parts[2].Trim(),
            SourceUrl: parts[3].Trim(),
            UpstreamHashAlgorithm: algorithm,
            UpstreamHash: parts[5].Trim(),
            UpstreamHashSource: parts[6].Trim()));
    }

    return sources;
}

static ReleasePacker.PackRequest BuildPackRequest(
    Dictionary<string, string> options,
    ReleaseVersion version,
    string outputDirectory,
    IReadOnlyList<ReleasePacker.PrerequisiteSource>? prerequisites = null)
{
    var timestamp = options.TryGetValue("timestamp", out var raw) && !string.IsNullOrWhiteSpace(raw)
        ? DateTimeOffset.Parse(raw).ToUniversalTime()
        : DateTimeOffset.UtcNow;

    return new ReleasePacker.PackRequest(
        PublishDirectory: Required(options, "publish"),
        OutputDirectory: outputDirectory,
        Version: version,
        SourceCommit: Required(options, "commit"),
        BuildTimestampUtc: timestamp,
        LatestMigration: options.GetValueOrDefault("latest-migration"),
        MigrationCount: int.TryParse(options.GetValueOrDefault("migration-count"), out var count) ? count : 0,
        HostAgentPublishDirectory: options.GetValueOrDefault("host-agent"),
        Prerequisites: prerequisites,
        SourceTag: options.GetValueOrDefault("tag"));
}

static int Pack(string[] args)
{
    try
    {
        var options = ParseOptions(args);

        var version = ReleaseVersion.Parse(Required(options, "version"));
        var result = ReleasePacker.Pack(
            BuildPackRequest(options, version, Required(options, "output"), ParsePrerequisites(args)));

        Console.WriteLine($"artifact={result.ArtifactPath}");
        Console.WriteLine($"version={result.Manifest.Source.Version}");
        Console.WriteLine($"commit={result.Manifest.Source.Commit}");
        Console.WriteLine($"components={result.Manifest.Components.Count}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"pack failed: {exception.Message}");
        return 1;
    }
}

static int Verify(string[] args)
{
    try
    {
        var options = ParseOptions(args);
        var artifactDirectory = Required(options, "artifact-dir");

        var manifestPath = Path.Combine(artifactDirectory, ReleaseManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"verify failed: {ReleaseManifest.FileName} not found in {artifactDirectory}");
            return 1;
        }

        var manifest = ReleaseManifest.FromJson(File.ReadAllText(manifestPath));
        if (manifest is null)
        {
            Console.Error.WriteLine("verify failed: manifest is not valid JSON.");
            return 1;
        }

        // Verifying an artifact against its own manifest: the identity it declares is the identity
        // we check it against. This is a self-consistency check, NOT the release-identity gate that
        // dist-verify performs against an independently resolved tag.
        var result = ReleaseAcquisition.Verify(
            artifactDirectory,
            ReleaseVersion.Parse(manifest.Source.Version),
            manifest.Source.Commit);

        if (!result.IsAcceptable)
        {
            Console.Error.WriteLine(result.Describe());
            return 1;
        }

        Console.WriteLine($"ok version={manifest.Source.Version} components={manifest.Components.Count}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"verify failed: {exception.Message}");
        return 1;
    }
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 1; index < args.Length; index++)
    {
        if (!args[index].StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var name = args[index][2..];
        var value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[++index]
            : string.Empty;
        options[name] = value;
    }

    return options;
}

static string Required(Dictionary<string, string> options, string name) =>
    options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing required option --{name}.");
