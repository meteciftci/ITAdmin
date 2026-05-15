using Microsoft.AspNetCore.Http;

namespace SasPortal.Api.Security;

/// <summary>
/// HttpOnly refresh token cookie for <c>/api/auth/*</c> endpoints.
/// Access token remains JSON + Bearer; this cookie is additive for future clients that send credentials.
/// Cross-origin SPA deployments will require CORS with credentials and a CSRF strategy in a later phase.
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
    /// <see cref="CookieOptions.Secure"/> is tied to <see cref="HttpRequest.IsHttps"/> so
    /// <c>http://localhost</c> development keeps working without extra configuration.
    /// </summary>
    public static CookieOptions CreateOptions(HttpRequest request, DateTimeOffset? expiresUtc)
    {
        var secure = request.IsHttps;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
            Expires = expiresUtc?.UtcDateTime,
        };
    }

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
