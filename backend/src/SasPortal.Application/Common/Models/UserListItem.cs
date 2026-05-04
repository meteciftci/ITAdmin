namespace SasPortal.Application.Common.Models;

public sealed record UserListItem(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Email,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyCollection<string> Roles);
