using ITAdmin.Domain.Enums;

namespace ITAdmin.Api.Contracts.LicenseManagement;

public sealed record LicenseFulfillmentCandidateUserResponse(
    Guid Id,
    string AdObjectId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Department,
    string? Title,
    string? Mail,
    LicenseRequestItemUserStatus Status);

public sealed record LicenseFulfillmentCandidateResponse(
    Guid RequestId,
    Guid RequestItemId,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string RequesterUnitDisplayName,
    Guid ProductId,
    string ProductName,
    string? ProductBrand,
    LicenseType LicenseType,
    int RequestedQuantity,
    int? ApprovedQuantity,
    int FulfilledQuantity,
    int RemainingQuantity,
    LicenseRequestItemStatus ItemStatus,
    bool IsFulfillable,
    IReadOnlyList<LicenseFulfillmentCandidateUserResponse> Users);

public sealed record TriageLicenseRequestItemRequest(
    Guid RequestItemId,
    LicenseRequestItemStatus Status,
    int? ApprovedQuantity,
    IReadOnlyList<Guid>? ApprovedUserIds = null);

public sealed record TriageLicenseRequestItemsRequest(
    IReadOnlyList<TriageLicenseRequestItemRequest> Items);

public sealed record ConvertFulfillmentLineRequest(
    Guid RequestItemId,
    int FulfillQuantity,
    IReadOnlyList<Guid>? RequestItemUserIds = null);

public sealed record ConvertFulfillmentPackageDefaultsRequest(
    Guid ProductId,
    LicenseType LicenseType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsPerpetual);

public sealed record ConvertFulfillmentNewPurchaseRequest(
    LicensePurchaseType PurchaseType,
    string Title,
    string? Description,
    DateOnly? PurchaseDate,
    Guid? SupplierCompanyId,
    Guid? SupportCompanyId,
    decimal? ActualTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? Notes);

public sealed record ConvertFulfillmentRenewalLineRequest(
    Guid SourcePackageId,
    int Quantity,
    LicenseType? LicenseType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsPerpetual,
    bool ExpireSourcePackage,
    bool CopySeatAssignments,
    bool? RenewalRequired = null,
    DateOnly? RenewalDate = null);

public sealed record ConvertFulfillmentManualLineRequest(
    Guid ProductId,
    int Quantity,
    LicenseType LicenseType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsPerpetual);

public sealed record ConvertLicenseRequestItemsRequest(
    Guid? ExistingPurchaseId,
    ConvertFulfillmentNewPurchaseRequest? NewPurchase,
    IReadOnlyList<ConvertFulfillmentLineRequest> Lines,
    IReadOnlyList<ConvertFulfillmentPackageDefaultsRequest> PackageDefaults,
    IReadOnlyList<ConvertFulfillmentRenewalLineRequest>? RenewalLines = null,
    IReadOnlyList<ConvertFulfillmentManualLineRequest>? ManualLines = null);

public sealed record LicenseFulfillmentResponse(
    bool Success,
    string Message,
    Guid? PurchaseId,
    IReadOnlyList<Guid> PackageIds);
