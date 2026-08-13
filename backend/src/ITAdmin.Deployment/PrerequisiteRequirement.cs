using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.Deployment;

/// <summary>
/// Hash algorithms ITAdmin can verify a third-party download against.
///
/// <para>
/// This exists because the algorithm is the vendor's choice, not ours. Microsoft publishes SHA-512
/// for .NET runtime downloads; another vendor may publish SHA-256. Encoding the algorithm in the
/// metadata - rather than assuming one - is what lets the pinned value be checked against what the
/// vendor actually published, instead of against a number somebody re-derived by hand.
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpstreamHashAlgorithm
{
    Sha256 = 0,
    Sha512 = 1,
}

/// <summary>
/// The repository-controlled authority for which third-party runtime prerequisite a release pins.
///
/// <para>
/// This is deliberately a checked-in file rather than a build-time lookup. "Fetch the latest Hosting
/// Bundle" would make every publish non-reproducible and would let an upstream change alter what
/// customers receive without a single line of review. Rebuilding the same source release must
/// consume the same runtime bytes, so version, URL, algorithm, and digest are a repository change:
/// edit, commit, tag, publish. The network supplies bytes; the repository decides which bytes count.
/// </para>
///
/// <para>
/// <b>Two hashes, two jobs.</b> <see cref="PinnedPrerequisite.ExpectedHash"/> is the <em>vendor's</em>
/// published digest, in the <em>vendor's</em> algorithm, and it answers exactly one question: did we
/// download what Microsoft published? Everything after that - chunk digests, the reassembled-file
/// digest, component integrity - is ITAdmin's own distribution integrity in ITAdmin's own algorithm,
/// and answers a different question: did the bytes we verified upstream survive the trip to this
/// server intact? Overloading one field for both would mean a change to either the vendor's
/// publishing practice or our own integrity scheme silently weakened the other.
/// </para>
/// </summary>
public sealed record PrerequisiteRequirementMetadata
{
    public const int CurrentSchemaVersion = 3;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("product")]
    public string Product { get; init; } = string.Empty;

    /// <summary>Operator-facing component name; also slugged into the distribution component path.</summary>
    [JsonPropertyName("componentName")]
    public string ComponentName { get; init; } = string.Empty;

    [JsonPropertyName("majorVersion")]
    public int MajorVersion { get; init; }

    [JsonPropertyName("minimumVersion")]
    public string MinimumVersion { get; init; } = string.Empty;

    [JsonPropertyName("targetFramework")]
    public string TargetFramework { get; init; } = string.Empty;

    [JsonPropertyName("installerFileNamePattern")]
    public string InstallerFileNamePattern { get; init; } = string.Empty;

    [JsonPropertyName("ancmRelativePath")]
    public string AncmRelativePath { get; init; } = string.Empty;

    [JsonPropertyName("ancmModuleName")]
    public string AncmModuleName { get; init; } = string.Empty;

    [JsonPropertyName("pinned")]
    public PinnedPrerequisite Pinned { get; init; } = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static PrerequisiteRequirementMetadata? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PrerequisiteRequirementMetadata>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether this metadata is complete enough to publish a release with.
    ///
    /// <para>
    /// Separate from <see cref="Validate"/> because a developer may legitimately work with an
    /// incomplete file locally, while the publisher must never ship one. Everything the publisher
    /// needs in order to verify a download is required here.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ValidateForPublishing()
    {
        var errors = new List<string>(Validate());

        if (Pinned.LooksLikeAPlaceholder())
        {
            errors.Add(
                $"pinned.expectedHash is a placeholder ('{Pinned.ExpectedHash}'). It must be the digest the "
                + "vendor publishes for this exact file. An unverified third-party installer must never "
                + "enter a distribution.");
        }

        return errors;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Unsupported requirement schemaVersion {SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(ComponentName))
        {
            errors.Add("componentName is required.");
        }

        errors.AddRange(Pinned.Validate());

        return errors;
    }
}

/// <summary>
/// The exact third-party file a release pins, and the vendor's own published digest for it.
/// </summary>
public sealed record PinnedPrerequisite
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    /// <summary>Authoritative vendor URL the publisher downloads from. Nothing else is acceptable.</summary>
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; init; } = string.Empty;

    /// <summary>
    /// The algorithm the vendor publishes in. Microsoft publishes SHA-512 for .NET runtime files.
    /// Recorded rather than assumed, so the pinned value can be compared with what the vendor
    /// actually published instead of a re-derived number.
    /// </summary>
    [JsonPropertyName("hashAlgorithm")]
    public UpstreamHashAlgorithm HashAlgorithm { get; init; } = UpstreamHashAlgorithm.Sha512;

    /// <summary>Vendor-published digest, lowercase hex, in <see cref="HashAlgorithm"/>.</summary>
    [JsonPropertyName("expectedHash")]
    public string ExpectedHash { get; init; } = string.Empty;

    /// <summary>Where the digest was read from, so an auditor can retrace the decision.</summary>
    [JsonPropertyName("hashSource")]
    public string HashSource { get; init; } = string.Empty;

    /// <summary>Expected hex length for the declared algorithm.</summary>
    public int ExpectedHashLength => HashAlgorithm switch
    {
        UpstreamHashAlgorithm.Sha256 => 64,
        UpstreamHashAlgorithm.Sha512 => 128,
        _ => 0,
    };

    /// <summary>
    /// Computes the digest of a downloaded file in the declared upstream algorithm.
    ///
    /// <para>
    /// An unknown algorithm throws rather than falling back to a default. Silently hashing with the
    /// wrong algorithm would produce a mismatch that looks like tampering, or - far worse, if a
    /// weaker default were ever chosen - a match that means nothing.
    /// </para>
    /// </summary>
    public string ComputeUpstreamDigest(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);

        return HashAlgorithm switch
        {
            UpstreamHashAlgorithm.Sha256 => Convert.ToHexStringLower(SHA256.HashData(stream)),
            UpstreamHashAlgorithm.Sha512 => Convert.ToHexStringLower(SHA512.HashData(stream)),
            _ => throw new NotSupportedException(
                $"Upstream hash algorithm '{HashAlgorithm}' is not supported; refusing to verify with a "
                + "substitute algorithm."),
        };
    }

    /// <summary>
    /// Verifies a downloaded file against the vendor's published digest. Fail-closed: an unsupported
    /// algorithm, a malformed pin, or a placeholder is a failure, never a skipped check.
    /// </summary>
    public UpstreamVerificationResult VerifyDownload(string filePath)
    {
        if (LooksLikeAPlaceholder())
        {
            return UpstreamVerificationResult.Failed(
                "The pinned upstream digest is a placeholder; refusing to accept the download.");
        }

        if (!IsWellFormedDigest(ExpectedHash))
        {
            return UpstreamVerificationResult.Failed(
                $"The pinned upstream digest is not a valid {HashAlgorithm} hex digest "
                + $"({ExpectedHashLength} characters expected).");
        }

        string actual;
        try
        {
            actual = ComputeUpstreamDigest(filePath);
        }
        catch (NotSupportedException exception)
        {
            return UpstreamVerificationResult.Failed(exception.Message);
        }

        return string.Equals(actual, ExpectedHash.Trim().ToLowerInvariant(), StringComparison.Ordinal)
            ? new UpstreamVerificationResult(true, HashAlgorithm, actual, string.Empty)
            : new UpstreamVerificationResult(
                false,
                HashAlgorithm,
                actual,
                $"{HashAlgorithm} mismatch for {FileName}.{Environment.NewLine}"
                + $"  expected: {ExpectedHash}{Environment.NewLine}"
                + $"  actual:   {actual}{Environment.NewLine}"
                + "Either the vendor republished this file, the pinned digest is wrong, or the download "
                + "was tampered with. Resolve which before republishing.");
    }

    /// <summary>
    /// Whether the pinned digest is obviously not a real one. Kept explicit so a half-finished pin
    /// fails loudly at publish time instead of quietly disabling upstream verification.
    /// </summary>
    public bool LooksLikeAPlaceholder()
    {
        if (string.IsNullOrWhiteSpace(ExpectedHash))
        {
            return true;
        }

        var value = ExpectedHash.Trim();

        if (value.Any(character => !Uri.IsHexDigit(character)))
        {
            return true;
        }

        // A single repeated character is what somebody types when filling in a shape.
        return value.Distinct().Count() <= 1;
    }

    public bool IsWellFormedDigest(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && ExpectedHashLength > 0
        && value.Trim().Length == ExpectedHashLength
        && value.Trim().All(Uri.IsHexDigit);

    internal IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Version))
        {
            errors.Add("pinned.version is required.");
        }

        if (!PrerequisiteChunking.IsSafeFileName(FileName))
        {
            errors.Add($"pinned.fileName '{FileName}' is not a plain file name.");
        }

        // https only. A plaintext download of an executable that will be run as SYSTEM is not
        // something a digest alone makes acceptable - it invites a downgrade to a stale, still
        // correctly-hashed vulnerable build.
        if (!Uri.TryCreate(SourceUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            errors.Add("pinned.sourceUrl must be an absolute https URL.");
        }

        if (!Enum.IsDefined(HashAlgorithm))
        {
            errors.Add("pinned.hashAlgorithm is not a supported algorithm.");
        }
        else if (!LooksLikeAPlaceholder() && !IsWellFormedDigest(ExpectedHash))
        {
            errors.Add(
                $"pinned.expectedHash must be a lowercase {ExpectedHashLength}-character "
                + $"{HashAlgorithm} hex digest.");
        }

        if (string.IsNullOrWhiteSpace(HashSource))
        {
            errors.Add("pinned.hashSource is required so the pinned digest can be re-checked against its origin.");
        }

        return errors;
    }
}

/// <summary>Outcome of verifying a download against the vendor's published digest.</summary>
public sealed record UpstreamVerificationResult(
    bool IsVerified,
    UpstreamHashAlgorithm Algorithm,
    string ActualDigest,
    string Message)
{
    public static UpstreamVerificationResult Failed(string message) =>
        new(false, UpstreamHashAlgorithm.Sha512, string.Empty, message);
}
