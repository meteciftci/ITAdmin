namespace ITAdmin.Application.Common.Models;

public sealed record LdapUserProfileRequest(
    string Host,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword,
    string UserName);
