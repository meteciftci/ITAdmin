namespace ITAdmin.Api.Contracts.Roles;

public sealed record UpdateRoleRequest(
    string Name,
    string? Description,
    bool IsActive);
