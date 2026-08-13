using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The distribution manifest is the single trust contract a server evaluates before it changes
/// anything. These pin its shape, its self-consistency rules, and the environment-neutrality that
/// lets one distribution install at any customer.
/// </summary>
public sealed class ReleaseManifestTests
{
    private const string Commit = "1234567890abcdef1234567890abcdef12345678";

    private static ReleaseIntegrity SampleIntegrity(params string[] paths)
    {
        var files = paths.ToDictionary(path => path, _ => new string('a', 64), StringComparer.Ordinal);
        return new ReleaseIntegrity
        {
            FileCount = files.Count,
            TotalBytes = files.Count * 100,
            Files = files,
        };
    }

    private static ReleaseManifest ValidManifest() => new()
    {
        Source = new SourceReleaseIdentity { Version = "2.1.0", Tag = "v2.1.0", Commit = Commit },
        Distribution = new DistributionIdentity
        {
            Version = "2.1.0",
            SourceCommit = Commit,
            BuiltAtUtc = DateTimeOffset.UnixEpoch,
            Ref = "refs/itadmin/dist/2.1.0",
        },
        Migrations = new ReleaseMigrationInfo { Latest = "20240101000000_Initial", Count = 1 },
        Components = new Dictionary<string, DistributionComponent>(StringComparer.Ordinal)
        {
            [DeploymentLayout.PayloadDirectoryName] = new()
            {
                Kind = DistributionComponentKind.ApplicationPayload,
                Integrity = SampleIntegrity("ITAdmin.Api.dll", "web.config", "wwwroot/index.html"),
            },
            [DeploymentLayout.HostAgentDirectoryName] = new()
            {
                Kind = DistributionComponentKind.HostAgent,
                Integrity = SampleIntegrity("ITAdmin.HostAgent.dll"),
            },
        },
    };

    private static PrerequisitePayload SamplePrerequisite() => new()
    {
        Name = "ASP.NET Core Hosting Bundle",
        Version = "10.0.10",
        FileName = "dotnet-hosting-10.0.10-win.exe",
        ComponentPath = "prerequisites/asp-net-core-hosting-bundle",
        Sha256 = new string('b', 64),
        SizeBytes = 150_000_000,
        ChunkDigests = [new string('c', 64), new string('d', 64)],
        SourceUrl = "https://example.invalid/dotnet-hosting-10.0.10-win.exe",
        UpstreamHash = new string('e', 128),
        UpstreamHashAlgorithm = UpstreamHashAlgorithm.Sha512,
        UpstreamHashSource = "Microsoft .NET 10.0 release metadata",
    };

    [Fact]
    public void Validate_CompleteManifest_Passes()
    {
        var result = ValidManifest().Validate();

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_IsRejected() =>
        Assert.Contains(
            (ValidManifest() with { SchemaVersion = 99 }).Validate().Errors,
            error => error.Contains("schemaVersion", StringComparison.Ordinal));

    [Fact]
    public void Validate_ForeignProduct_IsRejected() =>
        Assert.Contains(
            (ValidManifest() with { Product = "SomethingElse" }).Validate().Errors,
            error => error.Contains("product", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Validate_SourceAndDistributionVersionsMustAgree()
    {
        // The publisher records these independently. A disagreement means it built one thing and
        // labelled it another.
        var manifest = ValidManifest();
        var drifted = manifest with
        {
            Distribution = manifest.Distribution with { Version = "2.0.0" },
        };

        Assert.Contains(
            drifted.Validate().Errors,
            error => error.Contains("does not match source release version", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_SourceAndDistributionCommitsMustAgree()
    {
        var manifest = ValidManifest();
        var drifted = manifest with
        {
            Distribution = manifest.Distribution with { SourceCommit = new string('9', 40) },
        };

        Assert.Contains(
            drifted.Validate().Errors,
            error => error.Contains("not built from the commit", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MissingApplicationComponent_IsRejected()
    {
        var manifest = ValidManifest();
        var withoutApp = manifest with
        {
            Components = manifest.Components
                .Where(entry => entry.Key != DeploymentLayout.PayloadDirectoryName)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
        };

        Assert.Contains(
            withoutApp.Validate().Errors,
            error => error.Contains("nothing for IIS to serve", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("C:/drive")]
    [InlineData("back\\slash")]
    public void Validate_ComponentPathThatCouldEscapeTheRoot_IsRejected(string path)
    {
        var manifest = ValidManifest();
        var components = manifest.Components.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        components[path] = new DistributionComponent
        {
            Kind = DistributionComponentKind.RuntimePrerequisite,
            Integrity = SampleIntegrity("file.bin"),
        };

        Assert.Contains(
            (manifest with { Components = components }).Validate().Errors,
            error => error.Contains("normalised relative directory", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_PrerequisiteReferencingAnUndeclaredComponent_IsRejected()
    {
        var manifest = ValidManifest() with { Prerequisites = [SamplePrerequisite()] };

        Assert.Contains(
            manifest.Validate().Errors,
            error => error.Contains("not a declared component", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_PrerequisiteWithItsComponent_Passes()
    {
        var manifest = ValidManifest();
        var prerequisite = SamplePrerequisite();
        var components = manifest.Components.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        components[prerequisite.ComponentPath] = new DistributionComponent
        {
            Kind = DistributionComponentKind.RuntimePrerequisite,
            Integrity = SampleIntegrity(
                prerequisite.ChunkFileName(0),
                prerequisite.ChunkFileName(1)),
        };

        var result = (manifest with { Components = components, Prerequisites = [prerequisite] }).Validate();

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_PrerequisiteWithoutChunks_IsRejected()
    {
        var prerequisite = SamplePrerequisite() with { ChunkDigests = [] };

        Assert.Contains(
            prerequisite.Validate(),
            error => error.Contains("nothing to reassemble", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../evil.exe")]
    [InlineData("dir/file.exe")]
    [InlineData("C:evil.exe")]
    public void Validate_PrerequisiteFileNameThatIsNotAPlainName_IsRejected(string fileName)
    {
        // This value becomes a path on the target and the name of a file that gets executed.
        Assert.Contains(
            (SamplePrerequisite() with { FileName = fileName }).Validate(),
            error => error.Contains("plain file name", StringComparison.Ordinal));
    }

    [Fact]
    public void ChunkFileName_IsOrderedAndZeroPadded()
    {
        var prerequisite = SamplePrerequisite();

        Assert.Equal("dotnet-hosting-10.0.10-win.exe.part0000", prerequisite.ChunkFileName(0));
        Assert.Equal("dotnet-hosting-10.0.10-win.exe.part0011", prerequisite.ChunkFileName(11));
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var manifest = ValidManifest();
        var prerequisite = SamplePrerequisite();
        var components = manifest.Components.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        components[prerequisite.ComponentPath] = new DistributionComponent
        {
            Kind = DistributionComponentKind.RuntimePrerequisite,
            Integrity = SampleIntegrity(prerequisite.ChunkFileName(0), prerequisite.ChunkFileName(1)),
        };
        var full = manifest with { Components = components, Prerequisites = [prerequisite] };

        var restored = ReleaseManifest.FromJson(full.ToJson());

        Assert.NotNull(restored);
        Assert.Equal("2.1.0", restored!.Source.Version);
        Assert.Equal("v2.1.0", restored.Source.Tag);
        Assert.Equal(Commit, restored.Distribution.SourceCommit);
        Assert.Equal(3, restored.Components.Count);
        Assert.Equal(DistributionComponentKind.HostAgent, restored.HostAgentComponent!.Kind);
        Assert.Equal(2, Assert.Single(restored.Prerequisites).ChunkCount);
        Assert.True(restored.Validate().IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    public void FromJson_MalformedInput_ReturnsNull(string json) =>
        Assert.Null(ReleaseManifest.FromJson(json));

    [Fact]
    public void Manifest_IsEnvironmentNeutral()
    {
        // The single most important property of the distribution format: the same tree must install
        // at any customer. If an environment field is ever added, this fails loudly.
        string[] forbidden =
        [
            "Fqdn", "Domain", "DomainController", "Certificate", "Thumbprint",
            "Database", "ConnectionString", "Username", "Password", "Secret", "BaseDn", "Ip",
        ];

        foreach (var type in new[]
                 {
                     typeof(ReleaseManifest), typeof(SourceReleaseIdentity), typeof(DistributionIdentity),
                     typeof(DistributionComponent), typeof(PrerequisitePayload),
                 })
        {
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain(
                    forbidden,
                    term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void ManifestJson_ContainsNoEnvironmentSpecificValues()
    {
        var json = ValidManifest().ToJson();

        foreach (var term in new[]
                 {
                     "muglabb", "mugla.bel.tr", "SRV-ITADMIN", "10.5.1.", "10.30.40.", "DC=muglabb",
                 })
        {
            Assert.DoesNotContain(term, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SourceAndDistributionIdentity_AreSeparateTypes()
    {
        // Collapsing them would remove the comparison that makes a distribution ref safe to fetch
        // from: the ref says what it carries, the tag says what was released, and verification is
        // the difference between the two.
        Assert.NotEqual(typeof(SourceReleaseIdentity), typeof(DistributionIdentity));

        var manifest = ValidManifest();
        Assert.Equal(manifest.Source.Version, manifest.Distribution.Version);
        Assert.NotSame((object)manifest.Source, manifest.Distribution);
    }
}
