namespace ITAdmin.Api.Contracts.AdManagement;

public sealed record AdGroupListItemResponse(
    string Id,
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? Cn,
    string? SamAccountName,
    string? Description,
    string GroupScope,
    bool SecurityEnabled,
    int? GroupType);

public sealed record AdGroupListResponse(
    IReadOnlyList<AdGroupListItemResponse> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdGroupMemberItemResponse(
    string Type,
    string? DisplayName,
    string? Name,
    string? SamAccountName,
    string DistinguishedName,
    string? Description);

public sealed record AdGroupDetailResponse(
    string Id,
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? Cn,
    string? SamAccountName,
    string? Description,
    string GroupScope,
    bool SecurityEnabled,
    int? GroupType,
    DateTimeOffset? WhenCreated,
    DateTimeOffset? WhenChanged,
    string? ManagedByDistinguishedName,
    string? ManagedByDisplayName,
    int MemberCount,
    int MemberOfCount,
    IReadOnlyList<AdGroupMemberItemResponse> Members,
    IReadOnlyList<AdGroupMemberItemResponse> MemberOf,
    bool MembersTruncated,
    bool MemberOfTruncated);
