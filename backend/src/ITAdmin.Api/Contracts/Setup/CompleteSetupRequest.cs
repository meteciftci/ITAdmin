namespace ITAdmin.Api.Contracts.Setup;

public sealed record CompleteSetupRequest(
    string SetupKey,
    CompleteSetupLdapSettingsRequest? Ldap,
    IReadOnlyList<CompleteSetupAdminUserRequest>? AdminUsers);

public sealed record CompleteSetupLdapSettingsRequest(
    string Name,
    string Host,
    string BaseDn,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword);

public sealed record CompleteSetupAdminUserRequest(
    string UserName,
    string? DistinguishedName,
    string? DirectoryObjectId);
