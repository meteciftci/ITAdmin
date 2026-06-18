namespace ITAdmin.Api.Contracts.NotificationProviders;

public sealed record NotificationKeyValuePairResponse(string Key, string Value);

public sealed record SmsProviderSettingsResponse(
    string Channel,
    string ProviderKey,
    bool IsEnabled,
    string? DisplayName,
    string? Sender,
    int TimeoutSeconds,
    string? EndpointUrl,
    string Method,
    string ContentType,
    string AuthType,
    string? ApiKeyName,
    IReadOnlyList<NotificationKeyValuePairResponse> Headers,
    IReadOnlyList<NotificationKeyValuePairResponse> QueryParameters,
    string? BodyTemplate,
    IReadOnlyList<int> SuccessStatusCodes,
    string? SuccessBodyContains,
    string TurkishCharacterMode,
    bool HasBasicPassword,
    bool HasBearerToken,
    bool HasApiKey,
    DateTimeOffset? LastValidatedAt,
    string? LastValidationStatus,
    string? LastValidationMessage);
