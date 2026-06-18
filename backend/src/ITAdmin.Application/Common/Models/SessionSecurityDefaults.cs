namespace ITAdmin.Application.Common.Models;

public static class SessionSecurityDefaults
{
    public const int AccessTokenMinutes = 30;
    public const int IdleTimeoutMinutes = 30;
    public const int IdleWarningSeconds = 30;
    public const int SessionRefreshTokenHours = 6;
    public const int RememberMeRefreshTokenDays = 7;
    public const bool RememberMeEnabled = true;

    public static SessionSecuritySettings AsSettings() =>
        new(
            AccessTokenMinutes,
            IdleTimeoutMinutes,
            IdleWarningSeconds,
            SessionRefreshTokenHours,
            RememberMeRefreshTokenDays,
            RememberMeEnabled);
}
