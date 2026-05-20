namespace SasPortal.Application.Common.AdManagement;

public static class AdOrganizationalUnitLabelBuilder
{
    public static string Build(
        string distinguishedName,
        string? displayName,
        string? name,
        string? ou)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ou))
        {
            return ou.Trim();
        }

        var parsedOu = ParseOuNameFromDistinguishedName(distinguishedName);
        if (!string.IsNullOrWhiteSpace(parsedOu))
        {
            return parsedOu;
        }

        return distinguishedName.Trim();
    }

    internal static string? ParseOuNameFromDistinguishedName(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return null;
        }

        foreach (var component in AdLdapDnHelper.SplitDnComponents(distinguishedName.Trim()))
        {
            var trimmed = component.Trim();
            if (trimmed.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            {
                var value = AdLdapDnHelper.UnescapeDnValue(trimmed[3..]);
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }
}
