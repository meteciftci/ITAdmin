using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models.LicenseManagement;

public sealed record LicenseManagementOverviewSummary(
    int CompanyCount,
    int ActiveProductCount,
    int PurchaseCount,
    int PackageCount,
    int TotalLicenseQuantity);

public sealed record LicenseManagementSettingsModel(
    string DefaultCurrency,
    bool DefaultVatIncluded,
    int DefaultRenewalReminderDays,
    string? DefaultRenewalRecipients,
    string? DefaultRenewalCcRecipients,
    string? Notes,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record UpdateLicenseManagementSettingsRequest(
    string DefaultCurrency,
    bool DefaultVatIncluded,
    int DefaultRenewalReminderDays,
    string? DefaultRenewalRecipients,
    string? DefaultRenewalCcRecipients,
    string? Notes,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicenseManagementSettingsResult(
    bool IsSuccess,
    string Message,
    LicenseManagementSettingsModel? Settings = null);

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

public sealed record LicensePurchaseListQuery(
    string? Search,
    LicensePurchaseType? PurchaseType,
    LicensePurchaseStatus? Status,
    Guid? SupplierCompanyId,
    int PageNumber,
    int PageSize);

public sealed record LicensePurchaseListItem(
    Guid Id,
    string Title,
    LicensePurchaseType PurchaseType,
    DateOnly? PurchaseDate,
    string? SupplierCompanyName,
    string? SupportCompanyName,
    string? ContractNumber,
    LicensePurchaseStatus Status);

public sealed record LicensePurchaseDetail(
    Guid Id,
    LicensePurchaseType PurchaseType,
    string Title,
    string? Description,
    DateOnly? PurchaseDate,
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
    LicensePurchaseStatus Status,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicensePurchaseRequest(
    LicensePurchaseType PurchaseType,
    string Title,
    string? Description,
    DateOnly? PurchaseDate,
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
    LicensePurchaseStatus Status,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicensePurchaseRequest(
    Guid Id,
    LicensePurchaseType PurchaseType,
    string Title,
    string? Description,
    DateOnly? PurchaseDate,
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

public sealed record UpdateLicensePurchaseStatusRequest(
    Guid Id,
    LicensePurchaseStatus Status,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record LicensePurchaseOperationResult(
    bool IsSuccess,
    string Message,
    LicensePurchaseDetail? Purchase = null);

public sealed record LicensePackageListQuery(
    string? Search,
    Guid? PurchaseId,
    Guid? ProductId,
    LicensePackageStatus? Status,
    bool? IsActive,
    int PageNumber,
    int PageSize);

public sealed record LicensePackageListItem(
    Guid Id,
    string ProductName,
    string PurchaseTitle,
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
    Guid PurchaseId,
    string PurchaseTitle,
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
    Guid PurchaseId,
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
    Guid PurchaseId,
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
