namespace SasPortal.Api.Contracts.NotificationProviders;

public sealed record NotificationProviderOperationResponse(
    string Message,
    SmsProviderSettingsResponse? SmsSettings = null,
    EmailProviderSettingsResponse? EmailSettings = null,
    string? ProviderSummary = null);
