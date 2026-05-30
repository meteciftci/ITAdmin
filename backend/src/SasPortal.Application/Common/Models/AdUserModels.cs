namespace SasPortal.Application.Common.Models;

public enum AdUserStatusFilter
{
    Active,
    Disabled,
    All,
}

public enum AdDirectoryFailureKind
{
    NotConfigured,
    Disabled,
    MissingPassword,
    ConnectionFailed,
    NotFound,
    InvalidRequest,
}

public sealed record AdUserSearchQuery(
    string? Search,
    AdUserStatusFilter Status,
    int PageNumber,
    int PageSize);

public sealed record AdUserListItem(
    string Id,
    string DistinguishedName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Mail,
    string? Department,
    bool IsEnabled,
    bool IsLockedOut,
    DateTimeOffset? WhenCreated,
    DateTimeOffset? WhenChanged,
    DateTimeOffset? LastLogonAt);

public sealed record AdUserSearchPage(
    IReadOnlyList<AdUserListItem> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdUserGroupMembership(
    string Name,
    string DistinguishedName);

public sealed record MappedAdUserAttribute(
    string LogicalField,
    string DisplayName,
    string AdAttribute,
    IReadOnlyList<string>? Value,
    bool IsSensitive,
    string? MaskingStrategy,
    bool IsEditable,
    bool IsSearchable,
    int SortOrder);

public sealed record AdUserDetail(
    string Id,
    string DistinguishedName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Mail,
    string? GivenName,
    string? Surname,
    string? Department,
    bool IsEnabled,
    bool IsLockedOut,
    DateTimeOffset? PasswordLastSetAt,
    DateTimeOffset? LastLogonAt,
    DateTimeOffset? WhenCreated,
    DateTimeOffset? WhenChanged,
    IReadOnlyList<AdUserGroupMembership> Groups,
    IReadOnlyList<MappedAdUserAttribute> MappedAttributes);

public sealed record AdUserDirectorySearchResult(
    bool IsSuccess,
    string Message,
    AdUserSearchPage? Page,
    AdDirectoryFailureKind? FailureKind = null);

public sealed record AdUserDirectoryDetailResult(
    bool IsSuccess,
    string Message,
    AdUserDetail? User,
    AdDirectoryFailureKind? FailureKind = null);
