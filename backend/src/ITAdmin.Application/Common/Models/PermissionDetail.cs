namespace ITAdmin.Application.Common.Models;

public sealed record PermissionDetail(
    Guid Id,
    string Module,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
