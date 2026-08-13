using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// Which release a host installs is decided entirely by these rules, so they are pinned in detail.
/// A regression here does not produce an obvious failure - it produces a machine quietly running
/// something nobody released.
/// </summary>
public sealed class ReleaseTagResolverTests
{
    private const string CommitA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CommitB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string CommitC = "cccccccccccccccccccccccccccccccccccccccc";
    private const string TagObjectId = "1111111111111111111111111111111111111111";

    /// <summary>An annotated tag: an unpeeled row naming the tag object, plus a peeled row.</summary>
    private static string[] Annotated(string tag, string tagObjectId, string commit) =>
    [
        $"{tagObjectId}\trefs/tags/{tag}",
        $"{commit}\trefs/tags/{tag}^{{}}",
    ];

    /// <summary>A lightweight tag: one row, pointing straight at a commit. No peel line.</summary>
    private static string[] Lightweight(string tag, string commit) =>
    [
        $"{commit}\trefs/tags/{tag}",
    ];

    [Fact]
    public void Resolve_PicksTheHighestAnnotatedStableTag()
    {
        string[] lines =
        [
            .. Annotated("v1.9.0", TagObjectId, CommitA),
            .. Annotated("v2.0.0", TagObjectId, CommitB),
            .. Annotated("v1.10.0", TagObjectId, CommitC),
        ];

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Stable);

        Assert.True(resolution.IsResolved);
        Assert.Equal("2.0.0", resolution.Selected.Version.ToString());
        Assert.Equal(CommitB, resolution.Selected.SourceCommit);
    }

    [Fact]
    public void Resolve_OrdersNumericallyNotLexically()
    {
        // "v1.9.0" sorts after "v1.10.0" as text. Getting this wrong installs an older release.
        string[] lines =
        [
            .. Annotated("v1.9.0", TagObjectId, CommitA),
            .. Annotated("v1.10.0", TagObjectId, CommitB),
        ];

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Stable);

        Assert.Equal("1.10.0", resolution.Selected!.Version.ToString());
    }

    [Fact]
    public void Resolve_RecordsThePeeledCommit_NotTheTagObject()
    {
        // The unpeeled row of an annotated tag names the tag object. Pinning it would record an
        // object that is not a commit and could never match a payload's sourceCommit.
        var resolution = ReleaseTagResolver.Resolve(
            Annotated("v1.0.0", TagObjectId, CommitA),
            ReleaseChannel.Stable);

        Assert.Equal(CommitA, resolution.Selected!.SourceCommit);
        Assert.NotEqual(TagObjectId, resolution.Selected.SourceCommit);
    }

    [Fact]
    public void Resolve_RejectsLightweightTags()
    {
        string[] lines =
        [
            .. Annotated("v1.0.0", TagObjectId, CommitA),
            .. Lightweight("v9.9.9", CommitB),
        ];

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Stable);

        Assert.Equal("1.0.0", resolution.Selected!.Version.ToString());
        Assert.Contains(
            resolution.Rejected,
            rejection => rejection.TagName == "v9.9.9" && rejection.Reason == ReleaseTagRejection.Lightweight);
    }

    [Fact]
    public void Resolve_OnlyLightweightTagsAvailable_ResolvesNothing()
    {
        var resolution = ReleaseTagResolver.Resolve(Lightweight("v1.0.0", CommitA), ReleaseChannel.Stable);

        Assert.False(resolution.IsResolved);
        Assert.Contains("annotated", resolution.DescribeFailure(ReleaseChannel.Stable), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_StableChannel_RejectsPreReleases()
    {
        string[] lines =
        [
            .. Annotated("v1.0.0", TagObjectId, CommitA),
            .. Annotated("v2.0.0-rc.1", TagObjectId, CommitB),
        ];

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Stable);

        Assert.Equal("1.0.0", resolution.Selected!.Version.ToString());
        Assert.Contains(
            resolution.Rejected,
            rejection => rejection.Reason == ReleaseTagRejection.PreRelease);
    }

    [Fact]
    public void Resolve_PreviewChannel_AcceptsPreReleases()
    {
        string[] lines =
        [
            .. Annotated("v1.0.0", TagObjectId, CommitA),
            .. Annotated("v2.0.0-rc.1", TagObjectId, CommitB),
        ];

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Preview);

        Assert.Equal("2.0.0-rc.1", resolution.Selected!.Version.ToString());
    }

    [Fact]
    public void Resolve_StableOutranksPreReleaseOfTheSameNumber()
    {
        string[] lines =
        [
            .. Annotated("v2.0.0-rc.1", TagObjectId, CommitA),
            .. Annotated("v2.0.0", TagObjectId, CommitB),
        ];

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Preview);

        Assert.Equal("2.0.0", resolution.Selected!.Version.ToString());
        Assert.Equal(CommitB, resolution.Selected.SourceCommit);
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("release-candidate")]
    [InlineData("v1.0")]
    [InlineData("v1.0.0.0")]
    public void Resolve_RejectsTagsThatAreNotReleaseVersions(string tagName)
    {
        var resolution = ReleaseTagResolver.Resolve(
            Annotated(tagName, TagObjectId, CommitA),
            ReleaseChannel.Stable);

        Assert.False(resolution.IsResolved);
        Assert.Contains(
            resolution.Rejected,
            rejection => rejection.Reason == ReleaseTagRejection.NotAVersion);
    }

    [Fact]
    public void Resolve_IgnoresBranchAndOtherRefs()
    {
        string[] lines =
        [
            $"{CommitB}\trefs/heads/main",
            $"{CommitC}\trefs/itadmin/dist/1.0.0",
            .. Annotated("v1.0.0", TagObjectId, CommitA),
        ];

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Stable);

        Assert.Equal("1.0.0", resolution.Selected!.Version.ToString());
        Assert.Single(resolution.Candidates);
    }

    [Fact]
    public void Resolve_EmptyRemote_ExplainsWhatToDo()
    {
        var resolution = ReleaseTagResolver.Resolve([], ReleaseChannel.Stable);

        Assert.False(resolution.IsResolved);
        Assert.Empty(resolution.Rejected);
        Assert.Contains("git tag -a", resolution.DescribeFailure(ReleaseChannel.Stable), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ToleratesNoiseInLsRemoteOutput()
    {
        string[] lines =
        [
            "warning: something informational from git",
            string.Empty,
            "   ",
            .. Annotated("v1.0.0", TagObjectId, CommitA),
        ];

        Assert.True(ReleaseTagResolver.Resolve(lines, ReleaseChannel.Stable).IsResolved);
    }

    [Fact]
    public void ResolveExact_PinsTheRequestedVersion()
    {
        string[] lines =
        [
            .. Annotated("v1.0.0", TagObjectId, CommitA),
            .. Annotated("v2.0.0", TagObjectId, CommitB),
        ];

        var resolution = ReleaseTagResolver.ResolveExact(
            lines,
            ReleaseVersion.Parse("1.0.0"),
            ReleaseChannel.Stable);

        Assert.Equal("1.0.0", resolution.Selected!.Version.ToString());
        Assert.Equal(CommitA, resolution.Selected.SourceCommit);
    }

    [Fact]
    public void ResolveExact_CannotPinALightweightTag()
    {
        var resolution = ReleaseTagResolver.ResolveExact(
            Lightweight("v1.0.0", CommitA),
            ReleaseVersion.Parse("1.0.0"),
            ReleaseChannel.Stable);

        Assert.False(resolution.IsResolved);
    }

    [Fact]
    public void ResolveExact_CannotPinAPreReleaseOnTheStableChannel()
    {
        // Pinning must not be a way around the channel: a stable host stays on stable versions
        // even when an administrator names a pre-release explicitly.
        var resolution = ReleaseTagResolver.ResolveExact(
            Annotated("v2.0.0-rc.1", TagObjectId, CommitA),
            ReleaseVersion.Parse("2.0.0-rc.1"),
            ReleaseChannel.Stable);

        Assert.False(resolution.IsResolved);
    }

    [Fact]
    public void DistributionRef_IsOutsideBranchesAndTags()
    {
        // A payload that lived under refs/heads or refs/tags would be downloaded by every ordinary
        // clone and would show up in source history.
        var reference = GitReleaseRefs.DistributionRef(ReleaseVersion.Parse("2.1.0"));

        Assert.Equal("refs/itadmin/dist/2.1.0", reference);
        Assert.DoesNotContain("refs/heads/", reference, StringComparison.Ordinal);
        Assert.DoesNotContain("refs/tags/", reference, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceTagRef_UsesTheVPrefixedTagName() =>
        Assert.Equal("refs/tags/v2.1.0", GitReleaseRefs.SourceTagRef(ReleaseVersion.Parse("2.1.0")));

    [Theory]
    [InlineData("not-an-oid\trefs/tags/v1.0.0")]
    [InlineData("")]
    [InlineData("aaaa\trefs/tags/v1.0.0")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void TryParseLsRemoteLine_RejectsMalformedLines(string line) =>
        Assert.False(GitReleaseRefs.TryParseLsRemoteLine(line, out _, out _));

    [Fact]
    public void TryParseLsRemoteLine_AcceptsSha256ObjectIds()
    {
        // Repositories using the SHA-256 object format advertise 64-character ids.
        var line = new string('a', 64) + "\trefs/tags/v1.0.0";

        Assert.True(GitReleaseRefs.TryParseLsRemoteLine(line, out var objectId, out var reference));
        Assert.Equal(new string('a', 64), objectId);
        Assert.Equal("refs/tags/v1.0.0", reference);
    }
}
