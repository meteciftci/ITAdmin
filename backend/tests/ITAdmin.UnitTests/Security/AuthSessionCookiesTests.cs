using Microsoft.AspNetCore.Http;
using ITAdmin.Api.Security;

namespace ITAdmin.UnitTests.Security;

public sealed class AuthSessionCookiesTests
{
    [Fact]
    public void ApplySuccessfulAuthenticationCookies_sets_access_refresh_and_csrf_cookies()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.IsHttps = false;

        var accessExp = DateTime.SpecifyKind(new DateTime(2030, 1, 2, 3, 4, 5), DateTimeKind.Utc);
        var refreshExp = DateTime.SpecifyKind(new DateTime(2030, 6, 7, 8, 9, 10), DateTimeKind.Utc);

        AuthSessionCookies.ApplySuccessfulAuthenticationCookies(
            ctx.Response.Cookies,
            ctx.Request,
            "access-jwt",
            accessExp,
            "refresh-plain",
            refreshExp);

        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains(AuthAccessCookie.CookieName, raw);
        Assert.Contains(AuthRefreshCookie.CookieName, raw);
        Assert.Contains(AuthCsrfCookie.CookieName, raw);
        Assert.Contains("access-jwt", raw);
        Assert.Contains("refresh-plain", raw);
        Assert.Contains("path=/api", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearAuthenticationCookies_after_apply_emits_delete_for_all_three()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.IsHttps = false;

        var accessExp = DateTime.SpecifyKind(new DateTime(2030, 1, 2), DateTimeKind.Utc);
        var refreshExp = DateTime.SpecifyKind(new DateTime(2030, 6, 7), DateTimeKind.Utc);
        AuthSessionCookies.ApplySuccessfulAuthenticationCookies(
            ctx.Response.Cookies,
            ctx.Request,
            "a",
            accessExp,
            "r",
            refreshExp);

        ctx.Response.Headers.Remove("Set-Cookie");
        AuthSessionCookies.ClearAuthenticationCookies(ctx.Response.Cookies, ctx.Request);

        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains(AuthAccessCookie.CookieName, raw);
        Assert.Contains(AuthRefreshCookie.CookieName, raw);
        Assert.Contains(AuthCsrfCookie.CookieName, raw);
        Assert.Contains("path=/api", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", raw, StringComparison.OrdinalIgnoreCase);
    }
}
