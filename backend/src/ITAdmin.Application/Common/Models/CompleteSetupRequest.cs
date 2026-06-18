namespace ITAdmin.Application.Common.Models;

public sealed record CompleteSetupRequest(
    string SetupKey,
    CompleteSetupLdapSettings Ldap,
    CompleteSetupModulesSettings Modules,
    IReadOnlyList<CompleteSetupAdminUser> AdminUsers);

public sealed record CompleteSetupLdapSettings(
    string Name,
    string Host,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword);

public sealed record CompleteSetupModulesSettings(
    CompleteSetupAdManagementModuleSettings? AdManagement);

public sealed record CompleteSetupAdManagementModuleSettings(
    bool IsEnabled,
    string? UsersSearchBase,
    string? GroupsSearchBase,
    string? ComputersSearchBase,
    string? DefaultUserOu,
    string? DefaultGroupOu,
    string? DefaultComputerOu,
    bool DeletedObjectsEnabled);

public sealed record CompleteSetupAdminUser(
    string UserName,
    string? DistinguishedName,
    string? DirectoryObjectId);

public sealed record ValidateSetupLdapRequest(
    string SetupKey,
    CompleteSetupLdapSettings Ldap);

public sealed record ValidateSetupLdapResult(
    bool IsValid,
    string Message);

public sealed record SearchSetupAdminUsersRequest(
    string SetupKey,
    CompleteSetupLdapSettings Ldap,
    string Search);

public sealed record SetupAdminUserSearchResult(
    string UserName,
    string DisplayName,
    string? Email,
    string? DistinguishedName,
    string? DirectoryObjectId);

public sealed record SearchSetupAdminUsersResult(
    IReadOnlyList<SetupAdminUserSearchResult> Users,
    string? ErrorMessage = null)
{
    public bool IsSuccess => ErrorMessage is null;
}
