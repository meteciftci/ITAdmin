namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdManagementNotificationRecipientSourceRequest
{
    public string Type { get; init; } = string.Empty;
    public string? Value { get; init; }
}

public sealed record AdManagementUserCreatedNotificationSettingsRequest
{
    public bool IsEnabled { get; init; }
    public bool SmsEnabled { get; init; }
    public bool EmailEnabled { get; init; }
    public AdManagementNotificationRecipientSourceRequest? SmsRecipientSource { get; init; }
    public AdManagementNotificationRecipientSourceRequest? EmailRecipientSource { get; init; }
}

public sealed record AdManagementNotificationSettingsRequest
{
    public AdManagementUserCreatedNotificationSettingsRequest UserCreated { get; init; } = new();
}

public sealed record AdManagementNotificationRecipientSourceResponse
{
    public string Type { get; init; } = string.Empty;
    public string? Value { get; init; }
}

public sealed record AdManagementUserCreatedNotificationSettingsResponse
{
    public bool IsEnabled { get; init; }
    public bool SmsEnabled { get; init; }
    public bool EmailEnabled { get; init; }
    public AdManagementNotificationRecipientSourceResponse? SmsRecipientSource { get; init; }
    public AdManagementNotificationRecipientSourceResponse? EmailRecipientSource { get; init; }
}

public sealed record AdManagementNotificationSettingsResponse
{
    public AdManagementUserCreatedNotificationSettingsResponse UserCreated { get; init; } = new();
}
