namespace SasPortal.Application.Common.Models;

public sealed record UpdateRoleStatusRequest(
    Guid RoleId,
    bool IsActive,
    string? ActorUserName);
