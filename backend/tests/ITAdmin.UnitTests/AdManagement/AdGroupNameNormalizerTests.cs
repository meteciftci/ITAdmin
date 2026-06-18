using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdGroupNameNormalizerTests
{
    [Fact]
    public void BuildSamAccountNameSuggestion_NormalizesTurkishCharacters()
    {
        var result = AdGroupNameNormalizer.BuildSamAccountNameSuggestion("Şirket VPN");

        Assert.Equal("sirket.vpn", result);
    }

    [Fact]
    public void BuildSamAccountNameSuggestion_DoesNotTruncateToTwentyCharacters()
    {
        var technicalName = "very-long-group-technical-name-for-testing";
        var result = AdGroupNameNormalizer.BuildSamAccountNameSuggestion(technicalName);

        Assert.Equal(technicalName, result);
        Assert.True(result!.Length > 20);
    }

    [Fact]
    public void SamAccountNameMaxLength_IsNotUserLimit()
    {
        Assert.NotEqual(AdUserNameNormalizer.SamAccountNameMaxLength, AdGroupNameNormalizer.SamAccountNameMaxLength);
        Assert.Equal(64, AdGroupNameNormalizer.SamAccountNameMaxLength);
    }
}
