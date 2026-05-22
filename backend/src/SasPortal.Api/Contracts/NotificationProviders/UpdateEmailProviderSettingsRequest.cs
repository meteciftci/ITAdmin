namespace SasPortal.Api.Contracts.NotificationProviders;

public sealed class UpdateEmailProviderSettingsRequest
{
    public bool IsEnabled { get; set; }
    public string? DisplayName { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromDisplayName { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}
