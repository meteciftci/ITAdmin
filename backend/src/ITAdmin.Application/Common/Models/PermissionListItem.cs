namespace ITAdmin.Application.Common.Models;

public sealed record PermissionListItem(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive);
