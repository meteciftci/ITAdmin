namespace ITAdmin.Deployment;

/// <summary>
/// The gate between "Git handed us a tree" and "this is the release we asked for".
///
/// <para>
/// Fetching a distribution ref proves only that the remote had something at that name. Everything
/// else is checked here, in one place, in a fixed order, before any machine mutation:
/// </para>
/// <list type="number">
///   <item><description>the manifest parses and is structurally valid;</description></item>
///   <item><description>its <em>source</em> version equals the version of the annotated tag we resolved;</description></item>
///   <item><description>its <em>source</em> commit equals that tag's peeled commit;</description></item>
///   <item><description>its <em>distribution</em> identity agrees with its source identity;</description></item>
///   <item><description>the distribution tree contains exactly the declared components and nothing else;</description></item>
///   <item><description>every component's files match their digests;</description></item>
///   <item><description>every declared prerequisite's chunks are present and intact.</description></item>
/// </list>
///
/// <para>
/// (2) and (3) are what make a distribution ref safe to fetch from at all. The ref is a delivery
/// mechanism and can in principle be repointed; the tag the host independently resolved is the
/// authority. If a distribution ref were ever repointed at a payload built from a different commit,
/// or at another version's payload, the mismatch surfaces here and the install fails closed rather
/// than activating something nobody released.
/// </para>
///
/// <para>
/// (5) matters as much as the digests. Verifying only declared files would let an extra executable
/// ride along in the tree, unverified and unmentioned, next to binaries the installer is about to
/// run as SYSTEM. The component set is closed.
/// </para>
/// </summary>
public static class ReleaseAcquisition
{
    /// <summary>
    /// Verifies an acquired distribution directory against the release identity that was requested.
    /// Returns every problem rather than throwing on the first, so an operator sees the whole
    /// picture instead of fixing one fault at a time.
    /// </summary>
    public static ReleaseAcquisitionResult Verify(
        string acquiredDirectory,
        ReleaseVersion expectedVersion,
        string expectedSourceCommit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acquiredDirectory);
        ArgumentNullException.ThrowIfNull(expectedVersion);

        if (!Directory.Exists(acquiredDirectory))
        {
            return Failure(
                DistributionFault.TreeMalformed,
                $"Acquired distribution directory does not exist: {acquiredDirectory}");
        }

        var manifestPath = Path.Combine(acquiredDirectory, ReleaseManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            return Failure(
                DistributionFault.TreeMalformed,
                $"{ReleaseManifest.FileName} is missing. The distribution ref does not contain an "
                + "ITAdmin distribution.");
        }

        var manifest = ReleaseManifest.FromJson(File.ReadAllText(manifestPath));
        if (manifest is null)
        {
            return Failure(
                DistributionFault.TreeMalformed,
                $"{ReleaseManifest.FileName} is not valid JSON.");
        }

        // --- 1. Structural validity -----------------------------------------------------------
        var validation = manifest.Validate();
        if (!validation.IsValid)
        {
            return new ReleaseAcquisitionResult(false, manifest, DistributionFault.TreeMalformed, validation.Errors);
        }

        var problems = new List<string>();
        var fault = DistributionFault.None;

        // --- 2/3. Source identity against the tag this host independently resolved --------------
        if (!ReleaseVersion.TryParse(manifest.Source.Version, out var sourceVersion)
            || !sourceVersion.Equals(expectedVersion))
        {
            fault = DistributionFault.SourceIdentityMismatch;
            problems.Add(
                $"Distribution declares source version '{manifest.Source.Version}' but the requested "
                + $"release is '{expectedVersion}'.");
        }

        if (!CommitsMatch(manifest.Source.Commit, expectedSourceCommit))
        {
            fault = DistributionFault.SourceIdentityMismatch;
            problems.Add(
                $"Distribution was published for source commit '{Abbreviate(manifest.Source.Commit)}' but "
                + $"the release tag peels to '{Abbreviate(expectedSourceCommit)}'.");
        }

        // --- 4. Distribution identity against source identity -----------------------------------
        // Manifest.Validate() already cross-checks these; re-stating the fault classification here
        // keeps the caller's diagnosis specific rather than generic "malformed".
        if (!CommitsMatch(manifest.Distribution.SourceCommit, manifest.Source.Commit))
        {
            fault = DistributionFault.SourceIdentityMismatch;
            problems.Add("Distribution sourceCommit disagrees with the source release commit it claims.");
        }

        // --- 5. Closed component set ------------------------------------------------------------
        problems.AddRange(FindUndeclaredEntries(acquiredDirectory, manifest, ref fault));

        // --- 6. Component integrity -------------------------------------------------------------
        foreach (var (relativePath, component) in manifest.Components)
        {
            var componentRoot = Path.Combine(acquiredDirectory, relativePath);
            if (!Directory.Exists(componentRoot))
            {
                fault = fault is DistributionFault.None ? DistributionFault.TreeMalformed : fault;
                problems.Add($"Declared component '{relativePath}' is missing from the distribution.");
                continue;
            }

            var result = component.Integrity.Verify(componentRoot);
            if (!result.IsValid)
            {
                fault = fault is DistributionFault.None ? DistributionFault.IntegrityFailure : fault;
                problems.AddRange(result.Describe().Select(problem => $"{relativePath}: {problem}"));
            }
        }

        // --- 7. Prerequisite chunks -------------------------------------------------------------
        foreach (var prerequisite in manifest.Prerequisites)
        {
            var chunkRoot = Path.Combine(acquiredDirectory, prerequisite.ComponentPath);
            for (var index = 0; index < prerequisite.ChunkDigests.Count; index++)
            {
                var chunkPath = Path.Combine(chunkRoot, prerequisite.ChunkFileName(index));
                if (!File.Exists(chunkPath))
                {
                    fault = fault is DistributionFault.None ? DistributionFault.IntegrityFailure : fault;
                    problems.Add(
                        $"prerequisite '{prerequisite.Name}': chunk {index} is missing "
                        + $"({prerequisite.ChunkFileName(index)}).");
                }
            }
        }

        return new ReleaseAcquisitionResult(problems.Count == 0, manifest, fault, problems);

        static ReleaseAcquisitionResult Failure(DistributionFault fault, string problem) =>
            new(false, null, fault, [problem]);
    }

    /// <summary>
    /// Every file in the distribution that does not belong to a declared component.
    ///
    /// <para>
    /// This walks the whole tree rather than just the root, because component paths are nested
    /// (<c>prerequisites/aspnetcore-hosting-bundle</c>) and a root-only check would happily ignore
    /// an extra executable dropped one level down. Each component's own integrity check already
    /// reports files inside it that the manifest does not list; this closes the remaining gap -
    /// files that sit inside no component at all.
    /// </para>
    ///
    /// <para>
    /// The <c>.git</c> directory is exempt: a distribution acquired by fetching a ref is a working
    /// tree, and Git's own metadata is not part of the payload. It is never staged - the installer
    /// copies the manifest and the declared components explicitly, not the directory wholesale.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> FindUndeclaredEntries(
        string acquiredDirectory,
        ReleaseManifest manifest,
        ref DistributionFault fault)
    {
        var problems = new List<string>();

        var componentPrefixes = manifest.Components.Keys
            .Select(path => path.Replace('/', Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .ToList();

        foreach (var file in Directory.EnumerateFiles(acquiredDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(acquiredDirectory, file);

            if (relative.StartsWith(".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(relative, ReleaseManifest.FileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (componentPrefixes.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            problems.Add($"Distribution contains undeclared content '{relative.Replace('\\', '/')}'.");

            // A tree full of stray files should not produce a wall of identical findings.
            if (problems.Count >= 20)
            {
                problems.Add("... further undeclared entries suppressed.");
                break;
            }
        }

        if (problems.Count > 0 && fault is DistributionFault.None)
        {
            fault = DistributionFault.UndeclaredContent;
        }

        return problems;
    }

    /// <summary>
    /// Compares two commit ids. Accepts an abbreviation on either side, because a manifest written
    /// by CI and a peel line read from a remote need not use the same width, but requires at least
    /// a 7-character prefix so a truncated or empty value cannot pass as a match.
    /// </summary>
    public static bool CommitsMatch(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var a = left.Trim().ToLowerInvariant();
        var b = right.Trim().ToLowerInvariant();

        if (a.Length < 7 || b.Length < 7 || !a.All(Uri.IsHexDigit) || !b.All(Uri.IsHexDigit))
        {
            return false;
        }

        var shortest = Math.Min(a.Length, b.Length);
        return string.CompareOrdinal(a, 0, b, 0, shortest) == 0;
    }

    private static string Abbreviate(string? commit) =>
        string.IsNullOrWhiteSpace(commit)
            ? "(none)"
            : commit.Trim()[..Math.Min(12, commit.Trim().Length)];
}

/// <summary>
/// Why a distribution was refused. Classified so an operator is told which of several very
/// different problems they have, rather than being handed a list of symptoms to interpret.
/// </summary>
public enum DistributionFault
{
    None = 0,

    /// <summary>The tree is not a readable ITAdmin distribution at all.</summary>
    TreeMalformed = 1,

    /// <summary>The tree is well-formed but is not the release that was requested.</summary>
    SourceIdentityMismatch = 2,

    /// <summary>Declared content is missing or altered.</summary>
    IntegrityFailure = 3,

    /// <summary>The tree carries content the manifest does not declare.</summary>
    UndeclaredContent = 4,
}

public sealed record ReleaseAcquisitionResult(
    bool IsAcceptable,
    ReleaseManifest? Manifest,
    DistributionFault Fault,
    IReadOnlyList<string> Problems)
{
    /// <summary>
    /// One operator-facing sentence naming the class of failure, followed by the specifics. Written
    /// so the first line alone tells someone whether to look at the publisher, the ref, or the disk.
    /// </summary>
    public string Describe()
    {
        if (IsAcceptable)
        {
            return "Distribution verified.";
        }

        var headline = Fault switch
        {
            DistributionFault.TreeMalformed =>
                "The fetched distribution is not a readable ITAdmin distribution. The ref exists but "
                + "its contents are not what the publisher should have produced.",
            DistributionFault.SourceIdentityMismatch =>
                "The fetched distribution is not the release that was requested. The distribution ref "
                + "does not carry the release its name claims - re-publish the release, and treat this "
                + "as a supply-chain discrepancy until explained.",
            DistributionFault.IntegrityFailure =>
                "The fetched distribution failed integrity verification. Content is missing or altered "
                + "relative to what the publisher recorded.",
            DistributionFault.UndeclaredContent =>
                "The fetched distribution carries content its manifest does not declare. Nothing "
                + "undeclared is installed.",
            _ => "The fetched distribution was refused.",
        };

        return headline + Environment.NewLine + string.Join(Environment.NewLine, Problems.Select(p => "  - " + p));
    }
}
