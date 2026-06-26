using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public class LicenseCompany : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? SupportPhone { get; set; }
    public string? SupportEmail { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonPhone { get; set; }
    public string? ContactPersonEmail { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<LicensedProduct> VendorProducts { get; set; } = new List<LicensedProduct>();
    public ICollection<LicenseAcquisition> SupplierAcquisitions { get; set; } = new List<LicenseAcquisition>();
    public ICollection<LicenseAcquisition> SupportAcquisitions { get; set; } = new List<LicenseAcquisition>();
}
