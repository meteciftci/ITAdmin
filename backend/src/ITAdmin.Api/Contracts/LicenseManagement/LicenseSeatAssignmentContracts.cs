using ITAdmin.Domain.Enums;

namespace ITAdmin.Api.Contracts.LicenseManagement;

public sealed record LicenseSeatAssignmentResponse(
    Guid Id,
    Guid PackageId,
    string? AdObjectId,
    string DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? Mail,
    string? NationalId,
    string? Department,
    string? Title,
    DateOnly AssignedDate,
    DateOnly? ReleasedDate,
    LicenseSeatAssignmentStatus Status,
    Guid? ReplacesAssignmentId,
    string? ReplacesDisplayName,
    Guid? SourceRequestItemId,
    string? Note,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record LicenseSeatAssignmentListItemResponse(
    Guid Id,
    Guid PackageId,
    string ProductName,
    string? ProductBrand,
    string PurchaseTitle,
    string DisplayName,
    string? Mail,
    string? NationalId,
    string? Department,
    DateOnly AssignedDate,
    DateOnly? ReleasedDate,
    LicenseSeatAssignmentStatus Status);

public sealed record LicensePackageSeatOverviewResponse(
    Guid PackageId,
    string ProductName,
    string PurchaseTitle,
    int Quantity,
    int ActiveCount,
    int AvailableCount,
    IReadOnlyList<LicenseSeatAssignmentResponse> Assignments);

public sealed record LicenseSeatPersonRequest(
    string? AdObjectId,
    string DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? Mail,
    string? NationalId,
    string? Department,
    string? Title);

public sealed record AssignLicenseSeatBody(
    LicenseSeatPersonRequest Person,
    DateOnly? AssignedDate,
    Guid? SourceRequestItemId,
    string? Note);

public sealed record ReleaseLicenseSeatBody(
    DateOnly? ReleasedDate,
    string? Note);

public sealed record TransferLicenseSeatBody(
    LicenseSeatPersonRequest NewPerson,
    DateOnly? TransferDate,
    string? Note);

public sealed record CopyLicenseSeatsBody(
    Guid SourcePackageId,
    DateOnly? AssignedDate);

public sealed record LicenseSeatAssignmentOperationResponse(
    bool Success,
    string Message,
    LicenseSeatAssignmentResponse? Assignment);

public sealed record CopyLicenseSeatsResponse(
    bool Success,
    string Message,
    int CopiedCount,
    int SkippedCount);
