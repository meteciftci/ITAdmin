namespace SasPortal.Application.Common.Models;

public enum AdDeletedObjectType
{
    User,
    Group,
    Computer,
    Unknown,
}

public enum AdDeletedObjectTypeFilter
{
    All,
    User,
    Group,
    Computer,
}

public sealed record AdDeletedObjectSearchQuery(
    string? Search,
    AdDeletedObjectTypeFilter Type,
    int PageNumber,
    int PageSize);

public sealed record AdDeletedObjectListItem(
    string Id,
    AdDeletedObjectType ObjectType,
    string? Name,
    string? DisplayName,
    string? SamAccountName,
    string? UserPrincipalName,
    string DistinguishedName,
    string? LastKnownParent,
    DateTimeOffset? WhenChanged,
    DateTimeOffset? DeletedAt);

public sealed record AdDeletedObjectSearchPage(
    IReadOnlyList<AdDeletedObjectListItem> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdDeletedObjectDetail(
    string Id,
    AdDeletedObjectType ObjectType,
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

public sealed record AdDeletedObjectSearchResult(
    bool IsSuccess,
    string Message,
    AdDeletedObjectSearchPage? Page,
    AdDirectoryFailureKind? FailureKind = null);

public sealed record AdDeletedObjectDetailRequest(Guid ObjectGuid);

public sealed record AdDeletedObjectDetailResult(
    bool IsSuccess,
    string Message,
    AdDeletedObjectDetail? Object,
    AdDirectoryFailureKind? FailureKind = null);
