using System.Text;
using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdLdapDnHelper
{
    public static string? ParseCommonNameFromDistinguishedName(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return null;
        }

        foreach (var component in SplitDnComponents(distinguishedName.Trim()))
        {
            var trimmed = component.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                var value = UnescapeDnValue(trimmed[3..]);
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        var segments = SplitDnComponents(distinguishedName.Trim());
        if (segments.Length == 0)
        {
            return distinguishedName.Trim();
        }

        var fallback = UnescapeDnValue(segments[0].Trim());
        return string.IsNullOrWhiteSpace(fallback) ? distinguishedName.Trim() : fallback;
    }

    public static IReadOnlyList<AdUserGroupMembership> BuildGroupMemberships(
        IEnumerable<string> memberOfValues)
    {
        var seenDns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var memberships = new List<AdUserGroupMembership>();

        foreach (var rawDn in memberOfValues)
        {
            if (string.IsNullOrWhiteSpace(rawDn))
            {
                continue;
            }

            var distinguishedName = rawDn.Trim();
            if (!seenDns.Add(distinguishedName))
            {
                continue;
            }

            var name = ParseCommonNameFromDistinguishedName(distinguishedName) ?? distinguishedName;
            memberships.Add(new AdUserGroupMembership(name, distinguishedName));
        }

        return memberships
            .OrderBy(static membership => membership.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string[] SplitDnComponents(string distinguishedName)
    {
        var parts = new List<string>();
        var current = new StringBuilder();

        for (var index = 0; index < distinguishedName.Length; index++)
        {
            var character = distinguishedName[index];
            if (character == '\\' && index + 1 < distinguishedName.Length)
            {
                current.Append(distinguishedName[++index]);
                continue;
            }

            if (character == ',')
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts.ToArray();
    }

    internal static string UnescapeDnValue(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\\' && index + 1 < value.Length)
            {
                builder.Append(value[++index]);
                continue;
            }

            builder.Append(value[index]);
        }

        return builder.ToString().Trim();
    }
}
