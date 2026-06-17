using SasPortal.Application.Common.AdManagement;

namespace SasPortal.Application.Common.Models;

public sealed record AdGroupListQuery(
    string? Search,
    int PageNumber,
    int PageSize);

public sealed record AdGroupListItem(
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

public sealed record AdGroupSearchPage(
    IReadOnlyList<AdGroupListItem> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdGroupMemberItem(
    string Type,
    string? DisplayName,
    string? Name,
    string? SamAccountName,
    string DistinguishedName,
    string? Description);

public sealed record AdGroupDetail(
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
    IReadOnlyList<AdGroupMemberItem> Members,
    IReadOnlyList<AdGroupMemberItem> MemberOf,
    bool MembersTruncated,
    bool MemberOfTruncated);

public sealed record AdGroupDirectoryListResult(
    bool IsSuccess,
    string MessageKey,
    AdGroupSearchPage? Page,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdGroupDirectoryDetailResult(
    bool IsSuccess,
    string MessageKey,
    AdGroupDetail? Group,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
