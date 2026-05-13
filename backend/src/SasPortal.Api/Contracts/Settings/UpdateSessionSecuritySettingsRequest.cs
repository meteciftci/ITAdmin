namespace SasPortal.Api.Contracts.Settings;

public sealed record UpdateSessionSecuritySettingsRequest(
    int AccessTokenMinutes,
    int IdleTimeoutMinutes,
    int IdleWarningSeconds,
    int SessionRefreshTokenHours,
    int RememberMeRefreshTokenDays,
    bool RememberMeEnabled);
