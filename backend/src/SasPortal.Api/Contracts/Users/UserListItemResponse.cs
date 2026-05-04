namespace SasPortal.Api.Contracts.Users;

public sealed record UserListItemResponse(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Email,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyCollection<string> Roles);
