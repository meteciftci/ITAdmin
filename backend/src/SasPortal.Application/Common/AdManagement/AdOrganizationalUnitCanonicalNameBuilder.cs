namespace SasPortal.Application.Common.AdManagement;

public static class AdOrganizationalUnitCanonicalNameBuilder
{
    public static string Build(string distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return string.Empty;
        }

        var components = AdLdapDnHelper.SplitDnComponents(distinguishedName.Trim());
        var domainLabels = new List<string>();
        var organizationalUnitLabels = new List<string>();

        foreach (var component in components)
        {
            var trimmed = component.Trim();
            if (trimmed.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
            {
                var value = AdLdapDnHelper.UnescapeDnValue(trimmed[3..]);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    domainLabels.Add(value);
                }

                continue;
            }

            if (trimmed.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            {
                var value = AdLdapDnHelper.UnescapeDnValue(trimmed[3..]);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    organizationalUnitLabels.Add(value);
                }
            }
        }

        if (domainLabels.Count == 0)
        {
            return distinguishedName.Trim();
        }

        organizationalUnitLabels.Reverse();
        var domain = string.Join('.', domainLabels).ToLowerInvariant();
        return organizationalUnitLabels.Count == 0
            ? domain
            : $"{domain}/{string.Join('/', organizationalUnitLabels)}";
    }
}
