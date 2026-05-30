namespace SasPortal.Application.Common.AdManagement;

public static class AdSamAccountNameValidator
{
    private static readonly char[] ForbiddenCharacters =
        ['/', '\\', '[', ']', ':', ';', '|', '=', ',', '+', '*', '?', '<', '>', '"'];

    public const string EmptyMessage = "Kullanıcı adı (sAMAccountName) zorunludur.";
    public const string TooLongMessage = "Kullanıcı adı (sAMAccountName) en fazla 20 karakter olabilir.";
    public const string InvalidCharactersMessage =
        "Kullanıcı adı (sAMAccountName) geçersiz karakterler içeriyor.";

    public static bool IsValid(string? samAccountName, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            message = EmptyMessage;
            return false;
        }

        var trimmed = samAccountName.Trim();
        if (trimmed.Length > AdUserNameNormalizer.SamAccountNameMaxLength)
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
