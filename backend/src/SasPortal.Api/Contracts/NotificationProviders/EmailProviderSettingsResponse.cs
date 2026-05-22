namespace SasPortal.Api.Contracts.NotificationProviders;

public sealed record EmailProviderSettingsResponse(
    string Channel,
    string ProviderKey,
    bool IsEnabled,
    string? DisplayName,
    string? Host,
    int Port,
    bool UseSsl,
    string? UserName,
    string? FromAddress,
    string? FromDisplayName,
    int TimeoutSeconds,
    bool HasPassword,
    DateTimeOffset? LastValidatedAt,
    string? LastValidationStatus,
    string? LastValidationMessage);
