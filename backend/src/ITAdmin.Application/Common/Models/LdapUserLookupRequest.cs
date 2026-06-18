namespace ITAdmin.Application.Common.Models;

public sealed record LdapUserLookupRequest(
    string Host,
    string BaseDn,
    string UserSearchBase,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword,
    string Search,
    int MaxResults,
    string? NationalIdAttribute);
