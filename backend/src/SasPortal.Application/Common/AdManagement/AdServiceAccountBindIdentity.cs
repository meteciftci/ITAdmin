namespace SasPortal.Application.Common.AdManagement;

/// <summary>
/// Resolves the effective LDAP bind identity for the AD management service account.
/// Accepts the user-entered service account user name in any of the following formats:
///   - DOMAIN\username  -> used as-is
///   - username@domain.local -> used as-is
///   - username -> combined with NetBIOS domain as DOMAIN\username when available
/// </summary>
public static class AdServiceAccountBindIdentity
{
    /// <summary>
    /// Builds the bind identity that should be passed to LDAP based on the user-entered
    /// service account name and the optional NetBIOS domain. Returns an empty string when
    /// the user name itself is empty.
    /// </summary>
    public static string Build(string? userName, string? netbiosDomainName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return string.Empty;
        }

        var normalizedUser = userName.Trim();

        if (normalizedUser.Contains('\\', System.StringComparison.Ordinal)
            || normalizedUser.Contains('@', System.StringComparison.Ordinal))
        {
            return normalizedUser;
        }

        if (string.IsNullOrWhiteSpace(netbiosDomainName))
        {
            return normalizedUser;
        }

        var normalizedDomain = netbiosDomainName.Trim();
        return $"{normalizedDomain}\\{normalizedUser}";
    }
}
