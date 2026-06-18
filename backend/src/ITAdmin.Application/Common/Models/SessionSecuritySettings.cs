namespace ITAdmin.Application.Common.Models;

public sealed record SessionSecuritySettings(
    int AccessTokenMinutes,
    int IdleTimeoutMinutes,
    int IdleWarningSeconds,
    int SessionRefreshTokenHours,
    int RememberMeRefreshTokenDays,
    bool RememberMeEnabled);
