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

    public static string? ConvertNamingContextToDnsSuffix(string? namingContext)
    {
        if (string.IsNullOrWhiteSpace(namingContext))
        {
            return null;
        }

        var labels = new List<string>();
        foreach (var component in SplitDnComponents(namingContext.Trim()))
        {
            var trimmed = component.Trim();
            if (!trimmed.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = UnescapeDnValue(trimmed[3..]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                labels.Add(value);
            }
        }

        return labels.Count == 0 ? null : string.Join('.', labels).ToLowerInvariant();
    }

    public static bool IsEqualOrDescendantOf(string? childDistinguishedName, string? ancestorDistinguishedName)
    {
        if (string.IsNullOrWhiteSpace(childDistinguishedName) || string.IsNullOrWhiteSpace(ancestorDistinguishedName))
        {
            return false;
        }

        var child = NormalizeDn(childDistinguishedName);
        var ancestor = NormalizeDn(ancestorDistinguishedName);
        return child.Equals(ancestor, StringComparison.OrdinalIgnoreCase)
            || child.EndsWith($",{ancestor}", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildUserDistinguishedName(string commonName, string parentDistinguishedName)
    {
        var escapedCn = EscapeDnComponent(commonName);
        return $"CN={escapedCn},{parentDistinguishedName.Trim()}";
    }

    public static string EscapeDnComponent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var requiresQuotes = value.Any(static ch => ",=+<>#;\"\\".Contains(ch))
            || value.StartsWith(' ')
            || value.EndsWith(' ');

        var builder = new StringBuilder(value.Length + 8);
        if (requiresQuotes)
        {
            builder.Append('"');
        }

        foreach (var character in value)
        {
            if (",=+<>#;\"\\".Contains(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        if (requiresQuotes)
        {
            builder.Append('"');
        }

        return builder.ToString();
    }

    private static string NormalizeDn(string distinguishedName) =>
        string.Join(
            ",",
            SplitDnComponents(distinguishedName.Trim())
                .Select(static component => component.Trim()));

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
