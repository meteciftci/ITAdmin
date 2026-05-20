using System.Text.RegularExpressions;

namespace SasPortal.Application.Common.AdManagement;

public static partial class AdDefaultUpnSuffixNormalizer
{
    [GeneratedRegex(
        @"^(?!-)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)(?:\.(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?))*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DomainSuffixRegex();

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('@'))
        {
            trimmed = trimmed[1..].Trim();
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    public static bool IsValidFormat(string? normalizedValue) =>
        !string.IsNullOrWhiteSpace(normalizedValue) && DomainSuffixRegex().IsMatch(normalizedValue);
}
