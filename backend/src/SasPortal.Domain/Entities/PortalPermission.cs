using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public class PortalPermission : SoftDeletableEntity
{
    public string Module { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<PortalRolePermission> RolePermissions { get; set; } = new List<PortalRolePermission>();
}
