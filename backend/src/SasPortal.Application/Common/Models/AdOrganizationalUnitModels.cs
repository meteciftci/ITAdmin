namespace SasPortal.Application.Common.Models;

public sealed record AdOrganizationalUnitManageListQuery(
    string? Search,
    int PageNumber = 1,
    int PageSize = 25);

public sealed record AdOrganizationalUnitManageListItem(
    string ObjectGuid,
    string? Name,
    string? Ou,
    string DistinguishedName,
    string? ParentDistinguishedName,
    string CanonicalName,
    int ChildOuCount,
    int UserCount,
    int GroupCount,
    int ComputerCount);

public sealed record AdOrganizationalUnitManagePage(
    IReadOnlyList<AdOrganizationalUnitManageListItem> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdOrganizationalUnitManageListResult(
    bool IsSuccess,
    string MessageKey,
    AdOrganizationalUnitManagePage? Page,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdOrganizationalUnitChildListItem(
    string ObjectGuid,
    string? Name,
    string? Ou,
    string DistinguishedName,
    string CanonicalName);

public sealed record AdOrganizationalUnitContentSummary(
    int ChildOuCount,
    int UserCount,
    int GroupCount,
    int ComputerCount);

public sealed record AdOrganizationalUnitDetail(
    string ObjectGuid,
    string? Name,
    string? Ou,
    string? DisplayName,
    string DistinguishedName,
    string? ParentDistinguishedName,
    string CanonicalName,
    AdOrganizationalUnitContentSummary ContentSummary,
    IReadOnlyList<AdOrganizationalUnitChildListItem> ChildOrganizationalUnits);

public sealed record AdOrganizationalUnitDetailResult(
    bool IsSuccess,
    string MessageKey,
    AdOrganizationalUnitDetail? OrganizationalUnit,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record CreateAdOrganizationalUnitRequest(
    string Name,
    string ParentDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record CreateAdOrganizationalUnitResult(
    bool IsSuccess,
    string MessageKey,
    AdOrganizationalUnitDetail? OrganizationalUnit,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record RenameAdOrganizationalUnitRequest(
    Guid OrganizationalUnitId,
    string Name,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record RenameAdOrganizationalUnitResult(
    bool IsSuccess,
    string MessageKey,
    AdOrganizationalUnitDetail? OrganizationalUnit,
    string? PreviousDistinguishedName,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record MoveAdOrganizationalUnitRequest(
    Guid OrganizationalUnitId,
    string TargetParentDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record MoveAdOrganizationalUnitResult(
    bool IsSuccess,
    string MessageKey,
    AdOrganizationalUnitDetail? OrganizationalUnit,
    string? PreviousDistinguishedName,
    string? TargetParentDistinguishedName,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record DeleteAdOrganizationalUnitRequest(
    Guid OrganizationalUnitId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record DeleteAdOrganizationalUnitResult(
    bool IsSuccess,
    string MessageKey,
    string? DeletedOrganizationalUnitId,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
