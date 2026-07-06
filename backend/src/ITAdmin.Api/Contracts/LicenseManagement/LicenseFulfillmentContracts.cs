using ITAdmin.Domain.Enums;

namespace ITAdmin.Api.Contracts.LicenseManagement;

public sealed record LicenseFulfillmentCandidateResponse(
    Guid RequestId,
    Guid RequestItemId,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string RequesterUnitDisplayName,
    Guid ProductId,
    string ProductName,
    string? ProductBrand,
    int RequestedQuantity,
    int? ApprovedQuantity,
    int FulfilledQuantity,
    int RemainingQuantity,
    LicenseRequestItemStatus ItemStatus,
    bool IsFulfillable);

public sealed record TriageLicenseRequestItemRequest(
    Guid RequestItemId,
    LicenseRequestItemStatus Status,
    int? ApprovedQuantity);

public sealed record TriageLicenseRequestItemsRequest(
    IReadOnlyList<TriageLicenseRequestItemRequest> Items);

public sealed record ConvertFulfillmentLineRequest(
    Guid RequestItemId,
    int FulfillQuantity);

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

public sealed record ConvertLicenseRequestItemsRequest(
    Guid? ExistingPurchaseId,
    ConvertFulfillmentNewPurchaseRequest? NewPurchase,
    IReadOnlyList<ConvertFulfillmentLineRequest> Lines,
    IReadOnlyList<ConvertFulfillmentPackageDefaultsRequest> PackageDefaults);

public sealed record LicenseFulfillmentResponse(
    bool Success,
    string Message,
    Guid? PurchaseId,
    IReadOnlyList<Guid> PackageIds);
