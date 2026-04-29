using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public class PortalRole : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<PortalUserRole> UserRoles { get; set; } = new List<PortalUserRole>();
    public ICollection<PortalRolePermission> RolePermissions { get; set; } = new List<PortalRolePermission>();
}
