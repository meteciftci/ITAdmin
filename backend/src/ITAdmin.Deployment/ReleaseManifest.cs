using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.Deployment;

/// <summary>
/// The single trust contract for one ITAdmin distribution.
///
/// <para>
/// A distribution is everything a Windows host needs to reach a serving state: the application
/// payload, the privileged Host Agent, and the Windows runtime prerequisites. Earlier revisions
/// described these with separate integrity blocks, which meant three trust models that could drift
/// apart - one could gain a check the others lacked, and a reviewer had to read three code paths to
/// answer "what is verified before we touch this machine". They are now one closed
/// <see cref="Components"/> set with one verification order.
/// </para>
///
/// <para>
/// <b>Source identity versus distribution identity.</b> These are deliberately separate. The
/// <see cref="Source"/> block records what was released: the annotated tag and the commit it peeled
/// to at publish time. The <see cref="Distribution"/> block records what this particular transport
/// artifact is: which version it claims to carry and which source commit it was actually built
/// from. Verification is the comparison between the two, plus the comparison against the tag the
/// installing host independently resolved. Collapsing them into one set of fields would remove the
/// very thing that makes a distribution ref safe to fetch from.
/// </para>
///
/// <para>
/// This type is environment-neutral. Application FQDNs, AD domains, domain controllers, database
/// hosts, certificate thumbprints, accounts, and secrets are machine state and belong under
/// ProgramData - never here. The same distribution must install at any customer without
/// modification, and adding an environment field would silently break that.
/// </para>
/// </summary>
public sealed record ReleaseManifest
{
    /// <summary>
    /// Manifest format version, so an installer can refuse a distribution it cannot read.
    ///
    /// <para>
    /// Version 2 consolidated the previously separate app / Host Agent / prerequisite integrity
    /// blocks into <see cref="Components"/>, and split source identity from distribution identity.
    /// </para>
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    public const string ProductName = "ITAdmin";

    /// <summary>File name of the manifest at the root of a distribution.</summary>
    public const string FileName = "release.manifest.json";

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("product")]
    public string Product { get; init; } = ProductName;

    /// <summary>What was released: the annotated tag and the commit it peeled to.</summary>
    [JsonPropertyName("source")]
    public SourceReleaseIdentity Source { get; init; } = new();

    /// <summary>What this transport artifact is, and what it was actually built from.</summary>
    [JsonPropertyName("distribution")]
    public DistributionIdentity Distribution { get; init; } = new();

    /// <summary>
    /// Schema requirement carried by this release, so the installer can record what a successful
    /// migration brought the database to without needing EF tooling on the target.
    /// </summary>
    [JsonPropertyName("migrations")]
    public ReleaseMigrationInfo Migrations { get; init; } = new();

    /// <summary>
    /// Every directory in the distribution, keyed by its path relative to the distribution root.
    /// This is a <em>closed</em> set: a distribution containing a top-level entry that is not the
    /// manifest and not a declared component is refused, so an extra binary cannot ride along
    /// unverified.
    /// </summary>
    [JsonPropertyName("components")]
    public IReadOnlyDictionary<string, DistributionComponent> Components { get; init; } =
        new Dictionary<string, DistributionComponent>(StringComparer.Ordinal);

    /// <summary>
    /// Windows runtime prerequisites carried by this distribution, with the metadata needed to
    /// reassemble and verify each one before it is executed.
    /// </summary>
    [JsonPropertyName("prerequisites")]
    public IReadOnlyList<PrerequisitePayload> Prerequisites { get; init; } = [];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Parses manifest JSON. Returns null rather than throwing on malformed input so callers can
    /// report a clean "this is not a valid ITAdmin distribution" instead of a parser stack.
    /// </summary>
    public static ReleaseManifest? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReleaseManifest>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Component describing the application payload IIS is pointed at.</summary>
    public DistributionComponent? ApplicationComponent =>
        Components.TryGetValue(DeploymentLayout.PayloadDirectoryName, out var component) ? component : null;

    /// <summary>Component describing the privileged Host Agent, when the release carries one.</summary>
    public DistributionComponent? HostAgentComponent =>
        Components.TryGetValue(DeploymentLayout.HostAgentDirectoryName, out var component) ? component : null;

    /// <summary>
    /// Structural validation of a parsed manifest, independent of any files on disk. This is the
    /// first gate an installer runs before it trusts a distribution enough to look at its contents.
    /// </summary>
    public ManifestValidationResult Validate()
    {
        var errors = new List<string>();

        if (SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add(
                $"Unsupported manifest schemaVersion {SchemaVersion}; this installer supports {CurrentSchemaVersion}.");
        }

        if (!string.Equals(Product, ProductName, StringComparison.Ordinal))
        {
            errors.Add($"Distribution product is '{Product}', expected '{ProductName}'.");
        }

        errors.AddRange(Source.Validate());
        errors.AddRange(Distribution.Validate());

        // The two identity blocks must agree with each other. They are recorded independently by
        // the publisher, so a disagreement means the publish pipeline built one thing and labelled
        // it another - which is exactly the situation that must never reach a server.
        if (!string.IsNullOrWhiteSpace(Source.Version)
            && !string.IsNullOrWhiteSpace(Distribution.Version)
            && !string.Equals(Source.Version, Distribution.Version, StringComparison.Ordinal))
        {
            errors.Add(
                $"Distribution version '{Distribution.Version}' does not match source release version "
                + $"'{Source.Version}'.");
        }

        if (!ReleaseAcquisition.CommitsMatch(Source.Commit, Distribution.SourceCommit))
        {
            errors.Add(
                "Distribution sourceCommit does not match the source release commit; the payload was "
                + "not built from the commit this release claims.");
        }

        if (Components.Count == 0)
        {
            errors.Add("Distribution declares no components.");
        }

        if (ApplicationComponent is null)
        {
            errors.Add(
                $"Distribution declares no '{DeploymentLayout.PayloadDirectoryName}' component; "
                + "there is nothing for IIS to serve.");
        }

        foreach (var (path, component) in Components)
        {
            errors.AddRange(component.Validate(path).Select(error => $"component '{path}': {error}"));
        }

        foreach (var prerequisite in Prerequisites)
        {
            errors.AddRange(prerequisite.Validate().Select(error => $"prerequisite '{prerequisite.Name}': {error}"));

            if (!Components.ContainsKey(prerequisite.ComponentPath))
            {
                errors.Add(
                    $"prerequisite '{prerequisite.Name}': declares component path "
                    + $"'{prerequisite.ComponentPath}', which is not a declared component.");
            }
        }

        return new ManifestValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// What was released. Recorded by the publisher from the annotated tag it was asked to publish.
///
/// <para>
/// An annotated tag is not immutable - a repository administrator, or anyone with force-push
/// rights, can move or delete one. The property this design actually relies on is different and
/// weaker but sufficient: an annotated tag is an explicit tag <em>object</em>, so it names a
/// specific peeled commit that can be re-checked independently at any later time, and a change to
/// it is a visible change to a ref rather than an invisible reinterpretation. Protecting production
/// <c>v*</c> tags against update and deletion is a repository-governance responsibility documented
/// for the repository owner; it is deliberately not something a customer server tries to enforce.
/// </para>
/// </summary>
public sealed record SourceReleaseIdentity
{
    /// <summary>MAJOR.MINOR.PATCH[-prerelease] of the release.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>The annotated tag name, e.g. <c>v2.1.0</c>.</summary>
    [JsonPropertyName("tag")]
    public string Tag { get; init; } = string.Empty;

    /// <summary>The commit that tag peeled to at publish time.</summary>
    [JsonPropertyName("commit")]
    public string Commit { get; init; } = string.Empty;

    internal IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!ReleaseVersion.TryParse(Version, out _))
        {
            errors.Add($"source.version '{Version}' is not a valid MAJOR.MINOR.PATCH[-prerelease] version.");
        }

        if (string.IsNullOrWhiteSpace(Tag))
        {
            errors.Add("source.tag is required.");
        }

        if (!GitReleaseRefs.IsObjectId(Commit))
        {
            errors.Add("source.commit is not a Git object id.");
        }

        return errors;
    }
}

/// <summary>
/// What this transport artifact is. Distinct from <see cref="SourceReleaseIdentity"/> because a
/// distribution ref is a delivery mechanism that can, in principle, be repointed - so what it
/// claims about itself is evidence to be checked, never authority to be trusted.
/// </summary>
public sealed record DistributionIdentity
{
    /// <summary>Version this distribution claims to carry.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>Source commit the payload in this distribution was actually built from.</summary>
    [JsonPropertyName("sourceCommit")]
    public string SourceCommit { get; init; } = string.Empty;

    [JsonPropertyName("builtAtUtc")]
    public DateTimeOffset BuiltAtUtc { get; init; }

    /// <summary>Ref this distribution is published to, recorded for diagnosis.</summary>
    [JsonPropertyName("ref")]
    public string Ref { get; init; } = string.Empty;

    internal IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!ReleaseVersion.TryParse(Version, out _))
        {
            errors.Add($"distribution.version '{Version}' is not a valid version.");
        }

        if (!GitReleaseRefs.IsObjectId(SourceCommit))
        {
            errors.Add("distribution.sourceCommit is not a Git object id.");
        }

        if (BuiltAtUtc == default)
        {
            errors.Add("distribution.builtAtUtc is required.");
        }

        return errors;
    }
}

/// <summary>What a component is for. Used to apply the right rules to the right tree.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DistributionComponentKind
{
    /// <summary>The ASP.NET publish output. Becomes the IIS physicalPath.</summary>
    ApplicationPayload = 0,

    /// <summary>The privileged Windows service. Installed outside the web root.</summary>
    HostAgent = 1,

    /// <summary>Windows runtime prerequisite payload (chunked third-party installers).</summary>
    RuntimePrerequisite = 2,
}

/// <summary>One verified directory inside a distribution.</summary>
public sealed record DistributionComponent
{
    [JsonPropertyName("kind")]
    public DistributionComponentKind Kind { get; init; }

    /// <summary>Per-file digests for everything under this component's directory.</summary>
    [JsonPropertyName("integrity")]
    public ReleaseIntegrity Integrity { get; init; } = new();

    internal IReadOnlyList<string> Validate(string path)
    {
        var errors = new List<string>();

        // A component path becomes a directory name under the distribution root and, for the
        // application payload, an IIS physicalPath. Anything that could escape the root, or that
        // behaves differently on Windows, is refused before it reaches the filesystem.
        if (!ReleaseIntegrity.IsNormalisedRelativePath(path))
        {
            errors.Add("path is not a normalised relative directory name.");
        }

        if (!Enum.IsDefined(Kind))
        {
            errors.Add("kind is not a known component kind.");
        }

        errors.AddRange(Integrity.Validate());

        return errors;
    }
}

/// <summary>
/// A Windows runtime prerequisite carried inside the distribution.
///
/// <para>
/// The ASP.NET Core Hosting Bundle is a Microsoft redistributable well over the practical per-object
/// limit of a Git host, so it is stored as an ordered set of bounded chunks. Every chunk carries its
/// own digest, and the reassembled file must match <see cref="Sha256"/> before it is executed.
/// Verifying only the chunks would prove the pieces arrived intact but not that they were
/// reassembled into the file the release actually pinned.
/// </para>
/// </summary>
public sealed record PrerequisitePayload
{
    /// <summary>Operator-facing name, e.g. <c>ASP.NET Core Hosting Bundle</c>.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>File name to reassemble to, e.g. <c>dotnet-hosting-10.0.10-win.exe</c>.</summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    /// <summary>Version of the prerequisite this release pins.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>Distribution component directory holding this prerequisite's chunks.</summary>
    [JsonPropertyName("componentPath")]
    public string ComponentPath { get; init; } = string.Empty;

    /// <summary>
    /// ITAdmin's own SHA-256 over the complete file, verified after reassembly and immediately
    /// before execution. This is the digest that authorises running the installer on a server.
    ///
    /// <para>
    /// Distinct from <see cref="UpstreamHash"/>: this one is computed by the publisher from the
    /// bytes it had already verified upstream, and answers "did those bytes reach this server
    /// intact". It is deliberately in ITAdmin's algorithm, not the vendor's, so our distribution
    /// integrity does not change shape when a vendor changes publishing practice.
    /// </para>
    /// </summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>
    /// The vendor's published digest that this file was verified against at publish time, and the
    /// algorithm it was published in.
    ///
    /// <para>
    /// Carried into the distribution purely for provenance: it lets an auditor - months later, with
    /// only the distribution in hand - re-derive whether the file ITAdmin ships is the file the
    /// vendor published, without needing access to the build machine or the CI log. Nothing on the
    /// server verifies against it, because the server never sees the vendor.
    /// </para>
    /// </summary>
    [JsonPropertyName("upstreamHash")]
    public string UpstreamHash { get; init; } = string.Empty;

    [JsonPropertyName("upstreamHashAlgorithm")]
    public UpstreamHashAlgorithm UpstreamHashAlgorithm { get; init; } = UpstreamHashAlgorithm.Sha512;

    /// <summary>Where the vendor published that digest, recorded so the pin can be re-checked.</summary>
    [JsonPropertyName("upstreamHashSource")]
    public string UpstreamHashSource { get; init; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }

    /// <summary>
    /// Chunk digests in reassembly order. The order is the contract: chunks are concatenated in
    /// this sequence, not in whatever order a directory listing happens to produce.
    /// </summary>
    [JsonPropertyName("chunkDigests")]
    public IReadOnlyList<string> ChunkDigests { get; init; } = [];

    /// <summary>Where the prerequisite came from, recorded so an auditor can retrace the supply chain.</summary>
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; init; } = string.Empty;

    public int ChunkCount => ChunkDigests.Count;

    /// <summary>Chunk file name for an index, e.g. <c>dotnet-hosting-10.0.10-win.exe.part0000</c>.</summary>
    public string ChunkFileName(int index) => PrerequisiteChunking.ChunkFileName(FileName, index);

    /// <summary>Structural checks, public so a publisher can refuse a bad payload before staging it.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("name is required.");
        }

        if (!PrerequisiteChunking.IsSafeFileName(FileName))
        {
            errors.Add($"fileName '{FileName}' is not a plain file name.");
        }

        if (!ReleaseIntegrity.IsNormalisedRelativePath(ComponentPath))
        {
            errors.Add("componentPath is not a normalised relative directory name.");
        }

        if (!ReleaseIntegrity.IsSha256Hex(Sha256))
        {
            errors.Add("sha256 is not a SHA-256 hex digest.");
        }

        // Provenance is required, not optional: a prerequisite with no record of what it was
        // verified against upstream cannot be audited later, and its presence in a distribution
        // would imply a check that may never have happened.
        if (string.IsNullOrWhiteSpace(UpstreamHash))
        {
            errors.Add("upstreamHash is required; it records what this file was verified against at publish time.");
        }
        else
        {
            var expectedLength = UpstreamHashAlgorithm switch
            {
                UpstreamHashAlgorithm.Sha256 => 64,
                UpstreamHashAlgorithm.Sha512 => 128,
                _ => 0,
            };

            if (expectedLength == 0)
            {
                errors.Add("upstreamHashAlgorithm is not a supported algorithm.");
            }
            else if (UpstreamHash.Length != expectedLength || !UpstreamHash.All(Uri.IsHexDigit))
            {
                errors.Add(
                    $"upstreamHash must be a {expectedLength}-character {UpstreamHashAlgorithm} hex digest.");
            }
        }

        if (string.IsNullOrWhiteSpace(UpstreamHashSource))
        {
            errors.Add("upstreamHashSource is required so the pinned digest can be traced to its origin.");
        }

        if (SizeBytes <= 0)
        {
            errors.Add("sizeBytes must be positive.");
        }

        if (ChunkDigests.Count == 0)
        {
            errors.Add("chunkDigests is empty; there is nothing to reassemble.");
        }

        for (var index = 0; index < ChunkDigests.Count; index++)
        {
            if (!ReleaseIntegrity.IsSha256Hex(ChunkDigests[index]))
            {
                errors.Add($"chunkDigests[{index}] is not a SHA-256 hex digest.");
            }
        }

        return errors;
    }
}

public sealed record ReleaseMigrationInfo
{
    /// <summary>Identifier of the newest EF migration compiled into this release, if any.</summary>
    [JsonPropertyName("latest")]
    public string? Latest { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }
}

public sealed record ManifestValidationResult(bool IsValid, IReadOnlyList<string> Errors);
