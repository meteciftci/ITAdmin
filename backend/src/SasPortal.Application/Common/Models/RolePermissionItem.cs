namespace SasPortal.Application.Common.Models;

public sealed record RolePermissionItem(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive);
