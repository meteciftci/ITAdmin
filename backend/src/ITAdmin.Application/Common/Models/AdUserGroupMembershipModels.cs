namespace ITAdmin.Application.Common.Models;

public sealed record AdUserGroupMembershipRequest(
    Guid UserId,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AdGroupSearchRequest(string? Query);

public sealed record AddAdUserToGroupRequest(
    Guid UserId,
    string GroupDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record RemoveAdUserFromGroupRequest(
    Guid UserId,
    string GroupDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AdUserGroupMembershipItem(
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description,
    bool IsDirect);

public sealed record AdUserGroupMembershipResult(
    bool IsSuccess,
    string MessageKey,
    string? UserId,
    string? DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DistinguishedName,
    IReadOnlyList<AdUserGroupMembershipItem>? Groups,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdGroupSearchItem(
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description);

public sealed record AdGroupSearchResult(
    bool IsSuccess,
    string MessageKey,
    IReadOnlyList<AdGroupSearchItem>? Items,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdUserGroupOperationResult(
    bool IsSuccess,
    string MessageKey,
    string UserId,
    string GroupDistinguishedName,
    string? GroupName,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
