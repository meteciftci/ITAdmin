namespace ITAdmin.Application.Common.Models;

public sealed record UpdateUserRolesRequest(
    Guid UserId,
    IReadOnlyCollection<Guid> RoleIds,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
