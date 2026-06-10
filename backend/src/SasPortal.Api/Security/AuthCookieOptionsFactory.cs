namespace SasPortal.Api.Security;

/// <summary>
/// Central factory for auth cookie options so HttpOnly/Secure/SameSite/Path/Expires behavior
/// is defined once and shared by <see cref="AuthAccessCookie"/>, <see cref="AuthRefreshCookie"/>
/// and <see cref="AuthCsrfCookie"/>. The <c>Secure</c> flag is resolved through
/// <see cref="AuthCookieSecurityResolver"/> instead of being tied blindly to
/// <see cref="HttpRequest.IsHttps"/>.
/// </summary>
public static class AuthCookieOptionsFactory
{
    public static CookieOptions Create(
        HttpRequest request,
        bool httpOnly,
        string path,
        DateTimeOffset? expiresUtc)
    {
        return new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = AuthCookieSecurityResolver.ResolveSecure(request),
            SameSite = SameSiteMode.Lax,
            Path = path,
            Expires = expiresUtc?.UtcDateTime,
        };
    }
}
