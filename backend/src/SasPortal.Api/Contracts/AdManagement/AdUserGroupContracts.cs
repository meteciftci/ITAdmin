namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdUserGroupMembershipItemResponse(
    string DistinguishedName,
    string Name,
    string? SamAccountName,
    string? Description,
    bool IsDirect);

public sealed record AdUserDirectGroupMembershipsResponse(
    string UserId,
    string? DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DistinguishedName,
    IReadOnlyList<AdUserGroupMembershipItemResponse> Groups);

public sealed record AdGroupSearchItemResponse(
    string DistinguishedName,
    string Name,
    string? SamAccountName,
    string? Description);

public sealed record AdGroupSearchResponse(
    IReadOnlyList<AdGroupSearchItemResponse> Items);

public sealed record AdUserGroupMutationRequest(
    string GroupDistinguishedName);

public sealed record AdUserGroupOperationResponse(
    bool Success,
    string Message,
    string UserId,
    string GroupDistinguishedName,
    string? GroupName);
