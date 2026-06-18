using ITAdmin.Api.Contracts.Auth;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Api.Security;

/// <summary>
/// Maps an <see cref="AuthTokenResult"/> to the client-facing login response.
/// Detailed failure reasons (missing LDAP settings, passive user, missing portal authorization,
/// profile load failures, user name conflicts, ...) stay in the SecurityLog only; the client
/// receives a generic message so the login endpoint does not leak account or system state.
/// </summary>
public static class LoginClientResponse
{
    public const string ServiceUnavailableErrorCode = "ServiceUnavailable";
    public const string LoginErrorCode = "LoginError";

    public const string GenericInvalidCredentialsMessage = "Invalid user name or password.";
    public const string GenericServiceUnavailableMessage = "Authentication service is temporarily unavailable.";
    public const string GenericLoginErrorMessage = "Login could not be completed.";

    public static LoginResponse Create(AuthTokenResult result)
    {
        if (result.IsSuccess)
        {
            return new LoginResponse(true, result.Message, result.ErrorCode);
        }

        if (string.Equals(result.ErrorCode, ServiceUnavailableErrorCode, StringComparison.Ordinal))
        {
            return new LoginResponse(false, GenericServiceUnavailableMessage, ServiceUnavailableErrorCode);
        }

        if (string.Equals(result.ErrorCode, LoginErrorCode, StringComparison.Ordinal))
        {
            return new LoginResponse(false, GenericLoginErrorMessage, LoginErrorCode);
        }

        return new LoginResponse(false, GenericInvalidCredentialsMessage);
    }
}
