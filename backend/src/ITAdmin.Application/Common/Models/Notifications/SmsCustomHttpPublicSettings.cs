namespace ITAdmin.Application.Common.Models.Notifications;

public sealed class SmsCustomHttpPublicSettings
{
    public bool IsEnabled { get; set; }
    public string? DisplayName { get; set; }
    public string? Sender { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string? EndpointUrl { get; set; }
    public string Method { get; set; } = "POST";
    public string ContentType { get; set; } = "application/json";
    public string AuthType { get; set; } = "None";
    public string? ApiKeyName { get; set; }
    public IReadOnlyList<NotificationKeyValuePair> Headers { get; set; } = [];
    public IReadOnlyList<NotificationKeyValuePair> QueryParameters { get; set; } = [];
    public string? BodyTemplate { get; set; }
    public IReadOnlyList<int> SuccessStatusCodes { get; set; } = [200];
    public string? SuccessBodyContains { get; set; }
    public string TurkishCharacterMode { get; set; } = "Preserve";
}
