namespace ITAdmin.Application.Common.AdManagement;

public static class AdOrganizationalUnitGuard
{
    private static readonly string[] ProtectedContainerCommonNames =
    [
        "Builtin",
        "Computers",
        "ForeignSecurityPrincipals",
        "LostAndFound",
        "Managed Service Accounts",
        "Program Data",
        "System",
        "Users",
    ];

    public static bool IsDomainNamingContext(string? distinguishedName) =>
        AdLdapDnHelper.IsDomainNamingContext(distinguishedName);

    public static bool IsManagedOrganizationalUnit(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        if (!AdLdapDnHelper.IsOrganizationalUnitDistinguishedName(distinguishedName))
        {
            return false;
        }

        return !IsProtectedDistinguishedName(distinguishedName);
    }

    public static bool IsProtectedDistinguishedName(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return true;
        }

        var normalized = distinguishedName.Trim();
        if (IsDomainNamingContext(normalized))
        {
            return true;
        }

        if (IsProtectedContainer(normalized))
        {
            return true;
        }

        return IsWellKnownProtectedOu(normalized);
    }

    public static bool IsConfiguredRootProtected(
        string? distinguishedName,
        string? baseDn,
        string? defaultNamingContext)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return true;
        }

        var normalized = distinguishedName.Trim();
        if (!string.IsNullOrWhiteSpace(baseDn)
            && AdLdapDnHelper.AreDistinguishedNamesEqual(normalized, baseDn))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(defaultNamingContext)
            && AdLdapDnHelper.AreDistinguishedNamesEqual(normalized, defaultNamingContext))
        {
            return true;
        }

        return false;
    }

    public static bool IsInvalidMoveTarget(
        string sourceDistinguishedName,
        string targetParentDistinguishedName)
    {
        if (AdLdapDnHelper.AreDistinguishedNamesEqual(sourceDistinguishedName, targetParentDistinguishedName))
        {
            return true;
        }

        return AdLdapDnHelper.IsEqualOrDescendantOf(targetParentDistinguishedName, sourceDistinguishedName);
    }

    public static bool IsValidParentCandidate(string? distinguishedName) =>
        !string.IsNullOrWhiteSpace(distinguishedName)
        && (IsDomainNamingContext(distinguishedName) || IsManagedOrganizationalUnit(distinguishedName));

    private static bool IsProtectedContainer(string distinguishedName)
    {
        var relativeName = AdLdapDnHelper.GetRelativeDistinguishedName(distinguishedName);
        if (string.IsNullOrWhiteSpace(relativeName)
            || !relativeName.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commonName = AdLdapDnHelper.UnescapeDnValue(relativeName[3..]);
        return ProtectedContainerCommonNames.Contains(commonName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsWellKnownProtectedOu(string distinguishedName)
    {
        var relativeName = AdLdapDnHelper.GetRelativeDistinguishedName(distinguishedName);
        if (string.IsNullOrWhiteSpace(relativeName)
            || !relativeName.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ouName = AdLdapDnHelper.UnescapeDnValue(relativeName[3..]);
        return string.Equals(ouName, "Domain Controllers", StringComparison.OrdinalIgnoreCase);
    }
}
