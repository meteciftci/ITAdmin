namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdDeletedObjectListItemResponse(
    string Id,
    string ObjectType,
    string? Name,
    string? DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string DistinguishedName,
    string? LastKnownParent,
    DateTimeOffset? WhenChanged,
    DateTimeOffset? DeletedAt);

public sealed record AdDeletedObjectListResponse(
    IReadOnlyList<AdDeletedObjectListItemResponse> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdDeletedObjectDetailResponse(
    string Id,
    string ObjectType,
    string? Name,
    string? DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? Description,
    string DistinguishedName,
    string? LastKnownParent,
    string? LastKnownRdn,
    IReadOnlyList<string> ObjectClass,
    string? ObjectSid,
    DateTimeOffset? WhenCreated,
    DateTimeOffset? WhenChanged,
    DateTimeOffset? DeletedAt,
    string? Mail,
    string? Department,
    string? DnsHostName,
    string? OperatingSystem,
    int MemberOfCount,
    IReadOnlyList<string> MemberOf,
    bool MemberOfTruncated,
    IReadOnlyDictionary<string, string> AdditionalAttributes);
