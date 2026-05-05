namespace SasPortal.Application.Common.Models;

public sealed record CreateRoleRequest(
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    string? ActorUserName);
