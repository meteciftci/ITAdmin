using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdDefaultUpnSuffixNormalizerTests
{
    [Theory]
    [InlineData("@Mugla.Bel.TR", "mugla.bel.tr")]
    [InlineData("  DOMAIN.LOCAL  ", "domain.local")]
    [InlineData("sub.domain.local", "sub.domain.local")]
    public void Normalize_StripsAtPrefixAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, AdDefaultUpnSuffixNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("mugla.bel.tr", true)]
    [InlineData("domain.local", true)]
    [InlineData("sub.domain.local", true)]
    [InlineData("", false)]
    [InlineData("invalid suffix", false)]
    [InlineData("-bad.com", false)]
    public void IsValidFormat_ValidatesDomainSuffix(string value, bool expected)
    {
        Assert.Equal(expected, AdDefaultUpnSuffixNormalizer.IsValidFormat(value));
    }
}
