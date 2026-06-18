using System.Text;

namespace ITAdmin.Application.Common.Security;

public static class CorrelationIdNormalizer
{
    public static string Resolve(string? rawHeaderValue)
    {
        var normalized = Normalize(rawHeaderValue);
        return string.IsNullOrEmpty(normalized) ? Generate() : normalized;
    }

    public static string? Normalize(string? rawHeaderValue)
    {
        if (string.IsNullOrWhiteSpace(rawHeaderValue))
        {
            return null;
        }

        var builder = new StringBuilder(rawHeaderValue.Length);
        foreach (var character in rawHeaderValue.Trim())
        {
            if (char.IsControl(character) || character is '\r' or '\n')
            {
                continue;
            }

            if (IsAllowedCharacter(character))
            {
                builder.Append(character);
            }
        }

        if (builder.Length == 0)
        {
            return null;
        }

        if (builder.Length > CorrelationIdConstants.MaxLength)
        {
            return null;
        }

        return builder.ToString();
    }

    public static string Generate() => Guid.NewGuid().ToString("D");

    private static bool IsAllowedCharacter(char character) =>
        char.IsLetterOrDigit(character) || character is '-' or '_' or '.';
}
