using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Contracts.Setup;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.Setup;

public sealed class SetupControllerTests
{
    [Fact]
    public async Task ValidateLdap_WhenSetupAlreadyCompleted_ReturnsForbiddenWithoutCallingLdap()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = false };
        var preflight = new FakeSetupPreflightService();
        var ldap = new FakeLdapService();
        var controller = new SetupController(setup, preflight, ldap);

        var result = await controller.ValidateLdap(CreateValidRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(0, ldap.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdap_WhenSetupRequired_CallsLdapAndReturnsResult()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var preflight = new FakeSetupPreflightService();
        var ldap = new FakeLdapService { ValidateResult = new(true, "LDAP validation succeeded.") };
        var controller = new SetupController(setup, preflight, ldap);

        var result = await controller.ValidateLdap(CreateValidRequest(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ValidateLdapResponse>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Equal(1, ldap.ValidateCallCount);
        Assert.NotNull(ldap.LastValidateRequest);
        Assert.Equal("dc01.test", ldap.LastValidateRequest!.Host);
    }

    [Fact]
    public async Task GetPreflight_WhenSetupAlreadyCompleted_ReturnsForbidden()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = false };
        var preflight = new FakeSetupPreflightService();
        var ldap = new FakeLdapService();
        var controller = new SetupController(setup, preflight, ldap);

        var result = await controller.GetPreflight(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(0, preflight.CheckCallCount);
    }

    [Fact]
    public async Task GetPreflight_WhenSetupRequired_ReturnsChecks()
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
            ]),
        };
        var ldap = new FakeLdapService();
        var controller = new SetupController(setup, preflight, ldap);

        var result = await controller.GetPreflight(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SetupPreflightResponse>(okResult.Value);
        Assert.Single(response.Checks);
        Assert.Equal(SetupPreflightCheckKeys.JwtKeyConfigured, response.Checks[0].Key);
        Assert.Equal(1, preflight.CheckCallCount);
    }

    private static ValidateLdapRequest CreateValidRequest() => new()
    {
        Host = "dc01.test",
        BaseDn = "DC=test,DC=local",
        UserSearchBase = string.Empty,
        UserSearchFilter = "(&(objectClass=user)(sAMAccountName={0}))",
        BindUserName = "bind",
        BindUserDomain = null,
        BindPassword = "bindpw",
        TestUserName = "admin",
        TestPassword = "adminpw",
    };
}
