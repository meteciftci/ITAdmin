using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models.LicenseManagement;

public sealed record LicenseManagementOverviewSummary(
    int CompanyCount,
    int ActiveProductCount,
    int AcquisitionCount,
    int PackageCount,
    int TotalLicenseQuantity);

public sealed record LicenseCompanyListQuery(
    string? Search,
    bool? IsActive,
    int PageNumber,
    int PageSize);

public sealed record LicenseCompanyListItem(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? SupportEmail,
    string? ContactPersonName,
    bool IsActive);

public sealed record LicenseCompanyDetail(
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

public sealed record LicenseCompanyActorRequest(
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

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
    bool IsActive,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicenseCompanyRequest(
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
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicenseCompanyStatusRequest(
    Guid Id,
    bool IsActive,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record LicenseCompanyOperationResult(
    bool IsSuccess,
    string Message,
    LicenseCompanyDetail? Company = null);

public sealed record LicensedProductListQuery(
    string? Search,
    bool? IsActive,
    Guid? VendorCompanyId,
    int PageNumber,
    int PageSize);

public sealed record LicensedProductListItem(
    Guid Id,
    string Name,
    string? VendorCompanyName,
    string? Category,
    LicenseType? DefaultLicenseType,
    bool IsActive);

public sealed record LicensedProductDetail(
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
    string? Notes,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicensedProductRequest(
    Guid Id,
    string Name,
    Guid? VendorCompanyId,
    string? Category,
    LicenseType? DefaultLicenseType,
    string? Description,
    bool IsActive,
    string? Notes,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicensedProductStatusRequest(
    Guid Id,
    bool IsActive,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record LicensedProductOperationResult(
    bool IsSuccess,
    string Message,
    LicensedProductDetail? Product = null);

public sealed record LicenseAcquisitionListQuery(
    string? Search,
    LicenseAcquisitionType? AcquisitionType,
    LicenseAcquisitionStatus? Status,
    Guid? SupplierCompanyId,
    int PageNumber,
    int PageSize);

public sealed record LicenseAcquisitionListItem(
    Guid Id,
    string Title,
    LicenseAcquisitionType AcquisitionType,
    DateOnly? AcquisitionDate,
    string? SupplierCompanyName,
    string? SupportCompanyName,
    string? ContractNumber,
    LicenseAcquisitionStatus Status);

public sealed record LicenseAcquisitionDetail(
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
    LicenseAcquisitionStatus Status,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicenseAcquisitionRequest(
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
    Guid? SupportCompanyId,
    decimal? ActualTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? Notes,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicenseAcquisitionStatusRequest(
    Guid Id,
    LicenseAcquisitionStatus Status,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record LicenseAcquisitionOperationResult(
    bool IsSuccess,
    string Message,
    LicenseAcquisitionDetail? Acquisition = null);

public sealed record LicensePackageListQuery(
    string? Search,
    Guid? AcquisitionId,
    Guid? ProductId,
    LicensePackageStatus? Status,
    bool? IsActive,
    int PageNumber,
    int PageSize);

public sealed record LicensePackageListItem(
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

public sealed record LicensePackageDetail(
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
    LicensePackageStatus Status,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicensePackageRequest(
    Guid Id,
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
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicensePackageStatusRequest(
    Guid Id,
    LicensePackageStatus Status,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record LicensePackageOperationResult(
    bool IsSuccess,
    string Message,
    LicensePackageDetail? Package = null);
