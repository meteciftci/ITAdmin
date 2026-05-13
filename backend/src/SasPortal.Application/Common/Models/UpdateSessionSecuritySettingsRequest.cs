namespace SasPortal.Application.Common.Models;

public sealed record UpdateSessionSecuritySettingsRequest(
    int AccessTokenMinutes,
    int IdleTimeoutMinutes,
    int IdleWarningSeconds,
    int SessionRefreshTokenHours,
    int RememberMeRefreshTokenDays,
    bool RememberMeEnabled,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
