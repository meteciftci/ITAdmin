using Microsoft.AspNetCore.Http;
using ITAdmin.Api.Middlewares;

namespace ITAdmin.UnitTests.Security;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Invoke_adds_baseline_security_headers_when_response_starts()
    {
        var responseFeature = new StartCapturingResponseFeature();
        var ctx = new DefaultHttpContext();
        ctx.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(responseFeature);
        ctx.Request.Path = "/index.html";

        var middleware = new SecurityHeadersMiddleware(innerCtx =>
        {
            innerCtx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(ctx);
        await responseFeature.FireOnStartingAsync();

        Assert.Equal("nosniff", ctx.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("no-referrer", ctx.Response.Headers["Referrer-Policy"]);
        Assert.Equal("DENY", ctx.Response.Headers["X-Frame-Options"]);
        Assert.Equal(SecurityHeadersMiddleware.PermissionsPolicy, ctx.Response.Headers["Permissions-Policy"]);
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, ctx.Response.Headers["Content-Security-Policy"]);
    }

    [Fact]
    public void Csp_blocks_framing_and_keeps_spa_assets_working()
    {
        Assert.Contains("frame-ancestors 'none'", SecurityHeadersMiddleware.ContentSecurityPolicy);
        Assert.Contains("script-src 'self'", SecurityHeadersMiddleware.ContentSecurityPolicy);
        Assert.Contains("style-src 'self' 'unsafe-inline'", SecurityHeadersMiddleware.ContentSecurityPolicy);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", SecurityHeadersMiddleware.ContentSecurityPolicy);
    }

    [Fact]
    public void ApplyHeaders_forces_no_store_for_api_error_response()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/users";
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;

        SecurityHeadersMiddleware.ApplyHeaders(ctx);

        Assert.Equal("no-store", ctx.Response.Headers.CacheControl);
    }

    [Fact]
    public void ApplyHeaders_forces_no_store_for_auth_responses()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/auth/login";
        ctx.Response.StatusCode = StatusCodes.Status200OK;

        SecurityHeadersMiddleware.ApplyHeaders(ctx);

        Assert.Equal("no-store", ctx.Response.Headers.CacheControl);
    }

    [Fact]
    public void ApplyHeaders_does_not_touch_cache_control_for_static_assets()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/assets/app.js";
        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        SecurityHeadersMiddleware.ApplyHeaders(ctx);

        Assert.Equal("public, max-age=31536000, immutable", ctx.Response.Headers.CacheControl);
    }

    [Theory]
    [InlineData("/api/users", 200, false)]
    [InlineData("/api/users", 404, true)]
    [InlineData("/api/users", 500, true)]
    [InlineData("/api/auth/login", 200, true)]
    [InlineData("/api/auth/refresh", 401, true)]
    [InlineData("/assets/app.js", 200, false)]
    [InlineData("/", 500, false)]
    public void ShouldForceNoStore_targets_api_error_and_auth_responses_only(
        string path,
        int statusCode,
        bool expected)
    {
        Assert.Equal(expected, SecurityHeadersMiddleware.ShouldForceNoStore(new PathString(path), statusCode));
    }

    private sealed class StartCapturingResponseFeature : Microsoft.AspNetCore.Http.Features.HttpResponseFeature
    {
        private Func<Task> _onStarting = () => Task.CompletedTask;

        public override void OnStarting(Func<object, Task> callback, object state) =>
            _onStarting = () => callback(state);

        public Task FireOnStartingAsync() => _onStarting();
    }
}
