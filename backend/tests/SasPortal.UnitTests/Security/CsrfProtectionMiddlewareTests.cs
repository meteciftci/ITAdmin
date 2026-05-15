using System.IO;
using Microsoft.AspNetCore.Http;
using SasPortal.Api.Security;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.Security;

public sealed class CsrfProtectionMiddlewareTests
{
    [Fact]
    public async Task Safe_get_request_passes_through()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("GET", "/api/users");

        await middleware.InvokeAsync(ctx);

        Assert.Equal(1, calls.Count);
        Assert.NotEqual(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Exempt_login_endpoint_passes_without_csrf()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("POST", "/api/auth/login");

        await middleware.InvokeAsync(ctx);

        Assert.Equal(1, calls.Count);
    }

    [Fact]
    public async Task Exempt_refresh_endpoint_passes_without_csrf()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("POST", "/api/auth/refresh");

        await middleware.InvokeAsync(ctx);

        Assert.Equal(1, calls.Count);
    }

    [Fact]
    public async Task Exempt_logout_endpoint_passes_without_csrf()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("POST", "/api/auth/logout");

        await middleware.InvokeAsync(ctx);

        Assert.Equal(1, calls.Count);
    }

    [Fact]
    public async Task Bearer_authed_request_bypasses_csrf()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("POST", "/api/users");
        ctx.Request.Headers.Authorization = "Bearer abc";

        await middleware.InvokeAsync(ctx);

        Assert.Equal(1, calls.Count);
    }

    [Fact]
    public async Task Request_without_access_cookie_bypasses_csrf_so_auth_layer_decides()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/api/users";
        ctx.Request.Cookies = new FakeRequestCookieCollection();

        await middleware.InvokeAsync(ctx);

        Assert.Equal(1, calls.Count);
    }

    [Fact]
    public async Task Cookie_authed_unsafe_request_with_matching_token_passes()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("POST", "/api/users", csrfCookie: "shared");
        ctx.Request.Headers[CsrfProtection.HeaderName] = "shared";

        await middleware.InvokeAsync(ctx);

        Assert.Equal(1, calls.Count);
    }

    [Fact]
    public async Task Cookie_authed_unsafe_request_without_header_returns_403()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("POST", "/api/users", csrfCookie: "shared");
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx);

        Assert.Equal(0, calls.Count);
        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Cookie_authed_unsafe_request_without_cookie_returns_403()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("POST", "/api/users", csrfCookie: null);
        ctx.Request.Headers[CsrfProtection.HeaderName] = "from-client";
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx);

        Assert.Equal(0, calls.Count);
        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Cookie_authed_unsafe_request_with_mismatched_token_returns_403()
    {
        var (next, calls) = NextSpy();
        var middleware = new CsrfProtectionMiddleware(next);
        var ctx = BuildCookieAuthedContext("POST", "/api/users", csrfCookie: "cookie-value");
        ctx.Request.Headers[CsrfProtection.HeaderName] = "header-value";
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx);

        Assert.Equal(0, calls.Count);
        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    private static (RequestDelegate Next, NextCallCounter Calls) NextSpy()
    {
        var counter = new NextCallCounter();
        RequestDelegate next = _ =>
        {
            counter.Count++;
            return Task.CompletedTask;
        };
        return (next, counter);
    }

    private sealed class NextCallCounter
    {
        public int Count;
    }

    private static DefaultHttpContext BuildCookieAuthedContext(
        string method,
        string path,
        string? csrfCookie = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AuthAccessCookie.CookieName] = "access-jwt",
        };
        if (csrfCookie is not null)
        {
            cookies[AuthCsrfCookie.CookieName] = csrfCookie;
        }

        ctx.Request.Cookies = new FakeRequestCookieCollection(cookies);
        return ctx;
    }
}
