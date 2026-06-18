namespace ITAdmin.Api.Contracts.Permissions;

public sealed record PermissionDetailResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
