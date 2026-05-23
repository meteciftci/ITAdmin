using SasPortal.Application.Common.Constants;

namespace SasPortal.Application.Common.AdManagement;

public static class AdManagementNotificationSettingsValidator
{
    public static string? Validate(AdManagementNotificationSettings settings)
    {
        var rules = settings.Rules ?? [];
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            var eventKey = rule.EventKey?.Trim() ?? string.Empty;
            if (!AdManagementNotificationEventKeys.All.Contains(eventKey))
            {
                return "Bildirim olayı geçersiz.";
            }

            var channel = rule.Channel?.Trim() ?? string.Empty;
            if (!string.Equals(channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
            {
                return "Bildirim kanalı geçersiz.";
            }

            var duplicateKey = $"{eventKey}|{channel}";
            if (!seenKeys.Add(duplicateKey))
            {
                return "Aynı olay ve kanal için zaten bildirim kuralı var.";
            }
        }

        foreach (var rule in rules)
        {
            var channel = rule.Channel.Trim();
            var recipientError = ValidateRecipientSource(rule.RecipientSource, channel);
            if (recipientError is not null)
            {
                return recipientError;
            }
        }

        return null;
    }

    private static string? ValidateRecipientSource(
        AdManagementNotificationRecipientSource? source,
        string channel)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.Type))
        {
            return "Alıcı kaynağı zorunludur.";
        }

        var type = source.Type.Trim();
        var isSms = string.Equals(channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase);

        if (isSms)
        {
            if (type is not AdManagementNotificationRecipientSourceTypes.MappedAttribute
                and not AdManagementNotificationRecipientSourceTypes.AdAttribute)
            {
                return "SMS alıcı kaynağı geçersiz.";
            }
        }
        else if (type is not AdManagementNotificationRecipientSourceTypes.MappedAttribute
                 and not AdManagementNotificationRecipientSourceTypes.AdAttribute
                 and not AdManagementNotificationRecipientSourceTypes.UserPrincipalName
                 and not AdManagementNotificationRecipientSourceTypes.MailAttribute)
        {
            return "E-posta alıcı kaynağı geçersiz.";
        }

        if (type is AdManagementNotificationRecipientSourceTypes.MappedAttribute
            or AdManagementNotificationRecipientSourceTypes.AdAttribute)
        {
            if (string.IsNullOrWhiteSpace(source.Value))
            {
                return "Alıcı kaynağı değeri zorunludur.";
            }
        }

        return null;
    }
}
