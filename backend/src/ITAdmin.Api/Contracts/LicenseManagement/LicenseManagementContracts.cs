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
    string? ContactPersonName,
    string? ContactPersonPhone,
    bool IsActive);

public sealed record LicenseCompanyDetailResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Website,
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
    string? Phone,
    string? Email,
    string? Website,
    string? ContactPersonName,
    string? ContactPersonPhone,
    string? ContactPersonEmail,
    string? Notes,
    bool IsActive);

public sealed record UpdateLicenseCompanyRequest(
    string Name,
    string? Phone,
    string? Email,
    string? Website,
    string? ContactPersonName,
    string? ContactPersonPhone,
    string? ContactPersonEmail,
    string? Notes,
    bool IsActive);

public sealed record UpdateLicenseCompanyStatusRequest(bool IsActive);

public sealed record LicensedProductListItemResponse(
    Guid Id,
    string Name,
    string? Brand,
    Guid CategoryId,
    string CategoryName,
    bool IsActive);

public sealed record LicensedProductDetailResponse(
    Guid Id,
    string Name,
    string? Brand,
    Guid CategoryId,
    string CategoryName,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicensedProductRequest(
    string Name,
    string? Brand,
    Guid CategoryId,
    string? Description,
    bool IsActive);

public sealed record UpdateLicensedProductRequest(
    string Name,
    string? Brand,
    Guid CategoryId,
    string? Description,
    bool IsActive);

public sealed record UpdateLicensedProductStatusRequest(bool IsActive);

public sealed record DirectoryUserLookupReadinessResponse(
    bool IsReady,
    string Reason,
    string? Message);

public sealed record LicenseProductCategoryListItemResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive);

public sealed record LicenseProductCategoryDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicenseProductCategoryRequest(
    string Name,
    string? Description,
    bool IsActive);

public sealed record UpdateLicenseProductCategoryRequest(
    string Name,
    string? Description,
    bool IsActive);

public sealed record UpdateLicenseProductCategoryStatusRequest(bool IsActive);

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

public sealed record LicenseRequestAdUserSnapshotRequest(
    string AdObjectId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Department,
    string? Title,
    string? Mail,
    string? Phone);

public sealed record LicenseRequestItemUserRequest(
    string AdObjectId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Department,
    string? Title,
    string? Mail,
    string? Phone,
    LicenseRequestItemUserStatus Status);

public sealed record LicenseRequestItemRequest(
    Guid ProductId,
    decimal? EstimatedUnitCost,
    string? Currency,
    bool? VatIncluded,
    string? Justification,
    LicenseRequestItemStatus Status,
    IReadOnlyList<LicenseRequestItemUserRequest> Users);

public sealed record LicenseRequestListItemResponse(
    Guid Id,
    string RequestNumber,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? RequestedByDisplayName,
    string? RequesterUnit,
    int ProductCount,
    int UserCount,
    decimal? EstimatedTotalCost,
    string? Currency,
    LicenseRequestStatus Status);

public sealed record LicenseRequestItemUserResponse(
    Guid Id,
    string AdObjectId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Department,
    string? Title,
    string? Mail,
    string? Phone,
    LicenseRequestItemUserStatus Status);

public sealed record LicenseRequestItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int RequestedQuantity,
    int? ApprovedQuantity,
    int FulfilledQuantity,
    decimal? EstimatedUnitCost,
    decimal? EstimatedTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? Justification,
    LicenseRequestItemStatus Status,
    IReadOnlyList<LicenseRequestItemUserResponse> Users);

public sealed record LicenseRequestDetailResponse(
    Guid Id,
    string RequestNumber,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? ExternalRequestNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    string RequestedByAdObjectId,
    string? RequestedBySamAccountName,
    string? RequestedByUserPrincipalName,
    string? RequestedByDisplayName,
    string? RequestedByDepartment,
    string? RequestedByTitle,
    string? RequestedByMail,
    string? RequestedByPhone,
    string? RequestedByManagerName,
    string? RequesterUnit,
    string? Description,
    LicenseRequestStatus Status,
    decimal? EstimatedTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? CostNote,
    bool IsActive,
    IReadOnlyList<LicenseRequestItemResponse> Items,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicenseRequestRequest(
    string RequestNumber,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? ExternalRequestNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    LicenseRequestAdUserSnapshotRequest RequestedBy,
    string? RequestedByManagerName,
    string? RequesterUnit,
    string? Description,
    LicenseRequestStatus Status,
    decimal? EstimatedTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? CostNote,
    IReadOnlyList<LicenseRequestItemRequest> Items);

public sealed record UpdateLicenseRequestRequest(
    string RequestNumber,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? ExternalRequestNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    LicenseRequestAdUserSnapshotRequest RequestedBy,
    string? RequestedByManagerName,
    string? RequesterUnit,
    string? Description,
    LicenseRequestStatus Status,
    decimal? EstimatedTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? CostNote,
    IReadOnlyList<LicenseRequestItemRequest> Items);

public sealed record UpdateLicenseRequestStatusRequest(LicenseRequestStatus Status);
