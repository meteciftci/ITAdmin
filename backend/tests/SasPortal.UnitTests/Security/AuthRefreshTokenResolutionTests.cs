using SasPortal.Api.Security;

namespace SasPortal.UnitTests.Security;

public sealed class AuthRefreshTokenResolutionTests
{
    [Fact]
    public void ResolveFromBodyFirstThenCookie_prefers_body_when_both_present()
    {
        var resolved = AuthRefreshTokenResolution.ResolveFromBodyFirstThenCookie(" body-token ", "cookie-token");
        Assert.Equal("body-token", resolved);
    }

    [Fact]
    public void ResolveFromBodyFirstThenCookie_uses_cookie_when_body_missing()
    {
        var resolved = AuthRefreshTokenResolution.ResolveFromBodyFirstThenCookie(null, " cookie ");
        Assert.Equal("cookie", resolved);
    }

    /// <summary>
    /// Cookie-only refresh/logout sends an empty body; body refresh token is treated as absent and the cookie value is used.
    /// </summary>
    [Fact]
    public void ResolveFromBodyFirstThenCookie_uses_cookie_when_body_is_whitespace_only()
    {
        var resolved = AuthRefreshTokenResolution.ResolveFromBodyFirstThenCookie("   ", "from-cookie");
        Assert.Equal("from-cookie", resolved);
    }

    [Fact]
    public void ResolveFromBodyFirstThenCookie_returns_null_when_both_missing()
    {
        Assert.Null(AuthRefreshTokenResolution.ResolveFromBodyFirstThenCookie(null, null));
        Assert.Null(AuthRefreshTokenResolution.ResolveFromBodyFirstThenCookie("  ", ""));
    }
}
