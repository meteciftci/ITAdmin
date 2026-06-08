namespace SasPortal.Application.Common.Models;

public sealed record AdGroupMembersListQuery(
    Guid GroupId,
    string? Search,
    string? Type,
    int PageNumber,
    int PageSize);

public sealed record AdGroupMemberListItem(
    string? Id,
    string Type,
    string? DisplayName,
    string? Name,
    string? Cn,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DNSHostName,
    string? Description,
    string DistinguishedName,
    bool IsDirectMember);

public sealed record AdGroupMembersPage(
    IReadOnlyList<AdGroupMemberListItem> Items,
    int PageNumber,
    int PageSize,
    int MemberCount,
    bool HasNextPage);

public sealed record AdGroupMembersListResult(
    bool IsSuccess,
    string Message,
    AdGroupMembersPage? Page,
    AdDirectoryFailureKind? FailureKind = null);

public sealed record AdGroupMemberCandidatesQuery(
    Guid GroupId,
    string? Search,
    IReadOnlyList<string> Types,
    int PageSize);

public sealed record AdGroupMemberCandidateItem(
    string? Id,
    string Type,
    string? DisplayName,
    string? Name,
    string? Cn,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DNSHostName,
    string? Description,
    string DistinguishedName,
    bool IsAlreadyDirectMember,
    bool? IsEnabled);

public sealed record AdGroupMemberCandidatesResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<AdGroupMemberCandidateItem>? Items,
    AdDirectoryFailureKind? FailureKind = null);

public sealed record AddAdGroupMemberRequest(
    Guid GroupId,
    string MemberDistinguishedName,
    string? MemberType,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record RemoveAdGroupMemberRequest(
    Guid GroupId,
    string MemberDistinguishedName,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record AdGroupMemberOperationResult(
    bool IsSuccess,
    string Message,
    string? GroupId,
    string? GroupDistinguishedName,
    string? GroupName,
    string? MemberDistinguishedName,
    string? MemberName,
    AdDirectoryFailureKind? FailureKind = null);
