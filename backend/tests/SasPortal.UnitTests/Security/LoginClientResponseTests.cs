using SasPortal.Api.Security;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.Security;

public sealed class LoginClientResponseTests
{
    [Theory]
    [InlineData("LDAP settings are not configured.")]
    [InlineData("User is inactive.")]
    [InlineData("User is not authorized to access the portal.")]
    [InlineData("Directory user profile could not be loaded.")]
    [InlineData("Another portal user already uses this user name.")]
    [InlineData("Directory user authentication failed.")]
    public void Create_replaces_detailed_failure_reasons_with_generic_message(string detailedMessage)
    {
        var result = new AuthTokenResult(false, detailedMessage, null, null, null, null);

        var response = LoginClientResponse.Create(result);

        Assert.False(response.IsSuccess);
        Assert.Equal(LoginClientResponse.GenericInvalidCredentialsMessage, response.Message);
        Assert.Null(response.ErrorCode);
        Assert.DoesNotContain(detailedMessage, response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_keeps_service_unavailable_error_code_with_generic_message()
    {
        var result = new AuthTokenResult(
            false,
            "Authentication service is temporarily unavailable.",
            null,
            null,
            null,
            null,
            "ServiceUnavailable");

        var response = LoginClientResponse.Create(result);

        Assert.False(response.IsSuccess);
        Assert.Equal("ServiceUnavailable", response.ErrorCode);
        Assert.Equal(LoginClientResponse.GenericServiceUnavailableMessage, response.Message);
    }

    [Fact]
    public void Create_keeps_login_error_code_with_generic_message()
    {
        var result = new AuthTokenResult(false, "Login could not be completed.", null, null, null, null, "LoginError");

        var response = LoginClientResponse.Create(result);

        Assert.False(response.IsSuccess);
        Assert.Equal("LoginError", response.ErrorCode);
        Assert.Equal(LoginClientResponse.GenericLoginErrorMessage, response.Message);
    }

    [Fact]
    public void Create_passes_success_response_through()
    {
        var result = new AuthTokenResult(true, "Login succeeded.", "access", "refresh", DateTime.UtcNow, DateTime.UtcNow);

        var response = LoginClientResponse.Create(result);

        Assert.True(response.IsSuccess);
        Assert.Equal("Login succeeded.", response.Message);
        Assert.Null(response.ErrorCode);
    }
}
