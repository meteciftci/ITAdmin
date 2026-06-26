using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public class LicenseAcquisition : AuditableEntity
{
    public LicenseAcquisitionType AcquisitionType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? AcquisitionDate { get; set; }
    public string? TenderNumber { get; set; }
    public DateOnly? TenderDate { get; set; }
    public string? DirectPurchaseNumber { get; set; }
    public string? DmoOrderNumber { get; set; }
    public string? EbysNumber { get; set; }
    public DateOnly? EbysDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public string? ContractNumber { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public Guid? SupplierCompanyId { get; set; }
    public Guid? SupportCompanyId { get; set; }
    public decimal? ActualTotalCost { get; set; }
    public string? Currency { get; set; }
    public bool? VatIncluded { get; set; }
    public string? Notes { get; set; }
    public LicenseAcquisitionStatus Status { get; set; } = LicenseAcquisitionStatus.Draft;

    public LicenseCompany? SupplierCompany { get; set; }
    public LicenseCompany? SupportCompany { get; set; }
    public ICollection<LicensePackage> LicensePackages { get; set; } = new List<LicensePackage>();
}
