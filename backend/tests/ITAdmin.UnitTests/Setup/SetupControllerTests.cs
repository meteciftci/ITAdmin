using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Contracts.Setup;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.UnitTests.Fakes;
using ApiCompleteSetupRequest = ITAdmin.Api.Contracts.Setup.CompleteSetupRequest;
using ApiSearchSetupAdminUsersRequest = ITAdmin.Api.Contracts.Setup.SearchSetupAdminUsersRequest;

namespace ITAdmin.UnitTests.Setup;

public sealed class SetupControllerTests
{
    [Fact]
    public async Task ValidateLdap_WhenSetupAlreadyCompleted_ReturnsForbidden()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = false };
        var preflight = new FakeSetupPreflightService();
        var controller = new SetupController(setup, preflight);

        var result = await controller.ValidateLdap(CreateValidValidateRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(0, setup.ValidateLdapCallCount);
    }

    [Fact]
    public async Task ValidateLdap_WhenSetupRequired_DelegatesToSetupService()
    {
        var setup = new FakeSetupService
        {
            IsSetupRequiredResult = true,
            ValidateLdapResult = new(true, "LDAP validation succeeded."),
        };
        var preflight = new FakeSetupPreflightService();
        var controller = new SetupController(setup, preflight);

        var result = await controller.ValidateLdap(CreateValidValidateRequest(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ValidateLdapResponse>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Equal(1, setup.ValidateLdapCallCount);
        Assert.Equal("setup-secret", setup.LastValidateLdapRequest!.SetupKey);
    }

    [Fact]
    public async Task GetPreflight_WhenSetupAlreadyCompleted_ReturnsForbidden()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = false };
        var preflight = new FakeSetupPreflightService();
        var controller = new SetupController(setup, preflight);

        var result = await controller.GetPreflight(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(0, preflight.CheckCallCount);
    }

    [Fact]
    public async Task GetPreflight_WhenSetupRequired_ReturnsChecksAndCanContinue()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var preflight = new FakeSetupPreflightService
        {
            Result = new SetupPreflightResult([
                new SetupPreflightCheck(
                    SetupPreflightCheckKeys.JwtKeyConfigured,
                    SetupPreflightCheckStatuses.Ok,
                    SetupPreflightMessageKeys.JwtKeyConfigured,
                    null),
            ], true),
        };
        var controller = new SetupController(setup, preflight);

        var result = await controller.GetPreflight(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SetupPreflightResponse>(okResult.Value);
        Assert.Single(response.Checks);
        Assert.True(response.CanContinue);
    }

    [Fact]
    public async Task SearchAdminUsers_WhenSetupAlreadyCompleted_ReturnsForbidden()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = false };
        var preflight = new FakeSetupPreflightService();
        var controller = new SetupController(setup, preflight);

        var result = await controller.SearchAdminUsers(CreateValidSearchRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(0, setup.SearchAdminUsersCallCount);
    }

    [Fact]
    public async Task SearchAdminUsers_WhenInvalidSetupKey_ReturnsBadRequest()
    {
        var setup = new FakeSetupService
        {
            IsSetupRequiredResult = true,
            SearchAdminUsersResult = new([], "Invalid setup key."),
        };
        var preflight = new FakeSetupPreflightService();
        var controller = new SetupController(setup, preflight);

        var result = await controller.SearchAdminUsers(CreateValidSearchRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CompleteSetup_WhenRequestBodyNull_ReturnsBadRequest()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var controller = new SetupController(setup, new FakeSetupPreflightService());

        var result = await controller.CompleteSetup(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, setup.CompleteSetupCallCount);
        Assert.Contains("invalidRequestBody", badRequest.Value!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteSetup_WhenLdapNull_ReturnsBadRequest()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var controller = new SetupController(setup, new FakeSetupPreflightService());

        var result = await controller.CompleteSetup(
            new ApiCompleteSetupRequest(
                "setup-secret",
                null!,
                new CompleteSetupModulesRequest(null),
                [new CompleteSetupAdminUserRequest("admin", null, null)]),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, setup.CompleteSetupCallCount);
    }

    [Fact]
    public async Task SearchAdminUsers_WhenRequestBodyNull_ReturnsBadRequest()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var controller = new SetupController(setup, new FakeSetupPreflightService());

        var result = await controller.SearchAdminUsers(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, setup.SearchAdminUsersCallCount);
    }

    [Fact]
    public async Task ValidateLdap_WhenRequestBodyNull_ReturnsBadRequest()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var controller = new SetupController(setup, new FakeSetupPreflightService());

        var result = await controller.ValidateLdap(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, setup.ValidateLdapCallCount);
    }

    private static ValidateLdapRequest CreateValidValidateRequest() => new()
    {
        SetupKey = "setup-secret",
        Host = "dc01.test",
        BaseDn = "DC=test,DC=local",
        UserSearchFilter = "(&(objectClass=user)(sAMAccountName={0}))",
        BindUserName = "bind",
        BindUserDomain = null,
        BindPassword = "bindpw",
    };

    private static ApiSearchSetupAdminUsersRequest CreateValidSearchRequest() => new(
        "setup-secret",
        new CompleteSetupLdapSettingsRequest(
            "Default LDAP",
            "dc01.test",
            "DC=test,DC=local",
            "(&(objectClass=user)(sAMAccountName={0}))",
            "bind",
            null,
            "bindpw"),
        "admin");
}
