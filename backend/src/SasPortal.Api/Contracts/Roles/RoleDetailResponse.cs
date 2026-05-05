namespace SasPortal.Api.Contracts.Roles;

public sealed record RoleDetailResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsSystem,
    bool IsActive,
    IReadOnlyCollection<RolePermissionItemResponse> Permissions,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
