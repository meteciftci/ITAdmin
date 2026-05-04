namespace SasPortal.Application.Common.Models;

public sealed record LdapUserProfileRequest(
    string Host,
    int Port,
    bool UseSsl,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword,
    string UserName,
    string? NationalIdAttribute);
