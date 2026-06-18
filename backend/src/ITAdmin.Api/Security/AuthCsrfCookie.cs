using Microsoft.AspNetCore.Http;

namespace ITAdmin.Api.Security;

/// <summary>
/// Non-HttpOnly CSRF token cookie for double-submit validation in a later phase.
/// This phase only sets/clears the cookie; validation is intentionally not enforced in the pipeline.
/// </summary>
public static class AuthCsrfCookie
{
    public const string CookieName = AuthCookieNames.CsrfToken;

    public const string CookiePath = "/";

    public static CookieOptions CreateOptions(HttpRequest request, DateTimeOffset? expiresUtc) =>
        AuthCookieOptionsFactory.Create(request, httpOnly: false, CookiePath, expiresUtc);

    public static void Append(
        IResponseCookies cookies,
        HttpRequest request,
        string csrfToken,
        DateTimeOffset expiresAtUtc)
    {
        cookies.Append(CookieName, csrfToken, CreateOptions(request, expiresAtUtc));
    }

    public static void Delete(IResponseCookies cookies, HttpRequest request)
    {
        cookies.Delete(CookieName, CreateOptions(request, DateTimeOffset.UtcNow.AddDays(-1)));
    }
}
