using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public sealed class NotificationTemplate : BaseEntity
{
    public string ModuleKey { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
