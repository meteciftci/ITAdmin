namespace SasPortal.Application.Common.Security;

public static class SupportedLanguages
{
    public const string Turkish = "tr";
    public const string English = "en";

    public static bool IsSupported(string? language)
    {
        return string.Equals(language, Turkish, StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, English, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string language)
    {
        return language.Trim().ToLowerInvariant();
    }
}
