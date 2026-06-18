namespace ITAdmin.Api.Contracts.Auth;

public sealed record CurrentUserResponse(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? Email,
    string PreferredLanguage,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    bool IsSuperAdmin);
