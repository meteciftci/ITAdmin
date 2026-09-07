namespace ITAdmin.Application.Common.Models.Notifications;

public sealed record SmsProviderSettingsResponse
{
    public required string Channel { get; init; }
    public required string ProviderKey { get; init; }
    public IReadOnlyList<string> AvailableProviders { get; init; } = [];
    public bool IsEnabled { get; init; }
    public string? DisplayName { get; init; }

    public string? Sender { get; init; }
    public int TimeoutSeconds { get; init; }
    public string TurkishCharacterMode { get; init; } = "Preserve";

    // Custom HTTP
    public string? EndpointUrl { get; init; }
    public string Method { get; init; } = "POST";
    public string ContentType { get; init; } = "application/json";
    public string AuthType { get; init; } = "None";
    public string? ApiKeyName { get; init; }
    public IReadOnlyList<NotificationKeyValuePair> Headers { get; init; } = [];
    public IReadOnlyList<NotificationKeyValuePair> QueryParameters { get; init; } = [];
    public string? BodyTemplate { get; init; }
    public IReadOnlyList<int> SuccessStatusCodes { get; init; } = [200];
    public string? SuccessBodyContains { get; init; }
    public bool HasBasicPassword { get; init; }
    public bool HasBearerToken { get; init; }
    public bool HasApiKey { get; init; }

    // Teknomart
    public string? TeknomartBaseUrl { get; init; }
    public string TeknomartDefaultSmsKind { get; init; } = "Single";
    public string? TeknomartSingleSmsTitle { get; init; }
    public int TeknomartEncoding { get; init; }
    public int TeknomartValidity { get; init; }
    public bool TeknomartCommercial { get; init; }
    public string? TeknomartPushWebhookUrl { get; init; }
    public bool HasTeknomartCredentials { get; init; }

    public DateTimeOffset? LastValidatedAt { get; init; }
    public string? LastValidationStatus { get; init; }
    public string? LastValidationMessage { get; init; }
}
