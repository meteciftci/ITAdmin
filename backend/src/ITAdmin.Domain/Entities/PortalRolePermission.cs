using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public class PortalRolePermission : BaseEntity
{
    public Guid PortalRoleId { get; set; }
    public PortalRole PortalRole { get; set; } = null!;
    public Guid PortalPermissionId { get; set; }
    public PortalPermission PortalPermission { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
