namespace ITAdmin.Application.Common.Models;

public sealed record AuthenticatedUserInfo(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
