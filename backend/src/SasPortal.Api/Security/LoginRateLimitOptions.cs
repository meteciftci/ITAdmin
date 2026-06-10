namespace SasPortal.Api.Security;

/// <summary>
/// Configuration for the login brute-force rate limit
/// (<c>Security:LoginRateLimit</c>). Defaults are safe but loose enough
/// not to lock out normal development or shared-office logins.
/// </summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "Security:LoginRateLimit";

    public const int DefaultPermitLimit = 10;
    public const int DefaultWindowSeconds = 60;
    public const int DefaultQueueLimit = 0;

    public int PermitLimit { get; set; } = DefaultPermitLimit;

    public int WindowSeconds { get; set; } = DefaultWindowSeconds;

    public int QueueLimit { get; set; } = DefaultQueueLimit;

    /// <summary>
    /// Guards against invalid configuration: a zero/negative permit limit or window would
    /// either disable login entirely or crash the limiter, so values fall back to safe minimums.
    /// </summary>
    public LoginRateLimitOptions Sanitize() => new()
    {
        PermitLimit = PermitLimit > 0 ? PermitLimit : DefaultPermitLimit,
        WindowSeconds = WindowSeconds > 0 ? WindowSeconds : DefaultWindowSeconds,
        QueueLimit = QueueLimit >= 0 ? QueueLimit : DefaultQueueLimit,
    };

    public static LoginRateLimitOptions Load(IConfiguration configuration) =>
        (configuration.GetSection(SectionName).Get<LoginRateLimitOptions>() ?? new LoginRateLimitOptions())
            .Sanitize();
}
