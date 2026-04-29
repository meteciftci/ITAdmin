using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public class PortalUser : SoftDeletableEntity
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public ICollection<PortalUserRole> UserRoles { get; set; } = new List<PortalUserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
