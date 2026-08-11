namespace ITAdmin.Application.Common.Models;

public sealed record RolePermissionItem(
    Guid Id,
    string Module,
    string Name,
    string Code,
    string? Description,
    bool IsActive);
