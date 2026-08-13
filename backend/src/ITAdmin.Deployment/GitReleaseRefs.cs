using System.Diagnostics.CodeAnalysis;

namespace ITAdmin.Deployment;

/// <summary>
/// Resolves which release a Windows host should install, from the refs a Git remote advertises.
///
/// <para>
/// Production release authority is an <em>annotated</em> stable SemVer tag, never the mutable
/// <c>main</c> branch. The clone of <c>main</c> that the operator makes is bootstrap transport: it
/// carries the installer, not the application. What gets installed is decided here.
/// </para>
///
/// <para>
/// Annotated-only matters, but it is worth being precise about why - an annotated tag is NOT
/// immutable. A repository administrator, or anyone with force-push rights, can move or delete one.
/// The property this design actually relies on is narrower: an annotated tag is an explicit tag
/// <em>object</em> carrying a tagger and naming a specific peeled commit, so the host can record
/// that commit and independently re-check it at any later time, and any change is a visible change
/// to a ref rather than an invisible reinterpretation. A lightweight tag offers none of that: it is
/// a bare pointer with no object of its own, so "the release" could silently become a different
/// commit between resolution and fetch with nothing recorded anywhere.
/// </para>
///
/// <para>
/// Keeping production <c>v*</c> tags protected against update and deletion is therefore a
/// repository-governance responsibility of the repository owner, documented in the deployment
/// guide. A customer server deliberately does not try to enforce it - what it does instead is
/// verify, on every install, that the distribution it fetched was built from the commit the tag it
/// resolved actually peels to.
/// </para>
/// </summary>
public static class GitReleaseRefs
{
    /// <summary>Prefix of the source-authority tags: <c>refs/tags/v2.1.0</c>.</summary>
    public const string TagRefPrefix = "refs/tags/";

    /// <summary>Suffix Git appends to the peeled row of an annotated tag.</summary>
    public const string PeelSuffix = "^{}";

    /// <summary>
    /// Namespace holding prebuilt Windows payloads, one ref per published release:
    /// <c>refs/itadmin/dist/2.1.0</c>.
    ///
    /// <para>
    /// This constant is the single point of change for the transport namespace. If a Git host ever
    /// refuses to advertise or accept refs outside <c>refs/heads</c> and <c>refs/tags</c>, changing
    /// this prefix (and its PowerShell mirror, which a drift test pins to it) moves the entire
    /// distribution mechanism without touching the deployment engine, the manifest, the
    /// verification order, or the installer.
    /// </para>
    ///
    /// <para>
    /// A dedicated namespace outside <c>refs/heads</c> and <c>refs/tags</c> keeps the payload out of
    /// ordinary source history: it never appears in a branch, never shows up in <c>git log</c>, and
    /// a normal developer clone does not download it. The server fetches exactly one of these refs,
    /// at depth 1, and gets exactly one tree.
    /// </para>
    /// </summary>
    public const string DistributionRefPrefix = "refs/itadmin/dist/";

    /// <summary>Distribution ref for a release version, e.g. <c>refs/itadmin/dist/2.1.0</c>.</summary>
    public static string DistributionRef(ReleaseVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return DistributionRefPrefix + version;
    }

    /// <summary>Source-authority tag for a release version, e.g. <c>refs/tags/v2.1.0</c>.</summary>
    public static string SourceTagRef(ReleaseVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return TagRefPrefix + "v" + version;
    }

    /// <summary>
    /// Parses one line of <c>git ls-remote</c> output (<c>&lt;oid&gt;\t&lt;ref&gt;</c>).
    /// Returns false for blank lines and anything that is not an object-id/ref pair, so callers can
    /// stream real Git output without pre-filtering banner or progress text.
    /// </summary>
    public static bool TryParseLsRemoteLine(string? line, out string objectId, out string reference)
    {
        objectId = string.Empty;
        reference = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var separator = line.IndexOfAny([' ', '\t']);
        if (separator <= 0)
        {
            return false;
        }

        var candidateObjectId = line[..separator].Trim();
        var candidateReference = line[(separator + 1)..].Trim();

        if (!IsObjectId(candidateObjectId) || candidateReference.Length == 0)
        {
            return false;
        }

        objectId = candidateObjectId.ToLowerInvariant();
        reference = candidateReference;
        return true;
    }

    /// <summary>
    /// A 40-hex SHA-1 or 64-hex SHA-256 object id. Anything else is not something to fetch.
    /// </summary>
    public static bool IsObjectId([NotNullWhen(true)] string? value) =>
        value is not null
        && value.Length is 40 or 64
        && value.All(Uri.IsHexDigit);
}

/// <summary>How a release channel treats pre-release versions.</summary>
public enum ReleaseChannel
{
    /// <summary>Only stable versions. The production default.</summary>
    Stable = 0,

    /// <summary>Stable plus pre-releases, for pilot hosts that opt in explicitly.</summary>
    Preview = 1,
}

/// <summary>
/// A release candidate found on the remote: the tag that names it and the commit it peels to.
/// </summary>
public sealed record RemoteReleaseTag(string TagName, ReleaseVersion Version, string SourceCommit)
{
    /// <summary>Full ref of this tag.</summary>
    public string Ref => GitReleaseRefs.TagRefPrefix + TagName;

    /// <summary>Distribution ref where this release's prebuilt payload is expected.</summary>
    public string DistributionRef => GitReleaseRefs.DistributionRef(Version);
}

/// <summary>Why a tag advertised by the remote was not usable as release authority.</summary>
public sealed record RejectedReleaseTag(string TagName, ReleaseTagRejection Reason)
{
    public string Describe() => Reason switch
    {
        ReleaseTagRejection.Lightweight =>
            $"'{TagName}' is a lightweight tag. Production releases must be annotated tags "
            + "(git tag -a), which carry a tagger and a stable peeled commit.",
        ReleaseTagRejection.PreRelease =>
            $"'{TagName}' is a pre-release and this host is on the stable channel.",
        ReleaseTagRejection.NotAVersion =>
            $"'{TagName}' is not a MAJOR.MINOR.PATCH release tag.",
        _ => $"'{TagName}' was rejected.",
    };
}

public enum ReleaseTagRejection
{
    /// <summary>No peeled row: the ref points straight at a commit, so it is not a tag object.</summary>
    Lightweight,

    /// <summary>Carries a pre-release label, and the channel is stable.</summary>
    PreRelease,

    /// <summary>Does not parse as a release version at all.</summary>
    NotAVersion,
}

/// <summary>Outcome of resolving the newest usable release from a remote's advertised refs.</summary>
public sealed record ReleaseResolution(
    RemoteReleaseTag? Selected,
    IReadOnlyList<RemoteReleaseTag> Candidates,
    IReadOnlyList<RejectedReleaseTag> Rejected)
{
    [MemberNotNullWhen(true, nameof(Selected))]
    public bool IsResolved => Selected is not null;

    /// <summary>
    /// Operator-facing explanation for a failed resolution. Says what was seen and why nothing
    /// qualified, so "no release found" is never indistinguishable from "the remote was empty".
    /// </summary>
    public string DescribeFailure(ReleaseChannel channel)
    {
        if (IsResolved)
        {
            return string.Empty;
        }

        if (Rejected.Count == 0)
        {
            return "The repository advertises no release tags. Publish an annotated stable release "
                + "tag (for example: git tag -a v1.0.0 -m \"ITAdmin 1.0.0\") before installing.";
        }

        var reasons = Rejected.Select(rejection => "  - " + rejection.Describe());
        return $"No usable release tag was found on the {channel.ToString().ToLowerInvariant()} channel. "
            + $"{Rejected.Count} tag(s) were rejected:{Environment.NewLine}"
            + string.Join(Environment.NewLine, reasons);
    }
}

/// <summary>
/// Chooses the release to install from advertised refs. Pure: it never runs Git itself, so the
/// selection rules are unit-testable and identical whether the refs came from a real remote, a
/// local bare fixture, or a cached listing.
/// </summary>
public static class ReleaseTagResolver
{
    /// <summary>
    /// Resolves the highest usable release from raw <c>git ls-remote --tags</c> output.
    /// </summary>
    public static ReleaseResolution Resolve(string lsRemoteOutput, ReleaseChannel channel) =>
        Resolve(
            (lsRemoteOutput ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries),
            channel);

    /// <summary>
    /// Resolves from already-split <c>ls-remote</c> lines.
    ///
    /// <para>
    /// The pairing rule is the whole point: a tag is accepted only when a <c>refs/tags/x^{}</c> row
    /// exists, and the commit recorded is the one from that row. The unpeeled row of an annotated
    /// tag names the tag object, not the commit, so installing from it would pin the wrong object.
    /// </para>
    /// </summary>
    public static ReleaseResolution Resolve(IEnumerable<string> lsRemoteLines, ReleaseChannel channel)
    {
        ArgumentNullException.ThrowIfNull(lsRemoteLines);

        // tag name -> object id of the unpeeled row, and (when annotated) of the peeled row.
        var unpeeled = new Dictionary<string, string>(StringComparer.Ordinal);
        var peeled = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in lsRemoteLines)
        {
            if (!GitReleaseRefs.TryParseLsRemoteLine(line, out var objectId, out var reference))
            {
                continue;
            }

            if (!reference.StartsWith(GitReleaseRefs.TagRefPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var name = reference[GitReleaseRefs.TagRefPrefix.Length..];
            if (name.EndsWith(GitReleaseRefs.PeelSuffix, StringComparison.Ordinal))
            {
                peeled[name[..^GitReleaseRefs.PeelSuffix.Length]] = objectId;
            }
            else
            {
                unpeeled[name] = objectId;
            }
        }

        var candidates = new List<RemoteReleaseTag>();
        var rejected = new List<RejectedReleaseTag>();

        foreach (var (tagName, _) in unpeeled.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!ReleaseVersion.TryParse(tagName, out var version))
            {
                rejected.Add(new RejectedReleaseTag(tagName, ReleaseTagRejection.NotAVersion));
                continue;
            }

            if (!peeled.TryGetValue(tagName, out var sourceCommit))
            {
                rejected.Add(new RejectedReleaseTag(tagName, ReleaseTagRejection.Lightweight));
                continue;
            }

            if (version.IsPreRelease && channel is ReleaseChannel.Stable)
            {
                rejected.Add(new RejectedReleaseTag(tagName, ReleaseTagRejection.PreRelease));
                continue;
            }

            candidates.Add(new RemoteReleaseTag(tagName, version, sourceCommit));
        }

        var selected = candidates
            .OrderByDescending(candidate => candidate.Version)
            .ThenBy(candidate => candidate.TagName, StringComparer.Ordinal)
            .FirstOrDefault();

        return new ReleaseResolution(selected, candidates, rejected);
    }

    /// <summary>
    /// Resolves one explicitly requested version rather than "the newest". Used when an operator
    /// pins a version, and by the update path when the administrator chooses a specific release.
    /// The same annotated-tag rule applies; a pinned pre-release is allowed only off the stable
    /// channel, so pinning cannot be used to sneak a pre-release onto a stable host.
    /// </summary>
    public static ReleaseResolution ResolveExact(
        IEnumerable<string> lsRemoteLines,
        ReleaseVersion requested,
        ReleaseChannel channel)
    {
        ArgumentNullException.ThrowIfNull(requested);

        var all = Resolve(lsRemoteLines, channel);
        var match = all.Candidates.FirstOrDefault(candidate => candidate.Version.Equals(requested));

        return new ReleaseResolution(match, all.Candidates, all.Rejected);
    }
}

/// <summary>
/// Classified diagnostics for the distribution transport.
///
/// <para>
/// Every failure below produces the same raw Git output - a non-zero exit and some stderr - while
/// having a completely different cause and fix. Publishing them as distinct classes is what stops
/// an operator concluding "Git is broken" when the real answer is "this release was never
/// published" or "your Git host will not accept this ref namespace".
/// </para>
/// </summary>
public static class DistributionRefDiagnostics
{
    /// <summary>What went wrong on the transport, independent of what the tree contained.</summary>
    public enum Fault
    {
        /// <summary>The remote advertises no refs in the ITAdmin namespace at all.</summary>
        NamespaceNotAdvertised,

        /// <summary>The namespace works, but this release has no distribution ref.</summary>
        ReleaseNotPublished,

        /// <summary>The publisher could not create the ref.</summary>
        PublishRejected,

        /// <summary>The ref was fetched but its contents are not a distribution.</summary>
        TreeMalformed,
    }

    /// <summary>
    /// The operator-facing explanation. Deliberately says what to check next rather than restating
    /// the symptom.
    /// </summary>
    public static string Describe(Fault fault, ReleaseVersion? version = null, string? gitOutput = null)
    {
        var reference = version is null ? DistributionRefPrefixDisplay : GitReleaseRefs.DistributionRef(version);

        var message = fault switch
        {
            Fault.NamespaceNotAdvertised =>
                $"The repository advertises no refs under '{GitReleaseRefs.DistributionRefPrefix}'. Either no "
                + "release has been published yet, or this Git host does not advertise refs outside "
                + "refs/heads and refs/tags. Verify with: "
                + $"git ls-remote <repo> '{GitReleaseRefs.DistributionRefPrefix}*'. If the host genuinely "
                + "refuses the namespace, it is changed in one place - "
                + "ITAdmin.Deployment.GitReleaseRefs.DistributionRefPrefix.",

            Fault.ReleaseNotPublished =>
                $"The release tag exists but its prebuilt distribution ('{reference}') was not found. "
                + "Publishing the payload is a release-pipeline step; an annotated tag on its own is not "
                + "installable. Re-run the publish workflow for this release.",

            Fault.PublishRejected =>
                $"The distribution ref '{reference}' could not be created. Confirm the publishing "
                + "identity may write refs to this repository, and that the host permits the "
                + $"'{GitReleaseRefs.DistributionRefPrefix}' namespace. This is a publisher-side "
                + "problem; no server is affected until it is resolved.",

            Fault.TreeMalformed =>
                $"'{reference}' was fetched but does not contain a readable ITAdmin distribution. The ref "
                + "exists and points at something the publisher should not have produced - treat it as a "
                + "publish-pipeline fault and re-publish before installing.",

            _ => $"The distribution ref '{reference}' could not be used.",
        };

        return string.IsNullOrWhiteSpace(gitOutput)
            ? message
            : message + Environment.NewLine + "Git reported: " + gitOutput.Trim();
    }

    private const string DistributionRefPrefixDisplay = "refs/itadmin/dist/<version>";

    /// <summary>
    /// Whether any advertised ref lives in the distribution namespace. Distinguishes "this host
    /// does not do custom namespaces" from "this particular release was not published", which is
    /// the single most useful distinction during a first deployment against a new Git host.
    /// </summary>
    public static bool AdvertisesDistributionNamespace(IEnumerable<string> lsRemoteLines)
    {
        ArgumentNullException.ThrowIfNull(lsRemoteLines);

        foreach (var line in lsRemoteLines)
        {
            if (GitReleaseRefs.TryParseLsRemoteLine(line, out _, out var reference)
                && reference.StartsWith(GitReleaseRefs.DistributionRefPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a specific release's distribution ref is advertised.</summary>
    public static bool AdvertisesRelease(IEnumerable<string> lsRemoteLines, ReleaseVersion version)
    {
        ArgumentNullException.ThrowIfNull(lsRemoteLines);
        ArgumentNullException.ThrowIfNull(version);

        var expected = GitReleaseRefs.DistributionRef(version);

        foreach (var line in lsRemoteLines)
        {
            if (GitReleaseRefs.TryParseLsRemoteLine(line, out _, out var reference)
                && string.Equals(reference, expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
