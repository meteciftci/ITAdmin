using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models.LicenseManagement;

// ---- Candidates (open request lines waiting to be fulfilled) ----

public sealed record LicenseFulfillmentCandidateQuery(
    string? Search,
    Guid? ProductId,
    string? RequesterUnitObjectGuid,
    int PageNumber,
    int PageSize);

public sealed record LicenseFulfillmentCandidateItem(
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

// ---- Triage (approve / reject / cancel / hold items) ----

public sealed record TriageLicenseRequestItemInput(
    Guid RequestItemId,
    LicenseRequestItemStatus Status,
    int? ApprovedQuantity);

public sealed record TriageLicenseRequestItemsRequest(
    IReadOnlyList<TriageLicenseRequestItemInput> Items,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

// ---- Conversion (selected approved lines -> purchase + packages) ----

public sealed record ConvertFulfillmentLineInput(
    Guid RequestItemId,
    int FulfillQuantity);

/// <summary>Per-product license package defaults applied to the created package for that product.</summary>
public sealed record ConvertFulfillmentPackageDefaultsInput(
    Guid ProductId,
    LicenseType LicenseType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsPerpetual);

/// <summary>Lean fields for a purchase created by the conversion; it starts as Draft and can be completed later.</summary>
public sealed record ConvertFulfillmentNewPurchaseInput(
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
    ConvertFulfillmentNewPurchaseInput? NewPurchase,
    IReadOnlyList<ConvertFulfillmentLineInput> Lines,
    IReadOnlyList<ConvertFulfillmentPackageDefaultsInput> PackageDefaults,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record LicenseFulfillmentResult(
    bool IsSuccess,
    string Message,
    Guid? PurchaseId = null,
    IReadOnlyList<Guid>? PackageIds = null);
