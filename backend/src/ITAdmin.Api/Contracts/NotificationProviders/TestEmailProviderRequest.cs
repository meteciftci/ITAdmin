namespace ITAdmin.Api.Contracts.NotificationProviders;

public sealed class TestEmailProviderRequest
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
