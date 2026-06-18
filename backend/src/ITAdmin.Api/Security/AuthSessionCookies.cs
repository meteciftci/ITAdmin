using Microsoft.AspNetCore.Http;

namespace ITAdmin.Api.Security;

/// <summary>
/// Orchestrates setting and clearing auth cookies for login, refresh, and logout flows.
/// </summary>
public static class AuthSessionCookies
{
    public static void ApplySuccessfulAuthenticationCookies(
        IResponseCookies cookies,
        HttpRequest request,
        string? accessToken,
        DateTime? accessTokenExpiresAtUtc,
        string? refreshToken,
        DateTime? refreshTokenExpiresAtUtc)
    {
        if (!string.IsNullOrWhiteSpace(accessToken) && accessTokenExpiresAtUtc is { } accessExpires)
        {
            AuthAccessCookie.Append(
                cookies,
                request,
                accessToken.Trim(),
                new DateTimeOffset(accessExpires, TimeSpan.Zero));
        }

        if (!string.IsNullOrWhiteSpace(refreshToken) && refreshTokenExpiresAtUtc is { } refreshExpires)
        {
            AuthRefreshCookie.Append(
                cookies,
                request,
                refreshToken.Trim(),
                new DateTimeOffset(refreshExpires, TimeSpan.Zero));
        }

        var csrfToken = CsrfTokenGenerator.CreateToken();
        var csrfExpires = ResolveCsrfCookieExpiryUtc(accessTokenExpiresAtUtc, refreshTokenExpiresAtUtc);
        AuthCsrfCookie.Append(cookies, request, csrfToken, csrfExpires);
    }

    public static void ClearAuthenticationCookies(IResponseCookies cookies, HttpRequest request)
    {
        AuthAccessCookie.Delete(cookies, request);
        AuthRefreshCookie.Delete(cookies, request);
        AuthCsrfCookie.Delete(cookies, request);
    }

    private static DateTimeOffset ResolveCsrfCookieExpiryUtc(
        DateTime? accessTokenExpiresAtUtc,
        DateTime? refreshTokenExpiresAtUtc)
    {
        if (accessTokenExpiresAtUtc is { } accessExpires)
        {
            return new DateTimeOffset(accessExpires, TimeSpan.Zero);
        }

        if (refreshTokenExpiresAtUtc is { } refreshExpires)
        {
            return new DateTimeOffset(refreshExpires, TimeSpan.Zero);
        }

        return DateTimeOffset.UtcNow.AddHours(1);
    }
}
