namespace ITAdmin.Application.Common.Models;

public sealed record UpdateRoleStatusRequest(
    Guid RoleId,
    bool IsActive,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
