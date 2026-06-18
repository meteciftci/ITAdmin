namespace ITAdmin.Api.Contracts.Roles;

public sealed record RoleListItemResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsSystem,
    bool IsActive,
    int PermissionCount);
