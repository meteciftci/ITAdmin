namespace ITAdmin.Api.Contracts.Roles;

public sealed record RolePermissionItemResponse(
    Guid Id,
    string Module,
    string Name,
    string Code,
    string? Description,
    bool IsActive);
