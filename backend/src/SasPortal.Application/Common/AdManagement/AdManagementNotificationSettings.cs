namespace SasPortal.Application.Common.AdManagement;

public sealed class AdManagementNotificationSettings
{
    public AdManagementUserCreatedNotificationSettings UserCreated { get; set; } = AdManagementUserCreatedNotificationSettings.Disabled;
}

public sealed class AdManagementUserCreatedNotificationSettings
{
    public bool IsEnabled { get; set; }

    public bool SmsEnabled { get; set; }

    public bool EmailEnabled { get; set; }

    public AdManagementNotificationRecipientSource? SmsRecipientSource { get; set; }

    public AdManagementNotificationRecipientSource? EmailRecipientSource { get; set; }

    public static AdManagementUserCreatedNotificationSettings Disabled { get; } = new();
}

public sealed class AdManagementNotificationRecipientSource
{
    public string Type { get; set; } = string.Empty;

    public string? Value { get; set; }
}
