using System.DirectoryServices.Protocols;

namespace SasPortal.Infrastructure.Services;

/// <summary>
/// Maps <see cref="LdapException"/> from service-account bind attempts to user-facing validation messages.
/// </summary>
internal static class LdapBindFailureMessageResolver
{
    internal const string ServiceAccountBindFailedMessage = "LDAP service account authentication failed.";
    internal const string ServerConnectionFailedMessage = "LDAP server connection failed.";
    internal const string ValidationFailedMessage = "LDAP validation failed.";

    /// <summary>LDAP_INAPPROPRIATE_AUTH (48).</summary>
    private const int InappropriateAuthentication = 48;

    /// <summary>LDAP_INVALID_CREDENTIALS (49).</summary>
    private const int InvalidCredentials = 49;

    /// <summary>WinLDAP client-side result codes (not always exposed as <see cref="ResultCode"/> members).</summary>
    private const int LdapServerDown = 0x51; // 81
    private const int LdapTimeout = 0x55; // 85
    private const int LdapConnectError = 0x5B; // 91

    internal static string ResolveForServiceAccountBind(LdapException exception)
    {
        var code = exception.ErrorCode;

        if (code is InappropriateAuthentication or InvalidCredentials)
        {
            return ServiceAccountBindFailedMessage;
        }

        if (IsLikelyConnectionFailure(code))
        {
            return ServerConnectionFailedMessage;
        }

        return ValidationFailedMessage;
    }

    private static bool IsLikelyConnectionFailure(int code)
    {
        if (code is LdapServerDown or LdapTimeout or LdapConnectError)
        {
            return true;
        }

        return code switch
        {
            (int)ResultCode.Busy => true,
            (int)ResultCode.Unavailable => true,
            (int)ResultCode.StrongAuthRequired => true,
            (int)ResultCode.ConfidentialityRequired => true,
            (int)ResultCode.AuthMethodNotSupported => true,
            _ => false
        };
    }
}
