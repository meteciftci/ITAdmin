namespace ITAdmin.Application.Common.Models;

public sealed record LdapOrganizationalUnitSearchRequest(
    string Host,
    string BaseDn,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword,
    string? Search,
    string? ParentDistinguishedName,
    int MaxResults);

public sealed record LdapOrganizationalUnitSearchResult(
    IReadOnlyList<SetupOrganizationalUnitListItem> Items,
    bool HasMore);
