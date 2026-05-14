using Microsoft.AspNetCore.Http;
using SasPortal.Api.Security;

namespace SasPortal.UnitTests.Security;

public sealed class AuthRefreshCookieTests
{
    [Fact]
    public void Append_sets_set_cookie_with_name_path_httponly_samesite()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.IsHttps = false;

        var expires = DateTimeOffset.Parse("2030-01-02T03:04:05Z");
        AuthRefreshCookie.Append(ctx.Response.Cookies, ctx.Request, "refresh-value-xyz", expires);

        Assert.True(ctx.Response.Headers.ContainsKey("Set-Cookie"));
        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains(AuthRefreshCookie.CookieName, raw);
        Assert.Contains("refresh-value-xyz", raw);
        Assert.Contains("httponly", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Append_includes_secure_when_request_is_https()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.IsHttps = true;

        AuthRefreshCookie.Append(
            ctx.Response.Cookies,
            ctx.Request,
            "tok",
            DateTimeOffset.UtcNow.AddHours(1));

        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains("secure", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Delete_emits_set_cookie_for_same_path()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.IsHttps = false;

        AuthRefreshCookie.Delete(ctx.Response.Cookies, ctx.Request);

        Assert.True(ctx.Response.Headers.ContainsKey("Set-Cookie"));
        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains(AuthRefreshCookie.CookieName, raw);
        Assert.Contains("path=/api/auth", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateOptions_matches_append_delete_flags()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.IsHttps = true;
        var o = AuthRefreshCookie.CreateOptions(ctx.Request, DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.True(o.HttpOnly);
        Assert.True(o.Secure);
        Assert.Equal(SameSiteMode.Lax, o.SameSite);
        Assert.Equal(AuthRefreshCookie.CookiePath, o.Path);
    }
}
