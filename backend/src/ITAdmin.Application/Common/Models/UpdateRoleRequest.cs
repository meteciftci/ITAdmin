namespace ITAdmin.Application.Common.Models;

public sealed record UpdateRoleRequest(
    Guid RoleId,
    string Name,
    string? Description,
    bool IsActive,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
