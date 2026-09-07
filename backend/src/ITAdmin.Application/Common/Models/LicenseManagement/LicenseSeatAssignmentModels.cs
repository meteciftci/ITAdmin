using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models.LicenseManagement;

public sealed record LicenseSeatAssignmentItem(
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

public sealed record LicensePackageSeatOverview(
    Guid PackageId,
    string ProductName,
    string PurchaseTitle,
    int Quantity,
    int ActiveCount,
    int AvailableCount,
    IReadOnlyList<LicenseSeatAssignmentItem> Assignments);

public sealed record LicenseSeatPersonInput(
    string? AdObjectId,
    string DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? Mail,
    string? NationalId,
    string? Department,
    string? Title);

public sealed record LicenseSeatActorContext(
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AssignLicenseSeatRequest(
    Guid PackageId,
    LicenseSeatPersonInput Person,
    DateOnly? AssignedDate,
    Guid? SourceRequestItemId,
    string? Note,
    LicenseSeatActorContext Actor);

public sealed record ReleaseLicenseSeatRequest(
    Guid Id,
    DateOnly? ReleasedDate,
    string? Note,
    LicenseSeatActorContext Actor);

public sealed record TransferLicenseSeatRequest(
    Guid Id,
    LicenseSeatPersonInput NewPerson,
    DateOnly? TransferDate,
    string? Note,
    LicenseSeatActorContext Actor);

public sealed record CopyLicenseSeatsRequest(
    Guid SourcePackageId,
    Guid TargetPackageId,
    DateOnly? AssignedDate,
    LicenseSeatActorContext Actor);

public sealed record LicenseSeatAssignmentOperationResult(
    bool IsSuccess,
    string Message,
    LicenseSeatAssignmentItem? Assignment = null);

public sealed record CopyLicenseSeatsResult(
    bool IsSuccess,
    string Message,
    int CopiedCount = 0,
    int SkippedCount = 0);
