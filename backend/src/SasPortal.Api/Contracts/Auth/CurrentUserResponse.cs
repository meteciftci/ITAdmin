namespace SasPortal.Api.Contracts.Auth;

public sealed record CurrentUserResponse(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    bool IsSuperAdmin);
