using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using ITAdmin.IntegrationTests.Infrastructure;

namespace ITAdmin.IntegrationTests.Security;

/// <summary>
/// Broad guard rail asserting that permission-sensitive API endpoints across the main feature
/// areas are protected: an unauthenticated request is rejected with 401 (not served, and not a
/// 404) before any controller or database work runs. Complements
/// <see cref="AdManagementAuthorizationIntegrationTests"/> which covers the AD controllers.
/// </summary>
public sealed class EndpointAuthorizationIntegrationTests
    : IClassFixture<ITAdminWebApplicationFactory>
{
    private const string SampleGuid = "00000000-0000-0000-0000-000000000000";
    private readonly HttpClient _client;

    public EndpointAuthorizationIntegrationTests(ITAdminWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Theory]
    [InlineData("/api/users/lookup-directory")]
    [InlineData($"/api/roles/{SampleGuid}")]
    [InlineData($"/api/permissions/{SampleGuid}")]
    [InlineData("/api/license-management/requests")]
    [InlineData($"/api/license-management/products/{SampleGuid}")]
    [InlineData("/api/license-management/directory-user-lookup/readiness")]
    [InlineData("/api/notification-providers/sms")]
    [InlineData("/api/audit-logs/filter-options")]
    [InlineData("/api/security-logs/filter-options")]
    [InlineData($"/api/ad-management/operation-logs/{SampleGuid}")]
    public async Task Protected_endpoints_reject_unauthenticated_requests(string route)
    {
        var response = await _client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
