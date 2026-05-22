namespace SasPortal.Application.Common.Models.Notifications;

public sealed class SmsCustomHttpSecretSettings
{
    public string? BasicUserName { get; set; }
    public string? BasicPassword { get; set; }
    public string? BearerToken { get; set; }
    public string? ApiKeyValue { get; set; }
}
