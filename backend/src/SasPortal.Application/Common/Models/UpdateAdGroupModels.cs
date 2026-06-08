namespace SasPortal.Application.Common.Models;

public sealed record UpdateAdGroupRequest(
    Guid GroupId,
    string DisplayName,
    string Name,
    string SamAccountName,
    string? Description,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
