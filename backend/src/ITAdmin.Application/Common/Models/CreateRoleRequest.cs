namespace ITAdmin.Application.Common.Models;

public sealed record CreateRoleRequest(
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
