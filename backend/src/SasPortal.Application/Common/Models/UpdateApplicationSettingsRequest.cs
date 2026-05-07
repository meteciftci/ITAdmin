namespace SasPortal.Application.Common.Models;

public sealed record UpdateApplicationSettingsRequest(
    IReadOnlyList<UpdateApplicationSettingRequest> Items,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
