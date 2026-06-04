using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdLdapUserSearchBases
{
    public static IReadOnlyList<string> ResolveDistinctSearchBases(AdManagementConnectionParameters connection)
    {
        var bases = new List<string>();

        AddDistinct(bases, connection.DefaultNamingContext);
        AddDistinct(bases, connection.BaseDn);
        AddDistinct(bases, connection.UsersRootOu);
        AddDistinct(bases, connection.DisabledUsersOu);

        return bases;
    }

    private static void AddDistinct(List<string> bases, string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return;
        }

        var trimmed = distinguishedName.Trim();
        if (bases.Any(existing => AdLdapDnHelper.AreDistinguishedNamesEqual(existing, trimmed)))
        {
            return;
        }

        bases.Add(trimmed);
    }
}
