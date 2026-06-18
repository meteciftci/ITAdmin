using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Contracts.Setup;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.Models;
using ITAdmin.UnitTests.Fakes;
using ApiSearchSetupOrganizationalUnitsRequest = ITAdmin.Api.Contracts.Setup.SearchSetupOrganizationalUnitsRequest;

namespace ITAdmin.UnitTests.Setup;

public sealed class SetupOrganizationalUnitsControllerTests
{
    [Fact]
    public async Task SearchOrganizationalUnits_WhenSetupAlreadyCompleted_ReturnsForbidden()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = false };
        var controller = new SetupController(setup, new FakeSetupPreflightService());

        var result = await controller.SearchOrganizationalUnits(CreateValidRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(0, setup.SearchOrganizationalUnitsCallCount);
    }

    [Fact]
    public async Task SearchOrganizationalUnits_WhenSetupRequired_DelegatesToSetupService()
    {
        var setup = new FakeSetupService
        {
            IsSetupRequiredResult = true,
            SearchOrganizationalUnitsResult = new SearchSetupOrganizationalUnitsResult(
            [
                new SetupOrganizationalUnitListItem(
                    "OU=Users,DC=test,DC=local",
                    "Users",
                    "Users",
                    "Users",
                    "Users"),
            ],
            false),
        };
        var controller = new SetupController(setup, new FakeSetupPreflightService());

        var result = await controller.SearchOrganizationalUnits(CreateValidRequest(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SearchSetupOrganizationalUnitsResponse>(okResult.Value);
        Assert.Single(response.Items);
        Assert.Equal(1, setup.SearchOrganizationalUnitsCallCount);
    }

    [Fact]
    public async Task SearchOrganizationalUnits_WhenRequestBodyNull_ReturnsBadRequest()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var controller = new SetupController(setup, new FakeSetupPreflightService());

        var result = await controller.SearchOrganizationalUnits(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, setup.SearchOrganizationalUnitsCallCount);
    }

    private static ApiSearchSetupOrganizationalUnitsRequest CreateValidRequest() => new(
        "setup-secret",
        new CompleteSetupLdapSettingsRequest(
            "Default LDAP",
            "dc01.test",
            "DC=test,DC=local",
            "(&(objectClass=user)(sAMAccountName={0}))",
            "bind",
            null,
            "bindpw"),
        null,
        null);
}
