namespace SasPortal.Application.Common.Models;

public sealed record UpdateRolePermissionsRequest(
    Guid RoleId,
    IReadOnlyCollection<Guid> PermissionIds,
    string? ActorUserName);
