namespace SasPortal.Application.Common.Models;

public sealed record CurrentUserResult(
    bool IsSuccess,
    string Message,
    Guid? UserId,
    string? UserName,
    string? DisplayName,
    string? Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    bool IsSuperAdmin);
