using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models.LicenseManagement;

public sealed record LicenseRequestAdUserSnapshot(
    string AdObjectId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Department,
    string? Title,
    string? Mail,
    string? Phone);

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
    string? RequestedByAdObjectId,
    Guid? ProductId,
    int PageNumber,
    int PageSize);

public sealed record LicenseRequestListItem(
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
    IReadOnlyList<LicenseRequestItemDetail> Items,
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
    LicenseRequestAdUserSnapshot RequestedBy,
    string? RequestedByManagerName,
    string? RequesterUnit,
    string? Description,
    LicenseRequestStatus Status,
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
    string RequestNumber,
    LicenseRequestSource RequestSource,
    DateOnly RequestDate,
    string? ExternalRequestNumber,
    string? EbysNumber,
    DateOnly? EbysDate,
    LicenseRequestAdUserSnapshot RequestedBy,
    string? RequestedByManagerName,
    string? RequesterUnit,
    string? Description,
    LicenseRequestStatus Status,
    decimal? EstimatedTotalCost,
    string? Currency,
    bool? VatIncluded,
    string? CostNote,
    IReadOnlyList<LicenseRequestItemInput> Items,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateLicenseRequestStatusRequest(
    Guid Id,
    LicenseRequestStatus Status,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record LicenseRequestOperationResult(
    bool IsSuccess,
    string Message,
    LicenseRequestDetail? Request = null);
