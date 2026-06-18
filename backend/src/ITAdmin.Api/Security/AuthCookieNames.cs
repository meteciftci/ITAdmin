namespace ITAdmin.Api.Security;

/// <summary>
/// Central names for auth-related cookies (access, refresh, CSRF).
/// </summary>
public static class AuthCookieNames
{
    public const string AccessToken = "itadmin.access_token";

    public const string RefreshToken = "itadmin.refresh_token";

    public const string CsrfToken = "itadmin.csrf_token";
}
