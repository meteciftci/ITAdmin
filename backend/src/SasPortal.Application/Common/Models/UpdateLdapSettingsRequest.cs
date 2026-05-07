namespace SasPortal.Application.Common.Models;

public sealed record UpdateLdapSettingsRequest(
    string Name,
    string Host,
    int Port,
    bool UseSsl,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    string? BindPassword,
    string? Description,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
