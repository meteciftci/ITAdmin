using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public class LicenseRequestItem : AuditableEntity
{
    public Guid RequestId { get; set; }
    public Guid ProductId { get; set; }
    public int RequestedQuantity { get; set; }
    public int? ApprovedQuantity { get; set; }
    public int FulfilledQuantity { get; set; }
    public decimal? EstimatedUnitCost { get; set; }
    public decimal? EstimatedTotalCost { get; set; }
    public string? Currency { get; set; }
    public bool? VatIncluded { get; set; }
    public string? Justification { get; set; }
    public LicenseRequestItemStatus Status { get; set; } = LicenseRequestItemStatus.Pending;

    public LicenseRequest Request { get; set; } = null!;
    public LicensedProduct Product { get; set; } = null!;
    public ICollection<LicenseRequestItemUser> Users { get; set; } = new List<LicenseRequestItemUser>();
}
