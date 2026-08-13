using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("2.0.0", 2, 0, 0, null)]
    [InlineData("0.1.0", 0, 1, 0, null)]
    [InlineData("10.20.30", 10, 20, 30, null)]
    [InlineData("v2.0.0", 2, 0, 0, null)]
    [InlineData("2.0.0-rc.1", 2, 0, 0, "rc.1")]
    [InlineData("2.0.0-alpha", 2, 0, 0, "alpha")]
    public void TryParse_AcceptsSupportedVersions(string input, int major, int minor, int patch, string? preRelease)
    {
        Assert.True(ReleaseVersion.TryParse(input, out var version));
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(preRelease, version.PreRelease);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2.0")]
    [InlineData("2.0.0.0")]
    [InlineData("2.0.x")]
    [InlineData("two.zero.zero")]
    [InlineData("2.0.0-")]
    [InlineData("-1.0.0")]
    [InlineData("2.0.0 ; rm -rf")]
    [InlineData("2.0.0/../../etc")]
    [InlineData("01.0.0")]
    [InlineData("+2.0.0")]
    public void TryParse_RejectsMalformedOrUnsafeVersions(string? input) =>
        Assert.False(ReleaseVersion.TryParse(input, out _));

    [Fact]
    public void Parse_RejectedVersion_ThrowsFormatException() =>
        Assert.Throws<FormatException>(() => ReleaseVersion.Parse("not-a-version"));

    [Theory]
    [InlineData("2.0.0", "2.0.0")]
    [InlineData("v2.0.0", "2.0.0")]
    [InlineData("2.0.0-rc.1", "2.0.0-rc.1")]
    public void ToString_RoundTripsToACanonicalPathSafeSegment(string input, string expected)
    {
        var version = ReleaseVersion.Parse(input);

        Assert.Equal(expected, version.ToString());
        // A release version becomes a directory name, so it must contain no separators.
        Assert.DoesNotContain('\\', version.ToString());
        Assert.DoesNotContain('/', version.ToString());
    }

    [Theory]
    [InlineData("2.0.1", "2.0.0")]
    [InlineData("2.1.0", "2.0.9")]
    [InlineData("3.0.0", "2.99.99")]
    public void CompareTo_OrdersByNumericPrecedence(string greater, string lesser) =>
        Assert.True(ReleaseVersion.Parse(greater).CompareTo(ReleaseVersion.Parse(lesser)) > 0);

    [Fact]
    public void CompareTo_StableReleaseOutranksItsOwnPreRelease() =>
        Assert.True(ReleaseVersion.Parse("2.0.0").CompareTo(ReleaseVersion.Parse("2.0.0-rc.1")) > 0);

    [Fact]
    public void Equals_SameVersionParsedDifferently_IsEqual() =>
        Assert.Equal(ReleaseVersion.Parse("v2.0.0"), ReleaseVersion.Parse("2.0.0"));

    [Fact]
    public void CompareTo_Null_SortsAfter() =>
        Assert.True(ReleaseVersion.Parse("1.0.0").CompareTo(null) > 0);
}
