namespace ITAdmin.Application.Common.Models;

public sealed record RoleDetail(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsSystem,
    bool IsActive,
    IReadOnlyCollection<RolePermissionItem> Permissions,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
