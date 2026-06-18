using Microsoft.AspNetCore.Http;
using ITAdmin.Api.Security;
using ITAdmin.UnitTests.TestInfrastructure;

namespace ITAdmin.UnitTests.Security;

public sealed class CsrfProtectionTests
{
    [Fact]
    public void IsUnsafeMethod_returns_true_for_post_put_patch_delete()
    {
        Assert.True(CsrfProtection.IsUnsafeMethod(HttpMethods.Post));
        Assert.True(CsrfProtection.IsUnsafeMethod(HttpMethods.Put));
        Assert.True(CsrfProtection.IsUnsafeMethod(HttpMethods.Patch));
        Assert.True(CsrfProtection.IsUnsafeMethod(HttpMethods.Delete));
        Assert.False(CsrfProtection.IsUnsafeMethod(HttpMethods.Get));
        Assert.False(CsrfProtection.IsUnsafeMethod(HttpMethods.Options));
    }

    [Fact]
    public void IsValidHeaderAndCookieMatch_requires_exact_match_constant_time()
    {
        Assert.True(CsrfProtection.IsValidHeaderAndCookieMatch("abc", "abc"));
        Assert.False(CsrfProtection.IsValidHeaderAndCookieMatch("abc", "abd"));
        Assert.False(CsrfProtection.IsValidHeaderAndCookieMatch("", "x"));
        Assert.False(CsrfProtection.IsValidHeaderAndCookieMatch("x", ""));
        Assert.False(CsrfProtection.IsValidHeaderAndCookieMatch(" ", " "));
    }

    [Theory]
    [InlineData("/api/auth/login", true)]
    [InlineData("/api/auth/refresh", true)]
    [InlineData("/api/auth/logout", true)]
    [InlineData("/API/AUTH/LOGIN", true)]
    [InlineData("/api/auth/me", false)]
    [InlineData("/api/users", false)]
    [InlineData("/", false)]
    public void IsExemptPath_matches_only_lifecycle_endpoints(string path, bool expected)
    {
        Assert.Equal(expected, CsrfProtection.IsExemptPath(new PathString(path)));
    }

    [Fact]
    public void ShouldValidateRequest_false_for_safe_methods()
    {
        var ctx = BuildCookieAuthedRequest("GET", "/api/users");
        Assert.False(CsrfProtection.ShouldValidateRequest(ctx.Request));
    }

    [Fact]
    public void ShouldValidateRequest_false_when_path_outside_api()
    {
        var ctx = BuildCookieAuthedRequest("POST", "/health");
        Assert.False(CsrfProtection.ShouldValidateRequest(ctx.Request));
    }

    [Fact]
    public void ShouldValidateRequest_false_for_exempt_auth_endpoints()
    {
        Assert.False(CsrfProtection.ShouldValidateRequest(
            BuildCookieAuthedRequest("POST", "/api/auth/login").Request));
        Assert.False(CsrfProtection.ShouldValidateRequest(
            BuildCookieAuthedRequest("POST", "/api/auth/refresh").Request));
        Assert.False(CsrfProtection.ShouldValidateRequest(
            BuildCookieAuthedRequest("POST", "/api/auth/logout").Request));
    }

    [Fact]
    public void ShouldValidateRequest_true_when_bearer_header_present_with_access_cookie()
    {
        // Bearer header must not bypass CSRF: enforcement follows the access cookie only.
        var ctx = BuildCookieAuthedRequest("POST", "/api/users");
        ctx.Request.Headers.Authorization = "Bearer token";

        Assert.True(CsrfProtection.ShouldValidateRequest(ctx.Request));
    }

    [Fact]
    public void ShouldValidateRequest_false_when_no_access_cookie_even_if_bearer_header_present()
    {
        // Without the access cookie there is no cookie-authenticated session to protect.
        // Leave the request to the authentication layer, which will reject it as 401.
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/api/users";
        ctx.Request.Headers.Authorization = "Bearer token";
        ctx.Request.Cookies = new FakeRequestCookieCollection();

        Assert.False(CsrfProtection.ShouldValidateRequest(ctx.Request));
    }

    [Fact]
    public void ShouldValidateRequest_false_when_no_access_cookie_and_no_authorization()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/api/users";
        ctx.Request.Cookies = new FakeRequestCookieCollection();

        Assert.False(CsrfProtection.ShouldValidateRequest(ctx.Request));
    }

    [Fact]
    public void ShouldValidateRequest_true_for_unsafe_cookie_authed_api_request()
    {
        var ctx = BuildCookieAuthedRequest("POST", "/api/users");
        Assert.True(CsrfProtection.ShouldValidateRequest(ctx.Request));
    }

    [Fact]
    public void TryValidateRequest_false_when_header_missing()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal) { [AuthCsrfCookie.CookieName] = "tok" });

        Assert.False(CsrfProtection.TryValidateRequest(ctx.Request));
    }

    [Fact]
    public void TryValidateRequest_true_when_cookie_and_header_match()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[CsrfProtection.HeaderName] = "shared-value";
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal)
                { [AuthCsrfCookie.CookieName] = "shared-value" });

        Assert.True(CsrfProtection.TryValidateRequest(ctx.Request));
    }

    private static DefaultHttpContext BuildCookieAuthedRequest(string method, string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Request.Cookies = new FakeRequestCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AuthAccessCookie.CookieName] = "access-jwt",
            });

        return ctx;
    }
}
