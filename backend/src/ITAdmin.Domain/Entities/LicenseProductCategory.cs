using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public class LicenseProductCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<LicensedProduct> Products { get; set; } = new List<LicensedProduct>();
}
