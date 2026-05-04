namespace SasPortal.Application.Common.Models;

public sealed record UserDetail(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Email,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyCollection<string> Roles,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);
