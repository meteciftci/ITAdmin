namespace ITAdmin.Application.Common.Models.Notifications;

/// <summary>
/// Update the SMS provider settings. <see cref="ProviderKey"/> selects which provider is active;
/// only the matching settings block is read.
/// </summary>
public sealed record UpdateSmsProviderSettingsRequest
{
    public string ProviderKey { get; init; } = "custom-http";
    public bool IsEnabled { get; init; }
    public string? DisplayName { get; init; }

    // Common
    public string? Sender { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public string TurkishCharacterMode { get; init; } = "Preserve";

    // Custom HTTP
    public string? EndpointUrl { get; init; }
    public string Method { get; init; } = "POST";
    public string ContentType { get; init; } = "application/json";
    public string AuthType { get; init; } = "None";
    public string? ApiKeyName { get; init; }
    public string? BasicUserName { get; init; }
    public string? BasicPassword { get; init; }
    public string? BearerToken { get; init; }
    public string? ApiKeyValue { get; init; }
    public IReadOnlyList<NotificationKeyValuePair> Headers { get; init; } = [];
    public IReadOnlyList<NotificationKeyValuePair> QueryParameters { get; init; } = [];
    public string? BodyTemplate { get; init; }
    public IReadOnlyList<int> SuccessStatusCodes { get; init; } = [200];
    public string? SuccessBodyContains { get; init; }

    // Teknomart
    public string? TeknomartBaseUrl { get; init; }
    public string TeknomartDefaultSmsKind { get; init; } = "Single";
    public string? TeknomartSingleSmsTitle { get; init; }
    public int TeknomartEncoding { get; init; }
    public int TeknomartValidity { get; init; }
    public bool TeknomartCommercial { get; init; }
    public string? TeknomartPushWebhookUrl { get; init; }
    public string? TeknomartUsername { get; init; }
    public string? TeknomartPassword { get; init; }

    // Actor
    public Guid? ActorUserId { get; init; }
    public string? ActorUserName { get; init; }
    public string? ActorIpAddress { get; init; }
    public string? ActorUserAgent { get; init; }
}
