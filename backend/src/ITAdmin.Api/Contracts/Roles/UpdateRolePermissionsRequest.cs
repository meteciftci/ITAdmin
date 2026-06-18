namespace ITAdmin.Api.Contracts.Roles;

public sealed record UpdateRolePermissionsRequest(
    IReadOnlyCollection<Guid> PermissionIds);
