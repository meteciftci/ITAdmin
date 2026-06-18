using System.Globalization;
using System.Text;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdUserNameNormalizer
{
    public const int SamAccountNameMaxLength = 20;
    public const int UserPrincipalNameMaxLength = 256;

    private static readonly Dictionary<char, char> TurkishCharacterMap = new()
    {
        ['ç'] = 'c',
        ['Ç'] = 'c',
        ['ğ'] = 'g',
        ['Ğ'] = 'g',
        ['ı'] = 'i',
        ['İ'] = 'i',
        ['ö'] = 'o',
        ['Ö'] = 'o',
        ['ş'] = 's',
        ['Ş'] = 's',
        ['ü'] = 'u',
        ['Ü'] = 'u',
    };

    public static string NormalizeUserName(string? givenName, string? surname)
    {
        var combined = $"{givenName ?? string.Empty}.{surname ?? string.Empty}";
        return NormalizeSamAccountName(combined, reserveSuffixLength: 0) ?? string.Empty;
    }

    public static string? NormalizeSamAccountName(string? value, int reserveSuffixLength = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (TurkishCharacterMap.TryGetValue(character, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            var lowered = char.ToLowerInvariant(character);
            if (char.IsLetterOrDigit(lowered) || lowered is '.' or '-' or '_')
            {
                builder.Append(lowered);
            }
            else if (char.IsWhiteSpace(character))
            {
                builder.Append('.');
            }
        }

        var normalized = CollapseDots(builder.ToString()).Trim('.');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var maxLength = Math.Max(1, SamAccountNameMaxLength - Math.Max(0, reserveSuffixLength));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].TrimEnd('.');
    }

    public static string BuildDisplayName(string givenName, string surname, int suffixNumber = 0)
    {
        var baseName = $"{givenName.Trim()} {surname.Trim()}".Trim();
        return suffixNumber <= 1 ? baseName : $"{baseName} {suffixNumber}";
    }

    public static string BuildSamAccountNameWithSuffix(string baseSamAccountName, int suffixNumber)
    {
        if (suffixNumber <= 1)
        {
            return baseSamAccountName;
        }

        var suffix = suffixNumber.ToString(CultureInfo.InvariantCulture);
        var reserve = suffix.Length;
        var trimmedBase = NormalizeSamAccountName(baseSamAccountName, reserve) ?? baseSamAccountName;
        return $"{trimmedBase}{suffix}";
    }

    public static string BuildUserPrincipalName(string samAccountName, string defaultUpnSuffix)
    {
        var normalizedSuffix = AdDefaultUpnSuffixNormalizer.Normalize(defaultUpnSuffix)
            ?? throw new InvalidOperationException("Default UPN suffix is required.");
        return $"{samAccountName}@{normalizedSuffix}";
    }

    public static string? NormalizeUserPrincipalName(string? value, string defaultUpnSuffix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var atIndex = trimmed.IndexOf('@');
        string localPart;
        string suffixPart;

        if (atIndex >= 0)
        {
            localPart = trimmed[..atIndex];
            suffixPart = trimmed[(atIndex + 1)..];
        }
        else
        {
            localPart = trimmed;
            suffixPart = defaultUpnSuffix;
        }

        var normalizedLocal = NormalizeSamAccountName(localPart);
        var normalizedSuffix = AdDefaultUpnSuffixNormalizer.Normalize(suffixPart)
            ?? AdDefaultUpnSuffixNormalizer.Normalize(defaultUpnSuffix);

        if (string.IsNullOrWhiteSpace(normalizedLocal) || string.IsNullOrWhiteSpace(normalizedSuffix))
        {
            return null;
        }

        var upn = $"{normalizedLocal}@{normalizedSuffix}";
        return upn.Length <= UserPrincipalNameMaxLength ? upn : null;
    }

    public static string BuildUserPrincipalNameWithSuffix(
        string baseSamAccountName,
        string defaultUpnSuffix,
        int suffixNumber)
    {
        var sam = BuildSamAccountNameWithSuffix(baseSamAccountName, suffixNumber);
        return BuildUserPrincipalName(sam, defaultUpnSuffix);
    }

    private static string CollapseDots(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasDot = false;

        foreach (var character in value)
        {
            if (character == '.')
            {
                if (previousWasDot)
                {
                    continue;
                }

                previousWasDot = true;
                builder.Append(character);
                continue;
            }

            previousWasDot = false;
            builder.Append(character);
        }

        return builder.ToString();
    }
}
