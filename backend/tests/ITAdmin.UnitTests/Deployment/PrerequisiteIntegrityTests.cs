using System.Security.Cryptography;
using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The two-hash prerequisite model.
///
/// <para>
/// A third-party installer that will be executed as SYSTEM crosses two trust boundaries, and each
/// gets its own check in its own algorithm. Microsoft's published SHA-512 answers "did we download
/// what Microsoft published"; ITAdmin's SHA-256 answers "did those verified bytes reach this server
/// intact". These tests pin that they stay separate and that both are fail-closed.
/// </para>
/// </summary>
public sealed class PrerequisiteIntegrityTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "itadmin-prereq-" + Guid.NewGuid().ToString("N"));

    public PrerequisiteIntegrityTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] Bytes(int length)
    {
        var bytes = new byte[length];
        for (var index = 0; index < length; index++)
        {
            bytes[index] = (byte)((index * 17) % 253);
        }

        return bytes;
    }

    private static string Sha512Of(byte[] content) => Convert.ToHexStringLower(SHA512.HashData(content));

    /// <summary>A well-formed SHA-512 of some other content, for exercising the mismatch path.</summary>
    private const string WrongButWellFormedSha512 =
        "2cb6c479a8d8d2eb0d7bba24b1594546423e44e6f5d0ed593b9988ba60e3d23f"
        + "e670bc4b7c472aebf3a43bafd9980817a75bda06b96deb9f2befd3aa7e6f86fd";

    private static string Sha256Of(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private static PinnedPrerequisite Pin(string expectedHash, UpstreamHashAlgorithm algorithm = UpstreamHashAlgorithm.Sha512) => new()
    {
        Version = "10.0.10",
        FileName = "dotnet-hosting-10.0.10-win.exe",
        SourceUrl = "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.10/dotnet-hosting-10.0.10-win.exe",
        HashAlgorithm = algorithm,
        ExpectedHash = expectedHash,
        HashSource = "Microsoft .NET 10.0 release metadata",
    };

    // ==========================================================================================
    // Upstream verification — Microsoft's algorithm, Microsoft's value
    // ==========================================================================================

    [Fact]
    public void CorrectUpstreamSha512_Verifies()
    {
        var content = Bytes(4096);
        var path = WriteFile("installer.exe", content);

        var result = Pin(Sha512Of(content)).VerifyDownload(path);

        Assert.True(result.IsVerified, result.Message);
        Assert.Equal(UpstreamHashAlgorithm.Sha512, result.Algorithm);
        Assert.Equal(Sha512Of(content), result.ActualDigest);
    }

    [Fact]
    public void WrongUpstreamSha512_RefusesPublication()
    {
        var path = WriteFile("installer.exe", Bytes(4096));

        // A realistic-looking digest of some other file - not a repeated character, so this
        // exercises the mismatch path rather than the placeholder path.
        var result = Pin(WrongButWellFormedSha512).VerifyDownload(path);

        Assert.False(result.IsVerified);
        Assert.Contains("Sha512 mismatch", result.Message, StringComparison.Ordinal);
        Assert.Contains("tampered with", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sha256ValueUnderASha512Pin_IsRefusedAsMalformed()
    {
        // The exact mistake this model exists to prevent: somebody hand-deriving a SHA-256 and
        // pasting it where Microsoft's SHA-512 belongs. A length check catches it before it can
        // masquerade as a verified pin.
        var content = Bytes(4096);
        var path = WriteFile("installer.exe", content);

        var result = Pin(Sha256Of(content)).VerifyDownload(path);

        Assert.False(result.IsVerified);
        Assert.Contains("not a valid Sha512 hex digest", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedUpstreamAlgorithm_IsFailClosed()
    {
        var path = WriteFile("installer.exe", Bytes(1024));
        var pin = Pin(WrongButWellFormedSha512) with { HashAlgorithm = (UpstreamHashAlgorithm)99 };

        var result = pin.VerifyDownload(path);

        Assert.False(result.IsVerified);
        // Never silently substituted with a default algorithm.
        Assert.Throws<NotSupportedException>(() => pin.ComputeUpstreamDigest(path));
    }

    [Fact]
    public void Sha256PinIsStillSupportedForVendorsThatPublishIt()
    {
        // The algorithm is the vendor's choice, not ours.
        var content = Bytes(2048);
        var path = WriteFile("other-vendor.exe", content);

        var result = Pin(Sha256Of(content), UpstreamHashAlgorithm.Sha256).VerifyDownload(path);

        Assert.True(result.IsVerified, result.Message);
        Assert.Equal(UpstreamHashAlgorithm.Sha256, result.Algorithm);
    }

    // ==========================================================================================
    // Placeholders
    // ==========================================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("REPLACE_WITH_MICROSOFT_PUBLISHED_SHA256")]
    [InlineData("TODO")]
    public void PlaceholderHashes_CannotPublish(string placeholder)
    {
        var metadata = new PrerequisiteRequirementMetadata
        {
            ComponentName = "ASP.NET Core Hosting Bundle",
            Pinned = Pin(placeholder),
        };

        Assert.True(metadata.Pinned.LooksLikeAPlaceholder());
        Assert.Contains(
            metadata.ValidateForPublishing(),
            problem => problem.Contains("placeholder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RepeatedCharacterHash_IsTreatedAsAPlaceholder() =>
        // What somebody types when filling in a shape.
        Assert.True(Pin(new string('0', 128)).LooksLikeAPlaceholder());

    [Fact]
    public void PlaceholderVerification_NeverPasses()
    {
        var path = WriteFile("installer.exe", Bytes(512));

        var result = Pin("REPLACE_ME").VerifyDownload(path);

        Assert.False(result.IsVerified);
        Assert.Contains("placeholder", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================================================================
    // The checked-in Hosting Bundle authority
    // ==========================================================================================

    private static PrerequisiteRequirementMetadata RepositoryMetadata()
    {
        var path = Path.Combine(
            RepositoryRootLocator.Find(),
            "scripts", "install", "prerequisites", "hosting-bundle.requirement.json");

        var metadata = PrerequisiteRequirementMetadata.FromJson(File.ReadAllText(path));
        Assert.NotNull(metadata);
        return metadata!;
    }

    [Fact]
    public void RepositoryMetadata_IsPublishable()
    {
        // No placeholder blocker left: the checked-in file must be ready to publish as it stands.
        var metadata = RepositoryMetadata();

        Assert.Empty(metadata.Validate());
        Assert.Empty(metadata.ValidateForPublishing());
        Assert.False(metadata.Pinned.LooksLikeAPlaceholder());
    }

    [Fact]
    public void RepositoryMetadata_PinsMicrosoftsPublishedSha512()
    {
        var metadata = RepositoryMetadata();

        Assert.Equal(UpstreamHashAlgorithm.Sha512, metadata.Pinned.HashAlgorithm);
        Assert.Equal(128, metadata.Pinned.ExpectedHash.Length);
        Assert.True(metadata.Pinned.IsWellFormedDigest(metadata.Pinned.ExpectedHash));
        Assert.Equal(
            "11e66d71e01a32794051437124df4f63585d40ff80b837a9520e4a0bf9ce18b7"
            + "50765e25398b42455745183b972fa0541426fdf4f9ea253d61e129302f21460e",
            metadata.Pinned.ExpectedHash);
    }

    [Fact]
    public void RepositoryMetadata_PinsAnExactVersionRatherThanLatest()
    {
        // A release build must never resolve "latest": rebuilding the same source release has to
        // consume the same runtime bytes.
        var metadata = RepositoryMetadata();

        Assert.Equal("10.0.10", metadata.Pinned.Version);
        Assert.Contains("10.0.10", metadata.Pinned.SourceUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("latest", metadata.Pinned.SourceUrl, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://", metadata.Pinned.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryMetadata_RecordsWhereTheDigestCameFrom() =>
        // A pin nobody can trace back to its origin cannot be re-checked when the vendor changes it.
        Assert.False(string.IsNullOrWhiteSpace(RepositoryMetadata().Pinned.HashSource));

    [Fact]
    public void RepositoryMetadata_NoPlaceholderTokenSurvivesAnywhereInTheFile()
    {
        var path = Path.Combine(
            RepositoryRootLocator.Find(),
            "scripts", "install", "prerequisites", "hosting-bundle.requirement.json");
        var text = File.ReadAllText(path);

        foreach (var token in new[] { "REPLACE_WITH", "REPLACE_ME", "TODO", "XXXX", "FIXME" })
        {
            Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ==========================================================================================
    // Distribution integrity is computed only from upstream-verified bytes
    // ==========================================================================================

    private string StagePublishDirectory()
    {
        var publish = Path.Combine(_root, "publish");
        Directory.CreateDirectory(Path.Combine(publish, "wwwroot"));
        File.WriteAllText(Path.Combine(publish, "ITAdmin.Api.dll"), "assembly");
        File.WriteAllText(Path.Combine(publish, "web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(publish, "wwwroot", "index.html"), "<html></html>");
        return publish;
    }

    private ReleasePacker.PackRequest RequestWith(ReleasePacker.PrerequisiteSource? prerequisite) =>
        new(
            PublishDirectory: StagePublishDirectory(),
            OutputDirectory: Path.Combine(_root, "tree"),
            Version: ReleaseVersion.Parse("2.1.0"),
            SourceCommit: new string('a', 40),
            BuildTimestampUtc: DateTimeOffset.UnixEpoch,
            LatestMigration: "20240101000000_Initial",
            MigrationCount: 1,
            HostAgentPublishDirectory: StageCoreComponent("hostagent", "ITAdmin.HostAgent.dll"),
            DeploymentToolingDirectory: StageCoreComponent("deployment-tooling", "Install-ITAdmin.ps1"),
            UpdateCoordinatorPublishDirectory: StageCoreComponent("update-coordinator", "ITAdmin.UpdateCoordinator.exe"),
            Prerequisites: prerequisite is null ? null : [prerequisite],
            SourceTag: "v2.1.0");

    private string StageCoreComponent(string directoryName, string requiredFile)
    {
        var directory = Path.Combine(_root, directoryName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, requiredFile), directoryName);
        return directory;
    }

    [Fact]
    public void Staging_RefusesAPrerequisiteWithNoUpstreamEvidence()
    {
        // Computing our own digests over an unverified download would produce a distribution that
        // looks fully verified while resting on nothing.
        var content = Bytes(4096);
        var installer = WriteFile("dotnet-hosting-10.0.10-win.exe", content);

        var source = new ReleasePacker.PrerequisiteSource(
            "ASP.NET Core Hosting Bundle",
            "10.0.10",
            installer,
            "https://example.invalid/x.exe",
            UpstreamHash: string.Empty,
            UpstreamHashAlgorithm: UpstreamHashAlgorithm.Sha512,
            UpstreamHashSource: "n/a");

        var exception = Assert.Throws<InvalidOperationException>(
            () => ReleasePacker.StageReleaseTree(RequestWith(source), Path.Combine(_root, "tree")));

        Assert.Contains("no upstream verification digest", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Staging_ComputesDistributionSha256FromTheVerifiedBytes_AndPreservesUpstreamProvenance()
    {
        var content = Bytes(8192);
        var installer = WriteFile("dotnet-hosting-10.0.10-win.exe", content);
        var upstream = Sha512Of(content);

        var source = new ReleasePacker.PrerequisiteSource(
            "ASP.NET Core Hosting Bundle",
            "10.0.10",
            installer,
            "https://builds.dotnet.microsoft.com/x.exe",
            UpstreamHash: upstream,
            UpstreamHashAlgorithm: UpstreamHashAlgorithm.Sha512,
            UpstreamHashSource: "Microsoft .NET 10.0 release metadata");

        var manifest = ReleasePacker.StageReleaseTree(RequestWith(source), Path.Combine(_root, "tree"));

        var payload = Assert.Single(manifest.Prerequisites);

        // Internal integrity: ITAdmin's algorithm, over the same bytes.
        Assert.Equal(Sha256Of(content), payload.Sha256);
        // Upstream provenance: preserved verbatim, so an auditor can retrace the supply chain from
        // the distribution alone.
        Assert.Equal(upstream, payload.UpstreamHash);
        Assert.Equal(UpstreamHashAlgorithm.Sha512, payload.UpstreamHashAlgorithm);
        Assert.Equal("Microsoft .NET 10.0 release metadata", payload.UpstreamHashSource);

        Assert.NotEqual(payload.Sha256, payload.UpstreamHash);
        Assert.True(manifest.Validate().IsValid, string.Join("; ", manifest.Validate().Errors));
    }

    [Fact]
    public void Manifest_RejectsAPrerequisiteWithNoUpstreamProvenance()
    {
        var payload = new PrerequisitePayload
        {
            Name = "Example",
            Version = "1.0.0",
            FileName = "example.exe",
            ComponentPath = "prerequisites/example",
            Sha256 = new string('b', 64),
            SizeBytes = 10,
            ChunkDigests = [new string('c', 64)],
            SourceUrl = "https://example.invalid/example.exe",
        };

        var problems = payload.Validate();

        Assert.Contains(problems, problem => problem.Contains("upstreamHash is required", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("upstreamHashSource is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Manifest_RejectsAnUpstreamHashOfTheWrongLengthForItsAlgorithm()
    {
        var payload = new PrerequisitePayload
        {
            Name = "Example",
            Version = "1.0.0",
            FileName = "example.exe",
            ComponentPath = "prerequisites/example",
            Sha256 = new string('b', 64),
            SizeBytes = 10,
            ChunkDigests = [new string('c', 64)],
            SourceUrl = "https://example.invalid/example.exe",
            UpstreamHash = "fbcf774e29e916b115eb5c5cd71a581499c4af1ae6573bd4342b68453fa2dc23",
            UpstreamHashAlgorithm = UpstreamHashAlgorithm.Sha512,
            UpstreamHashSource = "vendor",
        };

        Assert.Contains(
            payload.Validate(),
            problem => problem.Contains("128-character Sha512", StringComparison.Ordinal));
    }

    // ==========================================================================================
    // The reassembled file is still verified before execution
    // ==========================================================================================

    [Fact]
    public void ReassembledPrerequisite_IsVerifiedAgainstTheDistributionDigestBeforeExecution()
    {
        var content = Bytes(5000);
        var installer = WriteFile("dotnet-hosting-10.0.10-win.exe", content);

        var source = new ReleasePacker.PrerequisiteSource(
            "ASP.NET Core Hosting Bundle",
            "10.0.10",
            installer,
            "https://builds.dotnet.microsoft.com/x.exe",
            Sha512Of(content),
            UpstreamHashAlgorithm.Sha512,
            "Microsoft .NET 10.0 release metadata");

        var tree = Path.Combine(_root, "tree");
        var manifest = ReleasePacker.StageReleaseTree(RequestWith(source), tree);
        var payload = Assert.Single(manifest.Prerequisites);

        var chunkRoot = Path.Combine(tree, payload.ComponentPath.Replace('/', Path.DirectorySeparatorChar));
        var destination = Path.Combine(_root, "reassembled", payload.FileName);

        var result = PrerequisiteChunking.Reassemble(payload, chunkRoot, destination);

        Assert.True(result.Succeeded, string.Join("; ", result.Problems));
        Assert.Equal(Sha256Of(content), result.Sha256);
        Assert.Equal(content, File.ReadAllBytes(destination));
    }

    [Fact]
    public void ReassembledPrerequisite_WithATamperedChunk_IsNeverExecuted()
    {
        var content = Bytes(5000);
        var installer = WriteFile("dotnet-hosting-10.0.10-win.exe", content);

        var source = new ReleasePacker.PrerequisiteSource(
            "ASP.NET Core Hosting Bundle",
            "10.0.10",
            installer,
            "https://builds.dotnet.microsoft.com/x.exe",
            Sha512Of(content),
            UpstreamHashAlgorithm.Sha512,
            "Microsoft .NET 10.0 release metadata");

        var tree = Path.Combine(_root, "tree");
        var manifest = ReleasePacker.StageReleaseTree(RequestWith(source), tree);
        var payload = Assert.Single(manifest.Prerequisites);

        var chunkRoot = Path.Combine(tree, payload.ComponentPath.Replace('/', Path.DirectorySeparatorChar));
        File.AppendAllText(Path.Combine(chunkRoot, payload.ChunkFileName(0)), "tamper");

        var destination = Path.Combine(_root, "reassembled", payload.FileName);
        var result = PrerequisiteChunking.Reassemble(payload, chunkRoot, destination);

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(destination));
    }
}
