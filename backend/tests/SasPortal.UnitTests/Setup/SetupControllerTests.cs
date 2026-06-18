using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Contracts.Setup;
using SasPortal.Api.Controllers;
using SasPortal.UnitTests.Fakes;

namespace SasPortal.UnitTests.Setup;

public sealed class SetupControllerTests
{
    [Fact]
    public async Task ValidateLdap_WhenSetupAlreadyCompleted_ReturnsForbiddenWithoutCallingLdap()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = false };
        var ldap = new FakeLdapService();
        var controller = new SetupController(setup, ldap);

        var result = await controller.ValidateLdap(CreateValidRequest(useSsl: true), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(0, ldap.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdap_WhenUseSslDisabled_ReturnsBadRequestWithoutCallingLdap()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var ldap = new FakeLdapService();
        var controller = new SetupController(setup, ldap);

        var result = await controller.ValidateLdap(CreateValidRequest(useSsl: false), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, ldap.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateLdap_WhenSetupRequiredAndSecure_CallsLdapAndReturnsResult()
    {
        var setup = new FakeSetupService { IsSetupRequiredResult = true };
        var ldap = new FakeLdapService { ValidateResult = new(true, "LDAP validation succeeded.") };
        var controller = new SetupController(setup, ldap);

        var result = await controller.ValidateLdap(CreateValidRequest(useSsl: true), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ValidateLdapResponse>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Equal(1, ldap.ValidateCallCount);
        Assert.NotNull(ldap.LastValidateRequest);
        Assert.True(ldap.LastValidateRequest!.UseSsl);
    }

    private static ValidateLdapRequest CreateValidRequest(bool useSsl) => new()
    {
        Host = "dc01.test",
        Port = 636,
        UseSsl = useSsl,
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
