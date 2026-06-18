namespace ITAdmin.Application.Common.AdManagement;

public sealed class AdManagementNotificationSettings
{
    public List<AdManagementNotificationRule> Rules { get; set; } = [];
}

public sealed class AdManagementNotificationRule
{
    public Guid Id { get; set; }

    public string EventKey { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public AdManagementNotificationRecipientSource? RecipientSource { get; set; }
}

public sealed class AdManagementNotificationRecipientSource
{
    public string Type { get; set; } = string.Empty;

    public string? Value { get; set; }
}

/// <summary>Legacy JSON shape for backward-compatible deserialization.</summary>
internal sealed class AdManagementNotificationSettingsJsonDto
{
    public AdManagementUserCreatedNotificationSettings? UserCreated { get; set; }

    public List<AdManagementNotificationRule>? Rules { get; set; }
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
