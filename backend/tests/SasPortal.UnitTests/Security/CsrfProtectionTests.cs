using Microsoft.AspNetCore.Http;
using SasPortal.Api.Security;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.Security;

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
}
