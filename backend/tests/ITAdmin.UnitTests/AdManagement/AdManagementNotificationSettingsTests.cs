using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdManagementNotificationSettingsTests
{
    [Fact]
    public void Deserialize_NullJson_ReturnsEmptyRules()
    {
        var settings = AdManagementNotificationSettingsSerializer.Deserialize(null);

        Assert.Empty(settings.Rules);
    }

    [Fact]
    public void Deserialize_LegacyUserCreated_MigratesToRules()
    {
        var legacyJson =
            """
            {
              "userCreated": {
                "isEnabled": true,
                "smsEnabled": true,
                "emailEnabled": false,
                "smsRecipientSource": {
                  "type": "MappedAttribute",
                  "value": "mapping-id"
                }
              }
            }
            """;

        var settings = AdManagementNotificationSettingsSerializer.Deserialize(legacyJson);

        Assert.Single(settings.Rules);
        Assert.Equal(AdManagementNotificationEventKeys.UserCreated, settings.Rules[0].EventKey);
        Assert.Equal(NotificationChannels.Sms, settings.Rules[0].Channel);
        Assert.True(settings.Rules[0].IsEnabled);
    }

    [Fact]
    public void SerializeDeserialize_PreservesRules()
    {
        var original = new AdManagementNotificationSettings
        {
            Rules =
            [
                new AdManagementNotificationRule
                {
                    Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    EventKey = AdManagementNotificationEventKeys.UserUnlocked,
                    Channel = NotificationChannels.Sms,
                    IsEnabled = true,
                    RecipientSource = new AdManagementNotificationRecipientSource
                    {
                        Type = AdManagementNotificationRecipientSourceTypes.AdAttribute,
                        Value = "mobile",
                    },
                },
            ],
        };

        var json = AdManagementNotificationSettingsSerializer.Serialize(original);
        var restored = AdManagementNotificationSettingsSerializer.Deserialize(json);

        Assert.Single(restored.Rules);
        Assert.Equal(AdManagementNotificationEventKeys.UserUnlocked, restored.Rules[0].EventKey);
        Assert.Equal("mobile", restored.Rules[0].RecipientSource?.Value);
    }

    [Fact]
    public void Validate_DuplicateEventChannel_ReturnsError()
    {
        var settings = new AdManagementNotificationSettings
        {
            Rules =
            [
                CreateRule(AdManagementNotificationEventKeys.UserCreated, NotificationChannels.Sms),
                CreateRule(AdManagementNotificationEventKeys.UserCreated, NotificationChannels.Sms),
            ],
        };

        var error = AdManagementNotificationSettingsValidator.Validate(settings);

        Assert.NotNull(error);
        Assert.Equal(AdManagementApiMessageKeys.NotificationSettings.DuplicateRule, error);
    }

    [Fact]
    public void Validate_MissingRecipient_ReturnsError()
    {
        var settings = new AdManagementNotificationSettings
        {
            Rules =
            [
                new AdManagementNotificationRule
                {
                    Id = Guid.NewGuid(),
                    EventKey = AdManagementNotificationEventKeys.UserEnabled,
                    Channel = NotificationChannels.Email,
                    IsEnabled = true,
                    RecipientSource = null,
                },
            ],
        };

        var error = AdManagementNotificationSettingsValidator.Validate(settings);

        Assert.Equal(AdManagementApiMessageKeys.NotificationSettings.RecipientSourceRequired, error);
    }

    private static AdManagementNotificationRule CreateRule(string eventKey, string channel)
    {
        var isSms = string.Equals(channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase);
        return new AdManagementNotificationRule
        {
            Id = Guid.NewGuid(),
            EventKey = eventKey,
            Channel = channel,
            IsEnabled = true,
            RecipientSource = new AdManagementNotificationRecipientSource
            {
                Type = isSms
                    ? AdManagementNotificationRecipientSourceTypes.MappedAttribute
                    : AdManagementNotificationRecipientSourceTypes.UserPrincipalName,
                Value = isSms ? "mapping-id" : null,
            },
        };
    }
}
