using Microsoft.AspNetCore.Http;

namespace SasPortal.Api.Security;

/// <summary>
/// HttpOnly access token cookie scoped to <c>/api</c>. This cookie is the sole transport
/// for the JWT access token; the <c>Authorization: Bearer</c> header is not accepted.
/// </summary>
public static class AuthAccessCookie
{
    public const string CookieName = AuthCookieNames.AccessToken;

    public const string CookiePath = "/api";

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
        string accessToken,
        DateTimeOffset accessTokenExpiresAtUtc)
    {
        cookies.Append(CookieName, accessToken, CreateOptions(request, accessTokenExpiresAtUtc));
    }

    public static void Delete(IResponseCookies cookies, HttpRequest request)
    {
        cookies.Delete(CookieName, CreateOptions(request, DateTimeOffset.UtcNow.AddDays(-1)));
    }
}
