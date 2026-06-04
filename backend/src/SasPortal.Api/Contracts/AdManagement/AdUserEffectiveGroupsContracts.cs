namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdMembershipPathNodeResponse(
    string Type,
    string Name,
    string? DisplayName,
    string? SamAccountName,
    string DistinguishedName);

public sealed record AdEffectiveGroupSummaryItemResponse(
    string Name,
    string DistinguishedName,
    string? SamAccountName,
    string? Description,
    string? DisplayName);

public sealed record AdEffectiveGroupNestedItemResponse(
    string Name,
    string DistinguishedName,
    string? SamAccountName,
    string? Description,
    string? DisplayName,
    int Depth,
    bool IsDirect,
    IReadOnlyList<AdMembershipPathNodeResponse> Path);

public sealed record AdUserEffectiveGroupsResponse(
    string UserId,
    string? DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DistinguishedName,
    IReadOnlyList<AdEffectiveGroupSummaryItemResponse> DirectGroups,
    IReadOnlyList<AdEffectiveGroupNestedItemResponse> EffectiveGroups,
    int MaxDepth,
    bool Truncated,
    string? TruncatedReason);
