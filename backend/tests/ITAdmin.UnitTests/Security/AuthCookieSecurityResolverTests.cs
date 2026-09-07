using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ITAdmin.Api.Security;

namespace ITAdmin.UnitTests.Security;

public sealed class AuthCookieSecurityResolverTests
{
    [Theory]
    [InlineData(true, "Production", true)]
    [InlineData(true, "Development", true)]
    [InlineData(true, null, true)]
    [InlineData(false, "Production", true)]
    [InlineData(false, "production", true)]
    [InlineData(false, "Development", false)]
    [InlineData(false, "Testing", false)]
    [InlineData(false, "Staging", false)]
    [InlineData(false, null, false)]
    public void ResolveSecure_combines_https_and_environment(
        bool isHttps,
        string? environmentName,
        bool expectedSecure)
    {
        // Steady state: HTTPS has been configured (httpsConfigured defaults to true).
        Assert.Equal(expectedSecure, AuthCookieSecurityResolver.ResolveSecure(isHttps, environmentName));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ResolveSecure_production_http_follows_whether_https_is_configured(
        bool httpsConfigured,
        bool expectedSecure)
    {
        Assert.Equal(
            expectedSecure,
            AuthCookieSecurityResolver.ResolveSecure(false, Environments.Production, httpsConfigured));

        // An HTTPS request is always Secure regardless of the commissioning flag.
        Assert.True(
            AuthCookieSecurityResolver.ResolveSecure(true, Environments.Production, httpsConfigured));
    }

    [Fact]
    public void ResolveSecure_from_request_without_services_falls_back_to_request_scheme()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.IsHttps = false;

        Assert.False(AuthCookieSecurityResolver.ResolveSecure(ctx.Request));

        ctx.Request.IsHttps = true;
        Assert.True(AuthCookieSecurityResolver.ResolveSecure(ctx.Request));
    }

    [Fact]
    public void ResolveSecure_http_production_is_not_secure_during_the_commissioning_window()
    {
        // Fresh install: production, HTTP, no Https:* configuration yet.
        var ctx = CreateHttpContextForEnvironment(Environments.Production);
        ctx.Request.IsHttps = false;

        Assert.False(AuthCookieSecurityResolver.ResolveSecure(ctx.Request));
    }

    [Fact]
    public void ResolveSecure_http_production_is_secure_once_https_is_configured()
    {
        var ctx = CreateHttpContextForEnvironment(
            Environments.Production,
            ("Https:Enabled", "true"));
        ctx.Request.IsHttps = false;

        Assert.True(AuthCookieSecurityResolver.ResolveSecure(ctx.Request));
    }

    [Fact]
    public void ResolveSecure_is_false_for_http_request_in_development_environment()
    {
        var ctx = CreateHttpContextForEnvironment(Environments.Development);
        ctx.Request.IsHttps = false;

        Assert.False(AuthCookieSecurityResolver.ResolveSecure(ctx.Request));
    }

    internal static DefaultHttpContext CreateHttpContextForEnvironment(
        string environmentName,
        params (string Key, string Value)[] configuration)
    {
        var services = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environmentName))
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(configuration.ToDictionary(
                    pair => pair.Key, pair => (string?)pair.Value))
                .Build())
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
        };
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "ITAdmin.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
