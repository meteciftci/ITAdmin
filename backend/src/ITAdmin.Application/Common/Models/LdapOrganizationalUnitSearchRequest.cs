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

public sealed record LdapOrganizationalUnitListItem(
    string DistinguishedName,
    string? Name,
    string? DisplayName,
    string? Ou,
    string Label);

public sealed record LdapOrganizationalUnitSearchResult(
    IReadOnlyList<LdapOrganizationalUnitListItem> Items,
    bool HasMore);
