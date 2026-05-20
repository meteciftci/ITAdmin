namespace SasPortal.Application.Common.Models;

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
    string Message,
    CreateAdUserResponse? User,
    AdDirectoryFailureKind? FailureKind = null);

public sealed record CreateAdUserResponse(
    string Id,
    string DistinguishedName,
    string Cn,
    string SamAccountName,
    string UserPrincipalName,
    string DisplayName,
    bool IsEnabled,
    string Message,
    bool NamingCollisionResolved,
    int? GeneratedSuffix);

public sealed record AdOrganizationalUnitSearchQuery(
    string? Search,
    int PageSize = 50);

public sealed record AdOrganizationalUnitListItem(
    string DistinguishedName,
    string? Name,
    string? DisplayName,
    string? Ou,
    string Label);

public sealed record AdOrganizationalUnitSearchResult(
    bool IsSuccess,
    string Message,
    AdOrganizationalUnitSearchPage? Page,
    AdDirectoryFailureKind? FailureKind = null);

public sealed record AdOrganizationalUnitSearchPage(
    IReadOnlyList<AdOrganizationalUnitListItem> Items,
    bool HasMore);
