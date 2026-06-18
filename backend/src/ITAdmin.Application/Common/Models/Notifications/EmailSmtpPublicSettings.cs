namespace ITAdmin.Application.Common.Models.Notifications;

public sealed class EmailSmtpPublicSettings
{
    public bool IsEnabled { get; set; }
    public string? DisplayName { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? FromAddress { get; set; }
    public string? FromDisplayName { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}
