using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ITAdmin.IntegrationTests.Infrastructure;

namespace ITAdmin.IntegrationTests.Security;

/// <summary>
/// The AD management endpoints are split across six domain controllers that all share the
/// <c>api/ad-management</c> route prefix and require authentication. These tests assert that a
/// representative endpoint on every controller is both routed (not 404) and protected: an
/// unauthenticated request is rejected with 401 before any controller/database work runs.
/// </summary>
public sealed class AdManagementAuthorizationIntegrationTests
    : IClassFixture<ITAdminWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdManagementAuthorizationIntegrationTests(ITAdminWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Theory]
    // AdUsersController
    [InlineData("/api/ad-management/users")]
    [InlineData("/api/ad-management/upn-suffixes")]
    // AdGroupsController
    [InlineData("/api/ad-management/groups")]
    [InlineData("/api/ad-management/group-organizational-units")]
    // AdComputersController
    [InlineData("/api/ad-management/computers")]
    [InlineData("/api/ad-management/computer-operating-systems")]
    // AdOrganizationalUnitsController
    [InlineData("/api/ad-management/organizational-units/manage")]
    // AdDeletedObjectsController
    [InlineData("/api/ad-management/deleted-objects")]
    [InlineData("/api/ad-management/deleted-objects/restore-readiness")]
    // AdManagementSettingsController
    [InlineData("/api/ad-management/settings")]
    [InlineData("/api/ad-management/attribute-mappings")]
    public async Task Get_endpoints_require_authentication(string route)
    {
        var response = await _client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    // AdUsersController
    [InlineData("/api/ad-management/users")]
    [InlineData("/api/ad-management/users/00000000-0000-0000-0000-000000000000/enable")]
    // AdGroupsController
    [InlineData("/api/ad-management/groups")]
    // AdComputersController
    [InlineData("/api/ad-management/computers/00000000-0000-0000-0000-000000000000/enable")]
    // AdOrganizationalUnitsController
    [InlineData("/api/ad-management/organizational-units")]
    // AdManagementSettingsController
    [InlineData("/api/ad-management/settings/validate")]
    public async Task Post_endpoints_require_authentication(string route)
    {
        var response = await _client.PostAsJsonAsync(route, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
