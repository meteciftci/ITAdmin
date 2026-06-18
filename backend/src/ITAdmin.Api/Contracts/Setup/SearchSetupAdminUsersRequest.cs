namespace ITAdmin.Api.Contracts.Setup;

public sealed record SearchSetupAdminUsersRequest(
    string SetupKey,
    CompleteSetupLdapSettingsRequest? Ldap,
    string? Search);

public sealed record SearchSetupAdminUsersResponse(
    IReadOnlyList<SetupAdminUserSearchResultResponse> Users);

public sealed record SetupAdminUserSearchResultResponse(
    string UserName,
    string DisplayName,
    string? Email,
    string? DistinguishedName,
    string? DirectoryObjectId);
