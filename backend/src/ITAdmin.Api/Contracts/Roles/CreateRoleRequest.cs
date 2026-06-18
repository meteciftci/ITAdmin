namespace ITAdmin.Api.Contracts.Roles;

public sealed record CreateRoleRequest(
    string Name,
    string Code,
    string? Description,
    bool IsActive);
