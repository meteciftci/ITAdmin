using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using ITAdmin.IntegrationTests.Infrastructure;

namespace ITAdmin.IntegrationTests.Health;

public sealed class HealthEndpointTests : IClassFixture<ITAdminWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ITAdminWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
