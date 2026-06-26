using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public class LicensedProduct : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? VendorCompanyId { get; set; }
    public string? Category { get; set; }
    public LicenseType? DefaultLicenseType { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public LicenseCompany? VendorCompany { get; set; }
    public ICollection<LicensePackage> LicensePackages { get; set; } = new List<LicensePackage>();
}
