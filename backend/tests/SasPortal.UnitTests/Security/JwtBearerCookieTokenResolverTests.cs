using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using SasPortal.Api.Security;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.Security;

public sealed class JwtBearerCookieTokenResolverTests
{
    private const string CookieName = "sasportal.access_token";

    [Fact]
    public void TryGet_when_cookie_present_returns_cookie_value()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal) { [CookieName] = "from-cookie" });

        var token = JwtBearerCookieTokenResolver.TryGetAccessTokenFromCookie(
            ctx.Request,
            CookieName);

        Assert.Equal("from-cookie", token);
    }

    [Fact]
    public void TryGet_when_cookie_missing_returns_null()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Cookies = new FakeRequestCookieCollection();

        var token = JwtBearerCookieTokenResolver.TryGetAccessTokenFromCookie(
            ctx.Request,
            CookieName);

        Assert.Null(token);
    }

    [Fact]
    public void TryGet_ignores_bearer_authorization_header_and_uses_cookie()
    {
        // Full cookie-auth: Authorization header is not consulted, the cookie wins.
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer header-jwt";
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal) { [CookieName] = "cookie-jwt" });

        var token = JwtBearerCookieTokenResolver.TryGetAccessTokenFromCookie(
            ctx.Request,
            CookieName);

        Assert.Equal("cookie-jwt", token);
    }

    [Fact]
    public void TryGet_ignores_bearer_authorization_header_when_cookie_missing()
    {
        // A Bearer header alone must not authenticate the request.
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer header-jwt";
        ctx.Request.Cookies = new FakeRequestCookieCollection();

        var token = JwtBearerCookieTokenResolver.TryGetAccessTokenFromCookie(
            ctx.Request,
            CookieName);

        Assert.Null(token);
    }

    [Fact]
    public async Task OnMessageReceived_assigns_token_from_access_cookie()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal)
                { [AuthAccessCookie.CookieName] = "jwt-from-cookie" });

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        var messageContext = new MessageReceivedContext(ctx, scheme, new JwtBearerOptions());

        await JwtBearerCookieTokenResolver.OnMessageReceived(messageContext);

        Assert.Equal("jwt-from-cookie", messageContext.Token);
    }

    [Fact]
    public async Task OnMessageReceived_does_not_set_token_when_cookie_missing()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Cookies = new FakeRequestCookieCollection();

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        var messageContext = new MessageReceivedContext(ctx, scheme, new JwtBearerOptions());

        await JwtBearerCookieTokenResolver.OnMessageReceived(messageContext);

        Assert.Null(messageContext.Token);
    }

    [Fact]
    public async Task OnMessageReceived_ignores_bearer_header_and_uses_cookie()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer from-header";
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal)
                { [AuthAccessCookie.CookieName] = "jwt-from-cookie" });

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        var messageContext = new MessageReceivedContext(ctx, scheme, new JwtBearerOptions());

        await JwtBearerCookieTokenResolver.OnMessageReceived(messageContext);

        Assert.Equal("jwt-from-cookie", messageContext.Token);
    }

    [Fact]
    public async Task OnMessageReceived_does_not_set_token_when_only_bearer_header_present()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer from-header";
        ctx.Request.Cookies = new FakeRequestCookieCollection();

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        var messageContext = new MessageReceivedContext(ctx, scheme, new JwtBearerOptions());

        await JwtBearerCookieTokenResolver.OnMessageReceived(messageContext);

        Assert.Null(messageContext.Token);
    }
}
