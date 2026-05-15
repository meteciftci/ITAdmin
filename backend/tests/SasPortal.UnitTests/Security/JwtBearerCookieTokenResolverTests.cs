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
    public void TryGet_when_bearer_header_present_returns_null_even_if_cookie_exists()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer header-jwt";
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal) { [CookieName] = "cookie-jwt" });

        var token = JwtBearerCookieTokenResolver.TryGetAccessTokenFromCookieWhenNoBearerHeader(
            ctx.Request,
            CookieName);

        Assert.Null(token);
    }

    [Fact]
    public void TryGet_when_no_authorization_header_uses_cookie_token()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal) { [CookieName] = "from-cookie" });

        var token = JwtBearerCookieTokenResolver.TryGetAccessTokenFromCookieWhenNoBearerHeader(
            ctx.Request,
            CookieName);

        Assert.Equal("from-cookie", token);
    }

    [Fact]
    public void TryGet_when_non_bearer_authorization_uses_cookie()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Basic dGVzdA==";
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal) { [CookieName] = "from-cookie" });

        var token = JwtBearerCookieTokenResolver.TryGetAccessTokenFromCookieWhenNoBearerHeader(
            ctx.Request,
            CookieName);

        Assert.Equal("from-cookie", token);
    }

    [Fact]
    public async Task OnMessageReceived_assigns_token_from_access_cookie_when_no_bearer_header()
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
    public async Task OnMessageReceived_does_not_set_token_when_bearer_header_present()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer from-header";
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal)
                { [AuthAccessCookie.CookieName] = "from-cookie-should-not-be-used" });

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        var messageContext = new MessageReceivedContext(ctx, scheme, new JwtBearerOptions());

        await JwtBearerCookieTokenResolver.OnMessageReceived(messageContext);

        Assert.Null(messageContext.Token);
    }
}
