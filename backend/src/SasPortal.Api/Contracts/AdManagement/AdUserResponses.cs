namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdUserListItemResponse(
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

public sealed record AdUserSearchResponse(
    IReadOnlyList<AdUserListItemResponse> Items,
    int PageNumber,
    int PageSize,
    bool HasNextPage);

public sealed record AdUserGroupMembershipResponse(
    string Name,
    string DistinguishedName);

public sealed record MappedAdUserAttributeResponse(
    string LogicalField,
    string DisplayName,
    string AdAttribute,
    IReadOnlyList<string>? Value,
    bool IsSensitive,
    string? MaskingStrategy,
    bool IsEditable,
    bool IsSearchable,
    int SortOrder);

public sealed record AdUserDetailResponse(
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
    DateTimeOffset? LockoutTimeAt,
    int? BadPwdCount,
    DateTimeOffset? BadPasswordTimeAt,
    DateTimeOffset? LastLogonTimestampAt,
    IReadOnlyList<AdUserGroupMembershipResponse> Groups,
    IReadOnlyList<MappedAdUserAttributeResponse> MappedAttributes,
    string? ManagerDistinguishedName = null,
    string? ManagerId = null,
    string? ManagerSamAccountName = null,
    string? ManagerUserPrincipalName = null,
    string? ManagerDisplayName = null);
