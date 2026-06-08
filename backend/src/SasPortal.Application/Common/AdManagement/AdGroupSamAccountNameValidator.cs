namespace SasPortal.Application.Common.AdManagement;

public static class AdGroupSamAccountNameValidator
{
    private static readonly char[] ForbiddenCharacters =
        ['/', '\\', '[', ']', ':', ';', '|', '=', ',', '+', '*', '?', '<', '>', '"'];

    public const string EmptyMessage = "sAMAccountName zorunludur.";
    public const string TooLongMessage = "sAMAccountName en fazla 64 karakter olabilir.";
    public const string InvalidCharactersMessage =
        "sAMAccountName geçersiz karakterler içeriyor.";

    public static bool IsValid(string? samAccountName, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            message = EmptyMessage;
            return false;
        }

        var trimmed = samAccountName.Trim();
        if (trimmed.Length > AdGroupNameNormalizer.SamAccountNameMaxLength)
        {
            message = TooLongMessage;
            return false;
        }

        if (trimmed.Any(static ch => ForbiddenCharacters.Contains(ch) || char.IsControl(ch)))
        {
            message = InvalidCharactersMessage;
            return false;
        }

        return true;
    }
}
