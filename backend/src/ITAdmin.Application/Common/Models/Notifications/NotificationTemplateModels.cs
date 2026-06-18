namespace ITAdmin.Application.Common.Models.Notifications;

public sealed record NotificationTemplateModel(
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

public sealed record NotificationTemplateListItem(
    Guid Id,
    string ModuleKey,
    string EventKey,
    string Channel,
    string Name,
    bool IsEnabled,
    DateTimeOffset? UpdatedAt);

public sealed record CreateNotificationTemplateRequest(
    string ModuleKey,
    string EventKey,
    string Channel,
    string Name,
    bool IsEnabled,
    string? SubjectTemplate,
    string BodyTemplate,
    string? Description,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateNotificationTemplateRequest(
    string ModuleKey,
    string EventKey,
    string Channel,
    string Name,
    bool IsEnabled,
    string? SubjectTemplate,
    string BodyTemplate,
    string? Description,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record NotificationTemplateListQuery(
    string? ModuleKey,
    string? EventKey,
    string? Channel);

public sealed record UpdateNotificationTemplateStatusRequest(
    bool IsEnabled,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record NotificationTemplateOperationResult(
    bool IsSuccess,
    string Message,
    NotificationTemplateModel? Template = null);
