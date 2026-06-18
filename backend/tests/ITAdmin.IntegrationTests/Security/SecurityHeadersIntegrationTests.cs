using Microsoft.AspNetCore.Mvc.Testing;
using ITAdmin.IntegrationTests.Infrastructure;

namespace ITAdmin.IntegrationTests.Security;

public sealed class SecurityHeadersIntegrationTests : IClassFixture<ITAdminWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersIntegrationTests(ITAdminWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Fact]
    public async Task Responses_carry_baseline_security_headers()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("frame-ancestors 'none'", csp);
    }
}
