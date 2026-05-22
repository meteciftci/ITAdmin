namespace SasPortal.Application.Common.Models.Notifications;

public sealed record NotificationProviderOperationResult(
    bool IsSuccess,
    string Message,
    SmsProviderSettingsResponse? SmsSettings = null,
    EmailProviderSettingsResponse? EmailSettings = null,
    string? ProviderSummary = null);
