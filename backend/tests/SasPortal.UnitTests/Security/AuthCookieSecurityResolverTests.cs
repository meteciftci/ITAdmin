using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SasPortal.Api.Security;

namespace SasPortal.UnitTests.Security;

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
        Assert.Equal(expectedSecure, AuthCookieSecurityResolver.ResolveSecure(isHttps, environmentName));
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
    public void ResolveSecure_is_true_for_http_request_in_production_environment()
    {
        var ctx = CreateHttpContextForEnvironment(Environments.Production);
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

    internal static DefaultHttpContext CreateHttpContextForEnvironment(string environmentName)
    {
        var services = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environmentName))
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
        };
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "SasPortal.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
