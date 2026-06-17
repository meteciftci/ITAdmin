using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdGroupSamAccountNameValidatorTests
{
    [Fact]
    public void IsValid_RejectsEmptySamAccountName()
    {
        var isValid = AdGroupSamAccountNameValidator.IsValid("  ", out var messageKey);

        Assert.False(isValid);
        Assert.Equal(AdManagementApiMessageKeys.Groups.SamAccountNameRequired, messageKey);
    }

    [Fact]
    public void IsValid_RejectsSamAccountNameLongerThanGroupLimit()
    {
        var value = new string('a', AdGroupNameNormalizer.SamAccountNameMaxLength + 1);

        var isValid = AdGroupSamAccountNameValidator.IsValid(value, out var messageKey);

        Assert.False(isValid);
        Assert.Equal(AdManagementApiMessageKeys.Groups.SamAccountNameTooLong, messageKey);
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
