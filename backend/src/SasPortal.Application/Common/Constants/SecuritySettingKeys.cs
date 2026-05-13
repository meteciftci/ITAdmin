namespace SasPortal.Application.Common.Constants;

public static class SecuritySettingKeys
{
    public const string AccessTokenMinutes = "Security:AccessTokenMinutes";
    public const string IdleTimeoutMinutes = "Security:IdleTimeoutMinutes";
    public const string IdleWarningSeconds = "Security:IdleWarningSeconds";
    public const string SessionRefreshTokenHours = "Security:SessionRefreshTokenHours";
    public const string RememberMeRefreshTokenDays = "Security:RememberMeRefreshTokenDays";
    public const string RememberMeEnabled = "Security:RememberMeEnabled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        AccessTokenMinutes,
        IdleTimeoutMinutes,
        IdleWarningSeconds,
        SessionRefreshTokenHours,
        RememberMeRefreshTokenDays,
        RememberMeEnabled
    };

    public static readonly HashSet<string> AllSet = new(All, StringComparer.Ordinal);
}
