namespace ITAdmin.Api.Contracts.AdManagement;

public sealed record AdGroupMemberListItemResponse(
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

public sealed record AdGroupMembersListResponse(
    IReadOnlyList<AdGroupMemberListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int MemberCount,
    bool HasNextPage);

public sealed record AdGroupMemberCandidateItemResponse(
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

public sealed record AdGroupMemberCandidatesResponse(
    IReadOnlyList<AdGroupMemberCandidateItemResponse> Items);

public sealed record AddAdGroupMemberRequest(
    string MemberDistinguishedName,
    string? MemberType);

public sealed record RemoveAdGroupMemberRequest(
    string MemberDistinguishedName);

public sealed record AdGroupMemberOperationResponse(
    bool Success,
    string MessageKey,
    string? GroupId,
    string? GroupDistinguishedName,
    string? GroupName,
    string? MemberDistinguishedName,
    string? MemberName,
    IReadOnlyDictionary<string, object>? MessageParams = null);
