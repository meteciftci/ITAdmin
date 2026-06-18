namespace SasPortal.Api.Contracts.Settings;

public sealed record LdapSettingsResponse(
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
