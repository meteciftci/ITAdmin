using System.Text;

namespace SasPortal.Application.Common.AdManagement;

public static class AdGroupNameNormalizer
{
    public const int SamAccountNameMaxLength = 64;

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

    public static string? BuildSamAccountNameSuggestion(string? technicalName)
    {
        if (string.IsNullOrWhiteSpace(technicalName))
        {
            return null;
        }

        var builder = new StringBuilder(technicalName.Length);
        foreach (var character in technicalName.Trim())
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
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static string NormalizeTechnicalName(string? technicalName) =>
        string.IsNullOrWhiteSpace(technicalName) ? string.Empty : technicalName.Trim();

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
