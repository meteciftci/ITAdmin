using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public class LicenseRequestItemUser : AuditableEntity
{
    public Guid RequestItemId { get; set; }
    public string AdObjectId { get; set; } = string.Empty;
    public string? SamAccountName { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? DisplayName { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
    public string? Mail { get; set; }
    public string? Phone { get; set; }
    public LicenseRequestItemUserStatus Status { get; set; } = LicenseRequestItemUserStatus.Pending;

    public LicenseRequestItem RequestItem { get; set; } = null!;
}
