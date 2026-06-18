namespace ITAdmin.Api.Contracts.Setup;

public sealed record CompleteSetupRequest(
    string SetupKey,
    CompleteSetupLdapSettingsRequest? Ldap,
    CompleteSetupModulesRequest? Modules,
    IReadOnlyList<CompleteSetupAdminUserRequest>? AdminUsers);

public sealed record CompleteSetupLdapSettingsRequest(
    string Name,
    string Host,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword);

public sealed record CompleteSetupModulesRequest(
    CompleteSetupAdManagementModuleRequest? AdManagement);

public sealed record CompleteSetupAdManagementModuleRequest(
    bool IsEnabled,
    string? UsersSearchBase,
    string? GroupsSearchBase,
    string? ComputersSearchBase,
    string? DefaultUserOu,
    string? DefaultGroupOu,
    string? DefaultComputerOu,
    bool DeletedObjectsEnabled);

public sealed record CompleteSetupAdminUserRequest(
    string UserName,
    string? DistinguishedName,
    string? DirectoryObjectId);
