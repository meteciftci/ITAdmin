namespace ITAdmin.Api.Contracts.NotificationProviders;

public sealed class UpdateSmsProviderSettingsRequest
{
    public string ProviderKey { get; set; } = "custom-http";
    public bool IsEnabled { get; set; }
    public string? DisplayName { get; set; }

    public string? Sender { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string TurkishCharacterMode { get; set; } = "Preserve";

    // Custom HTTP
    public string? EndpointUrl { get; set; }
    public string Method { get; set; } = "POST";
    public string ContentType { get; set; } = "application/json";
    public string AuthType { get; set; } = "None";
    public string? ApiKeyName { get; set; }
    public string? BasicUserName { get; set; }
    public string? BasicPassword { get; set; }
    public string? BearerToken { get; set; }
    public string? ApiKeyValue { get; set; }
    public List<NotificationKeyValuePairRequest> Headers { get; set; } = [];
    public List<NotificationKeyValuePairRequest> QueryParameters { get; set; } = [];
    public string? BodyTemplate { get; set; }
    public List<int> SuccessStatusCodes { get; set; } = [200];
    public string? SuccessBodyContains { get; set; }

    // Teknomart
    public string? TeknomartBaseUrl { get; set; }
    public string TeknomartDefaultSmsKind { get; set; } = "Single";
    public string? TeknomartSingleSmsTitle { get; set; }
    public int TeknomartEncoding { get; set; }
    public int TeknomartValidity { get; set; }
    public bool TeknomartCommercial { get; set; }
    public string? TeknomartPushWebhookUrl { get; set; }
    public string? TeknomartUsername { get; set; }
    public string? TeknomartPassword { get; set; }
}

public sealed class NotificationKeyValuePairRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
