namespace SasPortal.Application.Common.AdManagement;

using SasPortal.Application.Common.Constants;

public static class AdLdapErrorNormalizer
{
    // Common LDAP result codes (RFC 4511).
    private const int LdapAlreadyExists = 68;
    private const int LdapConstraintViolation = 19;
    private const int LdapInvalidDnSyntax = 34;
    private const int LdapInsufficientAccessRights = 50;
    private const int LdapUnwillingToPerform = 53;
    private const int LdapNoSuchObject = 32;
    private const int LdapUnavailable = 52;
    private const int LdapServerDown = 81;
    private const int LdapTimeout = 85;
    private const int LdapConnectError = 91;
    private const int LdapNamingViolation = 64;
    private const int LdapNotAllowedOnNonLeaf = 66;
    private const int LdapNotAllowedOnRdn = 67;
    private const int LdapAttributeOrValueExists = 20;
    private const int LdapConstraintAttributeType = 23;
    private const int LdapOperationsError = 1;
    private const int LdapTimeLimitExceeded = 3;
    private const int LdapBusy = 51;

    public const string UpdateUserFailedMessage = "AD user update failed.";
    public const string UpdateGroupFailedMessage = "AD group update failed.";
    public const string CreateGroupFailedMessage = "AD group create failed.";
    public const string PreflightGroupSamAccountNameDuplicateMessage =
        "Duplicate sAMAccountName detected for another group.";
    public const string PreflightGroupCnDuplicateMessage =
        "A group with the same CN already exists.";
    public const string GroupNotFoundMessage = "AD group not found.";
    public const string DeleteGroupFailedMessage = "AD group delete failed.";
    public const string PreflightSamAccountNameDuplicateMessage =
        "Duplicate sAMAccountName detected for another AD object.";
    public const string PreflightUserPrincipalNameDuplicateMessage =
        "Duplicate UPN detected for another AD object.";
    public const string PreflightCnDuplicateMessage =
        "Duplicate CN detected in the same OU.";
    public const string EntryAlreadyExistsMessage =
        "CN, sAMAccountName, or UPN already exists.";
    public const string ConstraintViolationMessage =
        "One or more fields violate AD constraints.";
    public const string InvalidDnSyntaxMessage =
        "Invalid DN syntax for AD object name.";
    public const string InsufficientAccessRightsMessage =
        "AD service account lacks permission for this operation.";
    public const string UnwillingToPerformMessage =
        "AD refused to perform the requested change.";
    public const string NoSuchObjectMessage = "AD object not found.";
    public const string ConnectionFailedMessage = "AD connection failed.";


    public static string NormalizeMessageKey(int ldapErrorCode, string? diagnosticMessage = null)
    {
        var adNormalizedKey = TryNormalizeKeyFromAdDiagnostic(diagnosticMessage);
        if (adNormalizedKey is not null)
        {
            return adNormalizedKey;
        }

        if (IsConnectionFailure(ldapErrorCode, diagnosticMessage))
        {
            return AdManagementApiMessageKeys.Ldap.ConnectionFailed;
        }

        return ldapErrorCode switch
        {
            LdapAlreadyExists or LdapAttributeOrValueExists => AdManagementApiMessageKeys.Ldap.EntryAlreadyExists,
            LdapConstraintViolation or LdapConstraintAttributeType => AdManagementApiMessageKeys.Ldap.ConstraintViolation,
            LdapInvalidDnSyntax or LdapNamingViolation => AdManagementApiMessageKeys.Ldap.InvalidDnSyntax,
            LdapInsufficientAccessRights => AdManagementApiMessageKeys.Ldap.InsufficientAccessRights,
            LdapUnwillingToPerform or LdapNotAllowedOnNonLeaf or LdapNotAllowedOnRdn => AdManagementApiMessageKeys.Ldap.UnwillingToPerform,
            LdapNoSuchObject => AdManagementApiMessageKeys.Ldap.NoSuchObject,
            LdapUnavailable or LdapBusy or LdapOperationsError or LdapTimeLimitExceeded => AdManagementApiMessageKeys.Ldap.ConnectionFailed,
            _ => MatchesAlreadyExists(diagnosticMessage)
                ? AdManagementApiMessageKeys.Ldap.EntryAlreadyExists
                : MatchesConstraint(diagnosticMessage)
                    ? AdManagementApiMessageKeys.Ldap.ConstraintViolation
                    : MatchesInvalidDn(diagnosticMessage)
                        ? AdManagementApiMessageKeys.Ldap.InvalidDnSyntax
                        : MatchesInsufficientAccess(diagnosticMessage)
                            ? AdManagementApiMessageKeys.Ldap.InsufficientAccessRights
                            : MatchesUnwilling(diagnosticMessage)
                                ? AdManagementApiMessageKeys.Ldap.UnwillingToPerform
                                : MatchesNoSuchObject(diagnosticMessage)
                                    ? AdManagementApiMessageKeys.Ldap.NoSuchObject
                                    : AdManagementApiMessageKeys.Ldap.UpdateUserFailed,
        };
    }

    public static string? TryNormalizeKeyFromAdDiagnostic(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (ContainsAdDiagnostic(message, "0000207D", "name reference is invalid", "invalid dn syntax"))
        {
            return AdManagementApiMessageKeys.Ldap.InvalidDnSyntax;
        }

        if (ContainsAdDiagnostic(message, "0000052D"))
        {
            return AdManagementApiMessageKeys.Ldap.ConstraintViolation;
        }

        if (ContainsAdDiagnostic(message, "00002098", "00002089", "insufficient access rights"))
        {
            return AdManagementApiMessageKeys.Ldap.InsufficientAccessRights;
        }

        if (ContainsAdDiagnostic(
                message,
                "0000208F",
                "00002071",
                "000021C7",
                "entry_exists",
                "entry already exists",
                "attributeorvalueexists",
                "attribute or value exists",
                "already exists",
                "object already exists",
                "constraint violation"))
        {
            return AdManagementApiMessageKeys.Ldap.EntryAlreadyExists;
        }

        if (ContainsAdDiagnostic(message, "00002056", "unwillingtoperform", "unwilling to perform"))
        {
            return AdManagementApiMessageKeys.Ldap.UnwillingToPerform;
        }

        if (ContainsAdDiagnostic(message, "00002030", "nosuchobject", "no such object"))
        {
            return AdManagementApiMessageKeys.Ldap.NoSuchObject;
        }

        return null;
    }


    private static bool IsConnectionFailure(int ldapErrorCode, string? message)
    {
        if (ldapErrorCode is LdapServerDown or LdapUnavailable or LdapTimeout or LdapConnectError)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("server down", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAdDiagnostic(string? message, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (message.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAlreadyExists(string? message) =>
        ContainsAny(
            message,
            "entryalreadyexists",
            "already exists",
            "attributeorvalueexists",
            "attribute or value exists",
            "entry_exists",
            "00002071",
            "000021c7",
            "constraint violation");

    private static bool MatchesConstraint(string? message) =>
        ContainsAny(message, "constraintviolation", "constraint violation", "0000052d");

    private static bool MatchesInvalidDn(string? message) =>
        ContainsAny(message, "invaliddnsyntax", "invalid dn", "namingviolation", "0000207d", "name reference is invalid");

    private static bool MatchesInsufficientAccess(string? message) =>
        ContainsAny(message, "insufficientaccessrights", "insufficient access", "00002098", "00002089");

    private static bool MatchesUnwilling(string? message) =>
        ContainsAny(message, "unwillingtoperform", "unwilling to perform");

    private static bool MatchesNoSuchObject(string? message) =>
        ContainsAny(message, "nosuchobject", "no such object");

    private static bool ContainsAny(string? message, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (message.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
