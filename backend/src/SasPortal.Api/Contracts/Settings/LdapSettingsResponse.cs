namespace SasPortal.Api.Contracts.Settings;

public sealed record LdapSettingsResponse(
    Guid Id,
    string Name,
    string Host,
    int Port,
    bool UseSsl,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    bool HasBindPassword,
    string? Description,
    bool IsActive);
