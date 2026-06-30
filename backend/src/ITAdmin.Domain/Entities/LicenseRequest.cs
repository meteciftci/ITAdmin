using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public class LicenseRequest : AuditableEntity
{
    public LicenseRequestSource RequestSource { get; set; }
    public DateOnly RequestDate { get; set; }
    public string? ExternalRequestNumber { get; set; }
    public string? EbysNumber { get; set; }
    public DateOnly? EbysDate { get; set; }
    public string RequesterUnitDisplayName { get; set; } = string.Empty;
    public string RequesterUnitDistinguishedName { get; set; } = string.Empty;
    public string RequesterUnitObjectGuid { get; set; } = string.Empty;
    public string? RequesterManagerName { get; set; }
    public string? Description { get; set; }
    public LicenseRequestStatus Status { get; set; } = LicenseRequestStatus.Draft;
    public decimal? EstimatedTotalCost { get; set; }
    public string? Currency { get; set; }
    public bool? VatIncluded { get; set; }
    public string? CostNote { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<LicenseRequestItem> Items { get; set; } = new List<LicenseRequestItem>();
}
