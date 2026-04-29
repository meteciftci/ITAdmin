using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public class PortalUserRole : BaseEntity
{
    public Guid PortalUserId { get; set; }
    public PortalUser PortalUser { get; set; } = null!;
    public Guid PortalRoleId { get; set; }
    public PortalRole PortalRole { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
