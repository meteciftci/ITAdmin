namespace SasPortal.Application.Common.Models;

public sealed record UpdateRoleRequest(
    Guid RoleId,
    string Name,
    string? Description,
    bool IsActive,
    string? ActorUserName);
