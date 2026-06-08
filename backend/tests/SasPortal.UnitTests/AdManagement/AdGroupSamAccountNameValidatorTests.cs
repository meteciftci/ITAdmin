using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdGroupSamAccountNameValidatorTests
{
    [Fact]
    public void IsValid_RejectsEmptySamAccountName()
    {
        var isValid = AdGroupSamAccountNameValidator.IsValid("  ", out var message);

        Assert.False(isValid);
        Assert.Equal(AdGroupSamAccountNameValidator.EmptyMessage, message);
    }

    [Fact]
    public void IsValid_RejectsSamAccountNameLongerThanGroupLimit()
    {
        var value = new string('a', AdGroupNameNormalizer.SamAccountNameMaxLength + 1);

        var isValid = AdGroupSamAccountNameValidator.IsValid(value, out var message);

        Assert.False(isValid);
        Assert.Equal(AdGroupSamAccountNameValidator.TooLongMessage, message);
    }

    [Fact]
    public void IsValid_DoesNotUseUserTwentyCharacterLimit()
    {
        var value = new string('a', 25);

        var isValid = AdGroupSamAccountNameValidator.IsValid(value, out _);

        Assert.True(isValid);
        Assert.False(AdSamAccountNameValidator.IsValid(value, out _));
    }
}
