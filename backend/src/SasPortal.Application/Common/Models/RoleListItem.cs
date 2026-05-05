namespace SasPortal.Application.Common.Models;

public sealed record RoleListItem(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsSystem,
    bool IsActive,
    int PermissionCount);
