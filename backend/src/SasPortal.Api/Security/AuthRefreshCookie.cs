using Microsoft.AspNetCore.Http;

namespace SasPortal.Api.Security;

/// <summary>
/// HttpOnly refresh token cookie scoped to <c>/api/auth/*</c>. SAS Portal uses full cookie auth:
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
