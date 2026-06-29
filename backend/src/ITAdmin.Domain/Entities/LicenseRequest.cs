using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public class LicenseRequest : AuditableEntity
{
    public string RequestNumber { get; set; } = string.Empty;
    public LicenseRequestSource RequestSource { get; set; }
    public DateOnly RequestDate { get; set; }
    public string? ExternalRequestNumber { get; set; }
    public string? EbysNumber { get; set; }
    public DateOnly? EbysDate { get; set; }
    public string RequestedByAdObjectId { get; set; } = string.Empty;
    public string? RequestedBySamAccountName { get; set; }
    public string? RequestedByUserPrincipalName { get; set; }
    public string? RequestedByDisplayName { get; set; }
    public string? RequestedByDepartment { get; set; }
    public string? RequestedByTitle { get; set; }
    public string? RequestedByMail { get; set; }
    public string? RequestedByPhone { get; set; }
    public string? RequestedByManagerName { get; set; }
    public string? RequesterUnit { get; set; }
    public string? Description { get; set; }
    public LicenseRequestStatus Status { get; set; } = LicenseRequestStatus.Draft;
    public decimal? EstimatedTotalCost { get; set; }
    public string? Currency { get; set; }
    public bool? VatIncluded { get; set; }
    public string? CostNote { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<LicenseRequestItem> Items { get; set; } = new List<LicenseRequestItem>();
}
