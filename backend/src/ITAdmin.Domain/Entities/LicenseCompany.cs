using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public class LicenseCompany : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonPhone { get; set; }
    public string? ContactPersonEmail { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<LicensePurchase> SupplierPurchases { get; set; } = new List<LicensePurchase>();
    public ICollection<LicensePurchase> SupportPurchases { get; set; } = new List<LicensePurchase>();
}
