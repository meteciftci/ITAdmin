namespace SasPortal.Application.Common.AdManagement;

public static class AdManagementNotificationSettingsValidator
{
    public static string? Validate(AdManagementNotificationSettings settings)
    {
        var userCreated = settings.UserCreated ?? AdManagementUserCreatedNotificationSettings.Disabled;

        if (!userCreated.IsEnabled)
        {
            return null;
        }

        if (!userCreated.SmsEnabled && !userCreated.EmailEnabled)
        {
            return "Kullanıcı oluşturuldu bildirimi için en az bir kanal seçilmelidir.";
        }

        if (userCreated.SmsEnabled)
        {
            var smsError = ValidateRecipientSource(
                userCreated.SmsRecipientSource,
                smsChannel: true);
            if (smsError is not null)
            {
                return smsError;
            }
        }

        if (userCreated.EmailEnabled)
        {
            var emailError = ValidateRecipientSource(
                userCreated.EmailRecipientSource,
                smsChannel: false);
            if (emailError is not null)
            {
                return emailError;
            }
        }

        return null;
    }

    private static string? ValidateRecipientSource(
        AdManagementNotificationRecipientSource? source,
        bool smsChannel)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.Type))
        {
            return smsChannel
                ? "SMS alıcı kaynağı zorunludur."
                : "E-posta alıcı kaynağı zorunludur.";
        }

        var type = source.Type.Trim();

        if (smsChannel)
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
