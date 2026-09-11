namespace ITAdmin.Application.Common.Security;

public static class LicenseManagementPermissionRules
{
    private static readonly HashSet<string> PermissionsThatImplyView = new(StringComparer.Ordinal)
    {
        PermissionCodes.LicenseManagement.ManageCatalog,
        PermissionCodes.LicenseManagement.ManagePurchases,
        PermissionCodes.LicenseManagement.ManageRequests,
        PermissionCodes.LicenseManagement.FulfillRequests,
    };

    public static bool IsSatisfiedBy(string requiredPermission, IEnumerable<string> grantedPermissions)
    {
        ArgumentNullException.ThrowIfNull(requiredPermission);
        ArgumentNullException.ThrowIfNull(grantedPermissions);

        if (grantedPermissions.Contains(requiredPermission, StringComparer.Ordinal))
        {
            return true;
        }

        // Operational roles need the module's read screens and lookup endpoints.
        // Their scoped permission therefore implies base View, but settings and
        // sensitive-data permissions never expand into unrelated module access.
        return string.Equals(requiredPermission, PermissionCodes.LicenseManagement.View, StringComparison.Ordinal)
            && grantedPermissions.Any(PermissionsThatImplyView.Contains);
    }
}
