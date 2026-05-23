using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdManagementNotificationSettingsTests
{
    [Fact]
    public void Deserialize_NullJson_ReturnsDisabledDefaults()
    {
        var settings = AdManagementNotificationSettingsSerializer.Deserialize(null);

        Assert.False(settings.UserCreated.IsEnabled);
        Assert.False(settings.UserCreated.SmsEnabled);
        Assert.False(settings.UserCreated.EmailEnabled);
    }

    [Fact]
    public void SerializeDeserialize_PreservesUserCreatedSettings()
    {
        var original = new AdManagementNotificationSettings
        {
            UserCreated = new AdManagementUserCreatedNotificationSettings
            {
                IsEnabled = true,
                SmsEnabled = true,
                EmailEnabled = false,
                SmsRecipientSource = new AdManagementNotificationRecipientSource
                {
                    Type = AdManagementNotificationRecipientSourceTypes.MappedAttribute,
                    Value = "mapping-id",
                },
            },
        };

        var json = AdManagementNotificationSettingsSerializer.Serialize(original);
        var restored = AdManagementNotificationSettingsSerializer.Deserialize(json);

        Assert.True(restored.UserCreated.IsEnabled);
        Assert.True(restored.UserCreated.SmsEnabled);
        Assert.False(restored.UserCreated.EmailEnabled);
        Assert.Equal(
            AdManagementNotificationRecipientSourceTypes.MappedAttribute,
            restored.UserCreated.SmsRecipientSource?.Type);
    }

    [Fact]
    public void Validate_EnabledWithoutChannel_ReturnsError()
    {
        var settings = new AdManagementNotificationSettings
        {
            UserCreated = new AdManagementUserCreatedNotificationSettings
            {
                IsEnabled = true,
                SmsEnabled = false,
                EmailEnabled = false,
            },
        };

        var error = AdManagementNotificationSettingsValidator.Validate(settings);

        Assert.NotNull(error);
    }
}
