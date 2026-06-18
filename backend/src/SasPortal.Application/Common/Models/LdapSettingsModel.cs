namespace SasPortal.Application.Common.Models;

public sealed record LdapSettingsModel(
    Guid Id,
    string Name,
    string Host,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    bool HasBindPassword,
    string? Description,
    bool IsActive);
