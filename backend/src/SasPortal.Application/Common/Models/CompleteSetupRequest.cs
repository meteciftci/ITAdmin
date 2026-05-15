namespace SasPortal.Application.Common.Models;

public sealed record CompleteSetupRequest(
    string SetupKey,
    CompleteSetupLdapSettings Ldap,
    CompleteSetupAdminUser Admin);

public sealed record CompleteSetupLdapSettings(
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

public sealed record CompleteSetupAdminUser(
    string UserName,
    string Password);
