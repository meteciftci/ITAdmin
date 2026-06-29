using ITAdmin.Domain.Enums;

namespace ITAdmin.Api.Contracts.LicenseManagement;

public sealed record LicenseManagementOverviewResponse(
    int CompanyCount,
    int ActiveProductCount,
    int PurchaseCount,
    int PackageCount,
    int TotalLicenseQuantity);

public sealed record LicenseManagementSettingsResponse(
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
    string? Notes);

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

public sealed record LicensePurchaseListItemResponse(
    Guid Id,
    string Title,
    LicensePurchaseType PurchaseType,
    DateOnly? PurchaseDate,
    string? SupplierCompanyName,
    string? SupportCompanyName,
    string? ContractNumber,
    LicensePurchaseStatus Status);

public sealed record LicensePurchaseDetailResponse(
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
    LicensePurchaseStatus Status);

public sealed record UpdateLicensePurchaseRequest(
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
    string? Notes);

public sealed record UpdateLicensePurchaseStatusRequest(LicensePurchaseStatus Status);

public sealed record LicensePackageListItemResponse(
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

public sealed record LicensePackageDetailResponse(
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
    LicensePackageStatus Status);

public sealed record UpdateLicensePackageRequest(
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
    bool IsActive);

public sealed record UpdateLicensePackageStatusRequest(LicensePackageStatus Status);
