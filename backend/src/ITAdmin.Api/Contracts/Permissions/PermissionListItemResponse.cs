namespace ITAdmin.Api.Contracts.Permissions;

public sealed record PermissionListItemResponse(
    Guid Id,
    string Module,
    string Name,
    string Code,
    string? Description,
    bool IsActive);
