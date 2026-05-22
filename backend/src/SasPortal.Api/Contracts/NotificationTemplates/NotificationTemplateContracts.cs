namespace SasPortal.Api.Contracts.NotificationTemplates;

public sealed class SaveNotificationTemplateRequest
{
    public string ModuleKey { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed record NotificationTemplateListItemResponse(
    Guid Id,
    string ModuleKey,
    string EventKey,
    string Channel,
    string Name,
    bool IsEnabled,
    DateTimeOffset? UpdatedAt);

public sealed record NotificationTemplateResponse(
    Guid Id,
    string ModuleKey,
    string EventKey,
    string Channel,
    string Name,
    bool IsEnabled,
    string? SubjectTemplate,
    string BodyTemplate,
    string? Description,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
