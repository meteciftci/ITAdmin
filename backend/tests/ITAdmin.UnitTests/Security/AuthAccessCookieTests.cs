using Microsoft.AspNetCore.Http;
using ITAdmin.Api.Security;

namespace ITAdmin.UnitTests.Security;

public sealed class AuthAccessCookieTests
{
    [Fact]
    public void Append_sets_set_cookie_with_name_path_httponly_samesite()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.IsHttps = false;

        var expires = DateTimeOffset.Parse("2030-01-02T03:04:05Z");
        AuthAccessCookie.Append(ctx.Response.Cookies, ctx.Request, "access-jwt-xyz", expires);

        Assert.True(ctx.Response.Headers.ContainsKey("Set-Cookie"));
        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains(AuthAccessCookie.CookieName, raw);
        Assert.Contains("access-jwt-xyz", raw);
        Assert.Contains("httponly", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Append_includes_secure_when_request_is_https()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.IsHttps = true;

        AuthAccessCookie.Append(
            ctx.Response.Cookies,
            ctx.Request,
            "tok",
            DateTimeOffset.UtcNow.AddHours(1));

        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains("secure", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Append_includes_secure_for_http_request_in_production_environment()
    {
        var ctx = AuthCookieSecurityResolverTests.CreateHttpContextForEnvironment("Production");
        ctx.Request.Scheme = "http";
        ctx.Request.IsHttps = false;

        AuthAccessCookie.Append(
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

        AuthAccessCookie.Delete(ctx.Response.Cookies, ctx.Request);

        Assert.True(ctx.Response.Headers.ContainsKey("Set-Cookie"));
        var raw = string.Join('\n', ctx.Response.Headers.SetCookie.ToArray());
        Assert.Contains(AuthAccessCookie.CookieName, raw);
        Assert.Contains("path=/api", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateOptions_matches_expected_flags_for_append_delete()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.IsHttps = true;
        var o = AuthAccessCookie.CreateOptions(ctx.Request, DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.True(o.HttpOnly);
        Assert.True(o.Secure);
        Assert.Equal(SameSiteMode.Lax, o.SameSite);
        Assert.Equal(AuthAccessCookie.CookiePath, o.Path);
    }
}
