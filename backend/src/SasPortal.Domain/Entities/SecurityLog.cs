using SasPortal.Domain.Common;
using SasPortal.Domain.Enums;

namespace SasPortal.Domain.Entities;

public class SecurityLog : BaseEntity
{
    public Guid? PortalUserId { get; set; }
    public string? UserName { get; set; }
    public SecurityEventType EventType { get; set; }
    public bool IsSuccess { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
