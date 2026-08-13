using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace ITAdmin.Deployment;

/// <summary>
/// Per-file digests for every payload file in a release, so a staged copy can be proven identical
/// to what the build produced before anything is activated.
///
/// <para>
/// This detects truncated transfers, partially-extracted archives, and accidental edits to a
/// staged release. It is an <em>integrity</em> mechanism, not an authenticity one: anybody who can
/// rewrite the payload can rewrite the manifest beside it. Authenticity requires signing the
/// artifact, which is a deliberate follow-up rather than something this format precludes.
/// </para>
/// </summary>
public sealed record ReleaseIntegrity
{
    public const string Sha256 = "SHA-256";

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; init; } = Sha256;

    [JsonPropertyName("fileCount")]
    public int FileCount { get; init; }

    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; init; }

    /// <summary>
    /// Payload-relative path (forward slashes, case preserved) to lowercase hex digest.
    /// Paths are normalised so a manifest built on one OS verifies on Windows unchanged.
    /// </summary>
    [JsonPropertyName("files")]
    public IReadOnlyDictionary<string, string> Files { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Structural problems in the integrity metadata itself, independent of any payload.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!string.Equals(Algorithm, Sha256, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported integrity algorithm '{Algorithm}'; expected '{Sha256}'.");
        }

        if (Files.Count == 0)
        {
            errors.Add("Release manifest contains no file digests.");
        }

        if (FileCount != Files.Count)
        {
            errors.Add($"Manifest fileCount is {FileCount} but {Files.Count} digests are present.");
        }

        foreach (var (path, digest) in Files)
        {
            if (!IsNormalisedRelativePath(path))
            {
                errors.Add($"Manifest path '{path}' is not a normalised relative payload path.");
            }

            if (!IsSha256Hex(digest))
            {
                errors.Add($"Manifest digest for '{path}' is not a SHA-256 hex digest.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Rejects anything that could escape the payload root or behave differently on Windows —
    /// absolute paths, drive letters, backslashes, and parent traversal.
    /// </summary>
    public static bool IsNormalisedRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.StartsWith('/')
            || path.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path.Split('/');
        return segments.Length != 0
            && segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    public static bool IsSha256Hex(string? digest) =>
        !string.IsNullOrWhiteSpace(digest)
        && digest.Length == 64
        && digest.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

    public static string ToPayloadRelativePath(string root, string filePath) =>
        Path.GetRelativePath(root, filePath).Replace('\\', '/');

    public static string ComputeFileDigest(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Builds integrity metadata for every file under <paramref name="payloadRoot"/>.</summary>
    public static ReleaseIntegrity Create(string payloadRoot)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        long totalBytes = 0;

        foreach (var filePath in Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = ToPayloadRelativePath(payloadRoot, filePath);
            files[relativePath] = ComputeFileDigest(filePath);
            totalBytes += new FileInfo(filePath).Length;
        }

        return new ReleaseIntegrity
        {
            Algorithm = Sha256,
            FileCount = files.Count,
            TotalBytes = totalBytes,
            Files = files,
        };
    }

    /// <summary>
    /// Verifies a staged payload against this manifest. Reports missing, altered, and unexpected
    /// files — an unexpected extra file matters because a release directory is supposed to be an
    /// exact reproduction of the build output.
    /// </summary>
    public IntegrityVerificationResult Verify(string payloadRoot)
    {
        var missing = new List<string>();
        var altered = new List<string>();

        foreach (var (relativePath, expectedDigest) in Files)
        {
            var fullPath = Path.Combine(payloadRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                missing.Add(relativePath);
                continue;
            }

            if (!string.Equals(ComputeFileDigest(fullPath), expectedDigest, StringComparison.Ordinal))
            {
                altered.Add(relativePath);
            }
        }

        var unexpected = Directory.Exists(payloadRoot)
            ? Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories)
                .Select(filePath => ToPayloadRelativePath(payloadRoot, filePath))
                .Where(relativePath => !Files.ContainsKey(relativePath))
                .OrderBy(relativePath => relativePath, StringComparer.Ordinal)
                .ToList()
            : [];

        return new IntegrityVerificationResult(missing, altered, unexpected);
    }
}

public sealed record IntegrityVerificationResult(
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Altered,
    IReadOnlyList<string> Unexpected)
{
    public bool IsValid => Missing.Count == 0 && Altered.Count == 0 && Unexpected.Count == 0;

    public IReadOnlyList<string> Describe()
    {
        var messages = new List<string>();
        foreach (var path in Missing)
        {
            messages.Add($"missing: {path}");
        }

        foreach (var path in Altered)
        {
            messages.Add($"altered: {path}");
        }

        foreach (var path in Unexpected)
        {
            messages.Add($"unexpected: {path}");
        }

        return messages;
    }
}
