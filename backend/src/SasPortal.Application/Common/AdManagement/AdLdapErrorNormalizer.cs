namespace SasPortal.Application.Common.AdManagement;

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

    public const string UpdateUserFailedMessage = "AD kullanıcısı güncellenemedi.";
    public const string EntryAlreadyExistsMessage =
        "Bu CN, kullanıcı adı veya UPN başka bir AD nesnesi tarafından kullanılıyor.";
    public const string ConstraintViolationMessage =
        "Girilen alanlardan biri AD kurallarına uygun değil.";
    public const string InvalidDnSyntaxMessage =
        "Kullanıcı adı veya görünen ad AD nesne adı için geçerli değil.";
    public const string InsufficientAccessRightsMessage =
        "AD servis hesabının bu işlem için yetkisi yok.";
    public const string UnwillingToPerformMessage =
        "AD bu değişikliği kabul etmedi. Alan kısıtları veya domain politikaları nedeniyle işlem yapılamıyor.";
    public const string NoSuchObjectMessage = "AD kullanıcısı bulunamadı.";
    public const string ConnectionFailedMessage = "AD bağlantısı kurulamadı.";

    public static string Normalize(int ldapErrorCode, string? diagnosticMessage = null)
    {
        if (IsConnectionFailure(ldapErrorCode, diagnosticMessage))
        {
            return ConnectionFailedMessage;
        }

        return ldapErrorCode switch
        {
            LdapAlreadyExists or LdapAttributeOrValueExists => EntryAlreadyExistsMessage,
            LdapConstraintViolation or LdapConstraintAttributeType => ConstraintViolationMessage,
            LdapInvalidDnSyntax or LdapNamingViolation => InvalidDnSyntaxMessage,
            LdapInsufficientAccessRights => InsufficientAccessRightsMessage,
            LdapUnwillingToPerform or LdapNotAllowedOnNonLeaf or LdapNotAllowedOnRdn => UnwillingToPerformMessage,
            LdapNoSuchObject => NoSuchObjectMessage,
            LdapUnavailable or LdapBusy or LdapOperationsError or LdapTimeLimitExceeded => ConnectionFailedMessage,
            _ => MatchesAlreadyExists(diagnosticMessage)
                ? EntryAlreadyExistsMessage
                : MatchesConstraint(diagnosticMessage)
                    ? ConstraintViolationMessage
                    : MatchesInvalidDn(diagnosticMessage)
                        ? InvalidDnSyntaxMessage
                        : MatchesInsufficientAccess(diagnosticMessage)
                            ? InsufficientAccessRightsMessage
                            : MatchesUnwilling(diagnosticMessage)
                                ? UnwillingToPerformMessage
                                : MatchesNoSuchObject(diagnosticMessage)
                                    ? NoSuchObjectMessage
                                    : UpdateUserFailedMessage,
        };
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

    private static bool MatchesAlreadyExists(string? message) =>
        ContainsAny(message, "entryalreadyexists", "already exists", "attributeorvalueexists");

    private static bool MatchesConstraint(string? message) =>
        ContainsAny(message, "constraintviolation", "constraint violation");

    private static bool MatchesInvalidDn(string? message) =>
        ContainsAny(message, "invaliddnsyntax", "invalid dn", "namingviolation");

    private static bool MatchesInsufficientAccess(string? message) =>
        ContainsAny(message, "insufficientaccessrights", "insufficient access");

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
