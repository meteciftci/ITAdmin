using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models.LicenseManagement;

public sealed record LicenseRequestOuSnapshot(
    string ObjectGuid,
    string DisplayName,
    string DistinguishedName);

public sealed record LicenseRequestItemUserInput(
    string AdObjectId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Department,
    string? Title,
    string? Mail,
    string? Phone,
    LicenseRequestItemUserStatus Status);

public sealed record LicenseRequestItemInput(
    Guid ProductId,
    decimal? EstimatedUnitCost,
    string? Currency,
    bool? VatIncluded,
    string? Justification,
    LicenseRequestItemStatus Status,
    IReadOnlyList<LicenseRequestItemUserInput> Users);

public sealed record LicenseRequestListQuery(
    string? Search,
    LicenseRequestStatus? Status,
    LicenseRequestSource? RequestSource,
    DateOnly? RequestDateFrom,
    DateOnly? RequestDateTo,
    string? RequesterUnitObjectGuid,
    Guid? ProductId,
    int PageNumber,
    int PageSize);

public sealed record LicenseRequestListItem(
    Guid Id,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? ExternalRequestNumber,
    string? EbysNumber,
    string RequesterUnitDisplayName,
    string? RequesterManagerName,
    int ProductCount,
    int UserCount,
    decimal? EstimatedTotalCost,
    string? Currency,
    LicenseRequestStatus Status);

public sealed record LicenseRequestItemUserDetail(
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

public sealed record LicenseRequestItemDetail(
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
    IReadOnlyList<LicenseRequestItemUserDetail> Users);

public sealed record LicenseRequestDetail(
    Guid Id,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? ExternalRequestNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    string RequesterUnitDisplayName,
    string RequesterUnitDistinguishedName,
    string RequesterUnitObjectGuid,
    string? RequesterManagerName,
    string? Description,
    LicenseRequestStatus Status,
    decimal? EstimatedTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? CostNote,
    bool IsActive,
    IReadOnlyList<LicenseRequestItemDetail> Items,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateLicenseRequestRequest(
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? ExternalRequestNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    LicenseRequestOuSnapshot RequesterUnit,
    string? RequesterManagerName,
    string? Description,
    decimal? EstimatedTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? CostNote,
    IReadOnlyList<LicenseRequestItemInput> Items,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicenseRequestRequest(
    Guid Id,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? ExternalRequestNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    LicenseRequestOuSnapshot RequesterUnit,
    string? RequesterManagerName,
    string? Description,
    decimal? EstimatedTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? CostNote,
    IReadOnlyList<LicenseRequestItemInput> Items,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record LicenseRequestOperationResult(
    bool IsSuccess,
    string Message,
    LicenseRequestDetail? Request = null);

// Item-level AD user snapshots for license request items only.
public sealed record LicenseRequestAdUserSnapshot(
    string AdObjectId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Department,
    string? Title,
    string? Mail,
    string? Phone);
