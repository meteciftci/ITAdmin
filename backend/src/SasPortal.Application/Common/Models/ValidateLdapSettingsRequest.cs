namespace SasPortal.Application.Common.Models;

public sealed record ValidateLdapSettingsRequest(
    string Name,
    string Host,
    string BaseDn,
    string UserSearchBase,
    string UserSearchFilter,
    string BindUserName,
    string? BindUserDomain,
    string? BindPassword,
    string? TestUserName,
    string? TestPassword,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
