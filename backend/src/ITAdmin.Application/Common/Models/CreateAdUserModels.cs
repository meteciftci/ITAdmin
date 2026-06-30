namespace ITAdmin.Application.Common.Models;

public sealed record CreateAdUserRequest(
    string GivenName,
    string Surname,
    string? Department,
    string? SamAccountName,
    string UpnSuffix,
    string TargetOuDistinguishedName,
    string InitialPassword,
    bool IsEnabled,
    bool MustChangePasswordAtNextLogon,
    IReadOnlyList<CreateAdUserMappedAttributeRequest> MappedAttributes,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record CreateAdUserMappedAttributeRequest(
    string LogicalField,
    object? Value);

public sealed record CreateAdUserResult(
    bool IsSuccess,
    string MessageKey,
    CreateAdUserResponse? User,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record CreateAdUserResponse(
    string Id,
    string DistinguishedName,
    string Cn,
    string SamAccountName,
    string UserPrincipalName,
    string DisplayName,
    bool IsEnabled,
    string MessageKey,
    bool NamingCollisionResolved,
    int? GeneratedSuffix,
    AdManagementNotificationSummary? NotificationSummary = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdOrganizationalUnitSearchQuery(
    string? Search,
    int PageSize = 50);

public sealed record AdOrganizationalUnitListItem(
    string DistinguishedName,
    string? Name,
    string? DisplayName,
    string? Ou,
    string Label,
    string? ObjectGuid = null);

public sealed record AdOrganizationalUnitSearchResult(
    bool IsSuccess,
    string MessageKey,
    AdOrganizationalUnitSearchPage? Page,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdOrganizationalUnitSearchPage(
    IReadOnlyList<AdOrganizationalUnitListItem> Items,
    bool HasMore);
