using Microsoft.AspNetCore.Http;
using SasPortal.Api.Security;

namespace SasPortal.UnitTests.Security;

public sealed class AuthCsrfCookieTests
{
    [Fact]
    public void Append_sets_non_httponly_path_root_samesite_lax()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.IsHttps = false;

        var expires = DateTimeOffset.Parse("2030-01-02T03:04:05Z");
        AuthCsrfCookie.Append(ctx.Response.Cookies, ctx.Request, "csrf-secret", expires);

        Assert.True(ctx.Response.Headers.ContainsKey("Set-Cookie"));
        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains(AuthCsrfCookie.CookieName, raw);
        Assert.Contains("csrf-secret", raw);
        Assert.DoesNotContain("httponly", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Append_includes_secure_when_request_is_https()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.IsHttps = true;

        AuthCsrfCookie.Append(ctx.Response.Cookies, ctx.Request, "x", DateTimeOffset.UtcNow.AddMinutes(30));

        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains("secure", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Delete_emits_set_cookie_for_same_path()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.IsHttps = false;

        AuthCsrfCookie.Delete(ctx.Response.Cookies, ctx.Request);

        Assert.True(ctx.Response.Headers.ContainsKey("Set-Cookie"));
        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains(AuthCsrfCookie.CookieName, raw);
        Assert.Contains("path=/", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateOptions_has_http_only_false_and_path_root()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.IsHttps = false;
        var o = AuthCsrfCookie.CreateOptions(ctx.Request, DateTimeOffset.UtcNow.AddHours(1));
        Assert.False(o.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, o.SameSite);
        Assert.Equal(AuthCsrfCookie.CookiePath, o.Path);
    }
}
