using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public class LicensePackage : AuditableEntity
{
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public LicenseType LicenseType { get; set; }
    public int Quantity { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsPerpetual { get; set; }
    public bool RenewalRequired { get; set; }
    public DateOnly? RenewalDate { get; set; }
    public string? SerialNumber { get; set; }
    public string? LicenseKey { get; set; }
    public string? LicenseAccountEmail { get; set; }
    public string? LicensePortalUrl { get; set; }
    public string? LicenseNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public LicensePackageStatus Status { get; set; } = LicensePackageStatus.Active;

    public LicensePurchase Purchase { get; set; } = null!;
    public LicensedProduct Product { get; set; } = null!;
    public ICollection<LicenseRequestItemFulfillment> Fulfillments { get; set; } = new List<LicenseRequestItemFulfillment>();
}
