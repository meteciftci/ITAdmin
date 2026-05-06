namespace SasPortal.Application.Common.Models;

public sealed record UpdateUserStatusRequest(
    Guid UserId,
    bool IsActive,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
