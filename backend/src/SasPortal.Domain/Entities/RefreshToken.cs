using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid PortalUserId { get; set; }
    public PortalUser PortalUser { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsPersistent { get; set; }
    public DateTime LastUsedAt { get; set; }
}
