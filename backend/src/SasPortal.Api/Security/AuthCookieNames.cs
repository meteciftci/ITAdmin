namespace SasPortal.Api.Security;

/// <summary>
/// Central names for auth-related cookies (access, refresh, CSRF).
/// </summary>
public static class AuthCookieNames
{
    public const string AccessToken = "sasportal.access_token";

    public const string RefreshToken = "sasportal.refresh_token";

    public const string CsrfToken = "sasportal.csrf_token";
}
