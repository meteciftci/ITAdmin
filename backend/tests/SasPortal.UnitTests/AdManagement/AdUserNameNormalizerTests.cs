using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdUserNameNormalizerTests
{
    [Fact]
    public void NormalizeUserName_ConvertsTurkishCharacters()
    {
        var result = AdUserNameNormalizer.NormalizeUserName("Çağrı", "IŞIK");

        Assert.Equal("cagri.isik", result);
    }

    [Fact]
    public void NormalizeUserName_CollapsesMultipleDotsAndSpaces()
    {
        var result = AdUserNameNormalizer.NormalizeUserName("  Ali   Veli  ", "  Kaya  ");

        Assert.Equal("ali.veli.kaya", result);
    }

    [Fact]
    public void NormalizeSamAccountName_RespectsMaxLength()
    {
        var result = AdUserNameNormalizer.NormalizeSamAccountName("verylongusernamethatexceedslimit");

        Assert.NotNull(result);
        Assert.True(result!.Length <= AdUserNameNormalizer.SamAccountNameMaxLength);
    }

    [Fact]
    public void BuildSamAccountNameWithSuffix_RespectsMaxLength()
    {
        var baseSam = AdUserNameNormalizer.NormalizeSamAccountName("verylongusernamethatexceeds");
        var result = AdUserNameNormalizer.BuildSamAccountNameWithSuffix(baseSam!, 12);

        Assert.True(result.Length <= AdUserNameNormalizer.SamAccountNameMaxLength);
        Assert.EndsWith("12", result);
    }

    [Fact]
    public void BuildUserPrincipalName_UsesDefaultSuffix()
    {
        var upn = AdUserNameNormalizer.BuildUserPrincipalName("cagri.isik", "Mugla.Bel.TR");

        Assert.Equal("cagri.isik@mugla.bel.tr", upn);
    }
}
