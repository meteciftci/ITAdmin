namespace SasPortal.Application.Common.Models;

public sealed record DeleteAdAttributeMappingRequest(
    Guid Id,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
