using Microsoft.AspNetCore.Http;

namespace ITAdmin.Api.Security;

/// <summary>
/// HttpOnly refresh token cookie scoped to <c>/api/auth/*</c>. ITAdmin uses full cookie auth:
/// both the access and refresh tokens are delivered exclusively as HttpOnly cookies and the
/// <c>Authorization: Bearer</c> header is not accepted (see <see cref="JwtBearerCookieTokenResolver"/>).
/// <see cref="CookieOptions"/> does not model "essential" consent flags; those apply when using cookie policy middleware.
/// </summary>
public static class AuthRefreshCookie
{
    public const string CookieName = AuthCookieNames.RefreshToken;

    /// <summary>
    /// Scoped to auth API routes so the cookie is not sent to unrelated paths.
    /// </summary>
    public const string CookiePath = "/api/auth";

    /// <summary>
    /// Builds cookie flags shared by append and delete so browsers reliably clear the cookie.
    /// The <c>Secure</c> flag is resolved through <see cref="AuthCookieSecurityResolver"/>:
    /// always on in production, request-scheme based elsewhere so <c>http://localhost</c>
    /// development keeps working without extra configuration.
    /// </summary>
    public static CookieOptions CreateOptions(HttpRequest request, DateTimeOffset? expiresUtc) =>
        AuthCookieOptionsFactory.Create(request, httpOnly: true, CookiePath, expiresUtc);

    public static void Append(
        IResponseCookies cookies,
        HttpRequest request,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAtUtc)
    {
        cookies.Append(CookieName, refreshToken, CreateOptions(request, refreshTokenExpiresAtUtc));
    }

    public static void Delete(IResponseCookies cookies, HttpRequest request)
    {
        cookies.Delete(CookieName, CreateOptions(request, DateTimeOffset.UtcNow.AddDays(-1)));
    }
}
