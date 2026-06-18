using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdManagementNotificationSettingsSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static AdManagementNotificationSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateDefault();
        }

        try
        {
            var dto = JsonSerializer.Deserialize<AdManagementNotificationSettingsJsonDto>(json, JsonOptions);
            if (dto is null)
            {
                return CreateDefault();
            }

            if (dto.Rules is { Count: > 0 })
            {
                return Normalize(new AdManagementNotificationSettings { Rules = dto.Rules });
            }

            return Normalize(MigrateFromLegacyUserCreated(dto.UserCreated));
        }
        catch (Exception)
        {
            // Invalid or legacy notification settings JSON falls back to safe defaults.
            return CreateDefault();
        }
    }

    public static string Serialize(AdManagementNotificationSettings settings) =>
        JsonSerializer.Serialize(Normalize(settings), JsonOptions);

    public static AdManagementNotificationSettings CreateDefault() =>
        new() { Rules = [] };

    private static AdManagementNotificationSettings MigrateFromLegacyUserCreated(
        AdManagementUserCreatedNotificationSettings? userCreated)
    {
        var rules = new List<AdManagementNotificationRule>();
        if (userCreated is null || !userCreated.IsEnabled)
        {
            return new AdManagementNotificationSettings { Rules = rules };
        }

        if (userCreated.SmsEnabled)
        {
            rules.Add(CreateLegacyRule(
                AdManagementNotificationEventKeys.UserCreated,
                NotificationChannels.Sms,
                userCreated.SmsRecipientSource));
        }

        if (userCreated.EmailEnabled)
        {
            rules.Add(CreateLegacyRule(
                AdManagementNotificationEventKeys.UserCreated,
                NotificationChannels.Email,
                userCreated.EmailRecipientSource));
        }

        return new AdManagementNotificationSettings { Rules = rules };
    }

    private static AdManagementNotificationRule CreateLegacyRule(
        string eventKey,
        string channel,
        AdManagementNotificationRecipientSource? recipientSource) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventKey = eventKey,
            Channel = channel,
            IsEnabled = true,
            RecipientSource = recipientSource is null
                ? null
                : new AdManagementNotificationRecipientSource
                {
                    Type = recipientSource.Type.Trim(),
                    Value = string.IsNullOrWhiteSpace(recipientSource.Value)
                        ? null
                        : recipientSource.Value.Trim(),
                },
        };

    private static AdManagementNotificationSettings Normalize(AdManagementNotificationSettings settings)
    {
        settings.Rules ??= [];

        foreach (var rule in settings.Rules)
        {
            if (rule.Id == Guid.Empty)
            {
                rule.Id = Guid.NewGuid();
            }

            rule.EventKey = rule.EventKey.Trim();
            rule.Channel = rule.Channel.Trim();

            if (rule.RecipientSource is not null)
            {
                rule.RecipientSource.Type = rule.RecipientSource.Type.Trim();
                rule.RecipientSource.Value = string.IsNullOrWhiteSpace(rule.RecipientSource.Value)
                    ? null
                    : rule.RecipientSource.Value.Trim();
            }
        }

        return settings;
    }
}
