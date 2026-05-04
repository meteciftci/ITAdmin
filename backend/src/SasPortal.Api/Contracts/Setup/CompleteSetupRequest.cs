namespace SasPortal.Api.Contracts.Setup;

public sealed record CompleteSetupRequest(
    string SetupKey,
    CompleteSetupLdapSettingsRequest Ldap,
    CompleteSetupAdminUserRequest Admin);

public sealed record CompleteSetupLdapSettingsRequest(
    string Name,
    string Host,
    int Port,
    bool UseSsl,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword,
    string? NationalIdAttribute);

public sealed record CompleteSetupAdminUserRequest(
    string UserName,
    string Password,
    string DisplayName,
    string? Email);
