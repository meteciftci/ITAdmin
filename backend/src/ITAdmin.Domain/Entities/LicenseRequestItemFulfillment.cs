using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

/// <summary>
/// Traceable link recording that a license package fulfilled a given quantity of a request item.
/// Enables incremental/partial fulfillment ("package P covered Q units of request item R").
/// </summary>
public class LicenseRequestItemFulfillment : AuditableEntity
{
    public Guid RequestItemId { get; set; }
    public Guid PackageId { get; set; }
    public int Quantity { get; set; }

    public LicenseRequestItem RequestItem { get; set; } = null!;
    public LicensePackage Package { get; set; } = null!;
}
