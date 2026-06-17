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
    int? UserAccountControl,
    DateTimeOffset? AccountExpiresAt,
    string? AccountExpiresDate,
    DateTimeOffset? LockoutTimeAt,
    int? BadPwdCount,
    DateTimeOffset? BadPasswordTimeAt,
    DateTimeOffset? LastLogonTimestampAt,
    IReadOnlyList<AdUserGroupMembership> Groups,
    IReadOnlyList<MappedAdUserAttribute> MappedAttributes,
    string? ManagerDistinguishedName = null,
    string? ManagerId = null,
    string? ManagerSamAccountName = null,
    string? ManagerUserPrincipalName = null,
    string? ManagerDisplayName = null);

public sealed record UpdateAdUserManagerRequest(
    Guid UserId,
    Guid? ManagerUserId,
    bool ClearManager,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateAdUserManagerResult(
    bool IsSuccess,
    string MessageKey,
    string? UserId,
    string? SamAccountName,
    string? ManagerDistinguishedName,
    string? ManagerDisplayName,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record UpdateAdUserAccountExpirationRequest(
    Guid UserId,
    bool NeverExpires,
    string? ExpiresAt,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record UpdateAdUserAccountExpirationResult(
    bool IsSuccess,
    string MessageKey,
    string? UserId,
    string? SamAccountName,
    string? AccountExpiresDate,
    bool NeverExpires,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdUserDirectorySearchResult(
    bool IsSuccess,
    string MessageKey,
    AdUserSearchPage? Page,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdUserDirectoryDetailResult(
    bool IsSuccess,
    string MessageKey,
    AdUserDetail? User,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
