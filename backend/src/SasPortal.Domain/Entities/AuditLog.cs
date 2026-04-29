using SasPortal.Domain.Common;
using SasPortal.Domain.Enums;

namespace SasPortal.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? PortalUserId { get; set; }
    public string? UserName { get; set; }
    public AuditActionType Action { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
