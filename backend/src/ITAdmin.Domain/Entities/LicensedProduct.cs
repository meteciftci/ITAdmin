using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public class LicensedProduct : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public Guid CategoryId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public LicenseProductCategory Category { get; set; } = null!;
    public ICollection<LicensePackage> LicensePackages { get; set; } = new List<LicensePackage>();
}
