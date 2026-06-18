namespace ITAdmin.Application.Common.Models;

public sealed record UpdateLdapSettingsRequest(
    string Name,
    string Host,
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
