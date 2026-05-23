namespace SasPortal.Api.Contracts.NotificationTemplates;

public sealed record UpdateNotificationTemplateStatusRequest
{
    public bool IsEnabled { get; init; }
}
