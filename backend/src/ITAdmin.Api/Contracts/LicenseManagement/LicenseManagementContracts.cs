using ITAdmin.Domain.Enums;

namespace ITAdmin.Api.Contracts.LicenseManagement;

public sealed record LicenseManagementOverviewResponse(
    int CompanyCount,
    int ActiveProductCount,
    int AcquisitionCount,
    int PackageCount,
    int TotalLicenseQuantity);

public sealed record LicenseCompanyListItemResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? SupportEmail,
    string? ContactPersonName,
    bool IsActive);

public sealed record LicenseCompanyDetailResponse(
    Guid Id,
    string Name,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Website,
    string? Address,
    string? SupportPhone,
    string? SupportEmail,
    string? ContactPersonName,
    string? ContactPersonPhone,
    string? ContactPersonEmail,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicenseCompanyRequest(
    string Name,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Website,
    string? Address,
    string? SupportPhone,
    string? SupportEmail,
    string? ContactPersonName,
    string? ContactPersonPhone,
    string? ContactPersonEmail,
    string? Notes,
    bool IsActive);

public sealed record UpdateLicenseCompanyRequest(
    string Name,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Website,
    string? Address,
    string? SupportPhone,
    string? SupportEmail,
    string? ContactPersonName,
    string? ContactPersonPhone,
    string? ContactPersonEmail,
    string? Notes,
    bool IsActive);

public sealed record UpdateLicenseCompanyStatusRequest(bool IsActive);

public sealed record LicensedProductListItemResponse(
    Guid Id,
    string Name,
    string? VendorCompanyName,
    string? Category,
    LicenseType? DefaultLicenseType,
    bool IsActive);

public sealed record LicensedProductDetailResponse(
    Guid Id,
    string Name,
    Guid? VendorCompanyId,
    string? VendorCompanyName,
    string? Category,
    LicenseType? DefaultLicenseType,
    string? Description,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicensedProductRequest(
    string Name,
    Guid? VendorCompanyId,
    string? Category,
    LicenseType? DefaultLicenseType,
    string? Description,
    bool IsActive,
    string? Notes);

public sealed record UpdateLicensedProductRequest(
    string Name,
    Guid? VendorCompanyId,
    string? Category,
    LicenseType? DefaultLicenseType,
    string? Description,
    bool IsActive,
    string? Notes);

public sealed record UpdateLicensedProductStatusRequest(bool IsActive);

public sealed record LicenseAcquisitionListItemResponse(
    Guid Id,
    string Title,
    LicenseAcquisitionType AcquisitionType,
    DateOnly? AcquisitionDate,
    string? SupplierCompanyName,
    string? SupportCompanyName,
    string? ContractNumber,
    LicenseAcquisitionStatus Status);

public sealed record LicenseAcquisitionDetailResponse(
    Guid Id,
    LicenseAcquisitionType AcquisitionType,
    string Title,
    string? Description,
    DateOnly? AcquisitionDate,
    string? TenderNumber,
    DateOnly? TenderDate,
    string? DirectPurchaseNumber,
    string? DmoOrderNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    string? ContractNumber,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    Guid? SupplierCompanyId,
    string? SupplierCompanyName,
    Guid? SupportCompanyId,
    string? SupportCompanyName,
    decimal? ActualTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? Notes,
    LicenseAcquisitionStatus Status,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicenseAcquisitionRequest(
    LicenseAcquisitionType AcquisitionType,
    string Title,
    string? Description,
    DateOnly? AcquisitionDate,
    string? TenderNumber,
    DateOnly? TenderDate,
    string? DirectPurchaseNumber,
    string? DmoOrderNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    string? ContractNumber,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    Guid? SupplierCompanyId,
    Guid? SupportCompanyId,
    decimal? ActualTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? Notes,
    LicenseAcquisitionStatus Status);

public sealed record UpdateLicenseAcquisitionRequest(
    LicenseAcquisitionType AcquisitionType,
    string Title,
    string? Description,
    DateOnly? AcquisitionDate,
    string? TenderNumber,
    DateOnly? TenderDate,
    string? DirectPurchaseNumber,
    string? DmoOrderNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    string? ContractNumber,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    Guid? SupplierCompanyId,
    Guid? SupportCompanyId,
    decimal? ActualTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? Notes);

public sealed record UpdateLicenseAcquisitionStatusRequest(LicenseAcquisitionStatus Status);

public sealed record LicensePackageListItemResponse(
    Guid Id,
    string ProductName,
    string AcquisitionTitle,
    LicenseType LicenseType,
    int Quantity,
    int UsedQuantity,
    int AvailableQuantity,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsPerpetual,
    bool RenewalRequired,
    LicensePackageStatus Status,
    bool IsActive);

public sealed record LicensePackageDetailResponse(
    Guid Id,
    Guid AcquisitionId,
    string AcquisitionTitle,
    Guid ProductId,
    string ProductName,
    LicenseType LicenseType,
    int Quantity,
    int UsedQuantity,
    int AvailableQuantity,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsPerpetual,
    bool RenewalRequired,
    DateOnly? RenewalDate,
    string? SerialNumber,
    string? LicenseKey,
    string? LicenseAccountEmail,
    string? LicensePortalUrl,
    string? LicenseNotes,
    bool IsActive,
    LicensePackageStatus Status,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicensePackageRequest(
    Guid AcquisitionId,
    Guid ProductId,
    LicenseType LicenseType,
    int Quantity,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsPerpetual,
    bool RenewalRequired,
    DateOnly? RenewalDate,
    string? SerialNumber,
    string? LicenseKey,
    string? LicenseAccountEmail,
    string? LicensePortalUrl,
    string? LicenseNotes,
    bool IsActive,
    LicensePackageStatus Status);

public sealed record UpdateLicensePackageRequest(
    Guid AcquisitionId,
    Guid ProductId,
    LicenseType LicenseType,
    int Quantity,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsPerpetual,
    bool RenewalRequired,
    DateOnly? RenewalDate,
    string? SerialNumber,
    string? LicenseKey,
    string? LicenseAccountEmail,
    string? LicensePortalUrl,
    string? LicenseNotes,
    bool IsActive);

public sealed record UpdateLicensePackageStatusRequest(LicensePackageStatus Status);
