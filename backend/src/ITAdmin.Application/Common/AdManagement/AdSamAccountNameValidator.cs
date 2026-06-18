using ITAdmin.Application.Common.Constants;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdSamAccountNameValidator
{
    private static readonly char[] ForbiddenCharacters =
        ['/', '\\', '[', ']', ':', ';', '|', '=', ',', '+', '*', '?', '<', '>', '"'];

    public static bool IsValid(string? samAccountName, out string messageKey)
    {
        messageKey = string.Empty;
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            messageKey = AdManagementApiMessageKeys.Users.SamAccountNameRequired;
            return false;
        }

        var trimmed = samAccountName.Trim();
        if (trimmed.Length > AdUserNameNormalizer.SamAccountNameMaxLength)
        {
            messageKey = AdManagementApiMessageKeys.Users.SamAccountNameTooLong;
            return false;
        }

        if (trimmed.Any(static ch => ForbiddenCharacters.Contains(ch) || char.IsControl(ch)))
        {
            messageKey = AdManagementApiMessageKeys.Users.SamAccountNameInvalidCharacters;
            return false;
        }

        return true;
    }
}
