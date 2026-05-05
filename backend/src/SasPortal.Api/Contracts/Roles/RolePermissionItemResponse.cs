namespace SasPortal.Api.Contracts.Roles;

public sealed record RolePermissionItemResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive);
