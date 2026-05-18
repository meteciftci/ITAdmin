namespace SasPortal.Application.Common.Models;

public sealed record AdManagementConnectionParameters(
    string? DomainFqdn,
    string? NetbiosDomainName,
    string? DefaultNamingContext,
    string? BaseDn,
    string? UsersRootOu,
    string? DisabledUsersOu,
    string? GroupsSearchBase,
    string? ComputersSearchBase,
    IReadOnlyList<string> PreferredDomainControllers,
    bool UseSsl,
    int LdapPort,
    string? ServiceAccountUserName,
    string? ServiceAccountPassword);
