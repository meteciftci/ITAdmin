using System.Text;

namespace ITAdmin.Infrastructure.Notifications;

internal static class TurkishCharacterTransliterator
{
    private static readonly Dictionary<char, string> Map = new()
    {
        ['ç'] = "c",
        ['Ç'] = "C",
        ['ğ'] = "g",
        ['Ğ'] = "G",
        ['ı'] = "i",
        ['İ'] = "I",
        ['ö'] = "o",
        ['Ö'] = "O",
        ['ş'] = "s",
        ['Ş'] = "S",
        ['ü'] = "u",
        ['Ü'] = "U",
    };

    public static string Transliterate(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (Map.TryGetValue(character, out var replacement))
            {
                builder.Append(replacement);
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
