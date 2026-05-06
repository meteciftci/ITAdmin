using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public class SecurityLog : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
