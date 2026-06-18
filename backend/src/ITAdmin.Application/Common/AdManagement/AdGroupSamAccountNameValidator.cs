using ITAdmin.Application.Common.Constants;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdGroupSamAccountNameValidator
{
    private static readonly char[] ForbiddenCharacters =
        ['/', '\\', '[', ']', ':', ';', '|', '=', ',', '+', '*', '?', '<', '>', '"'];

    public static bool IsValid(string? samAccountName, out string messageKey)
    {
        messageKey = string.Empty;
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            messageKey = AdManagementApiMessageKeys.Groups.SamAccountNameRequired;
            return false;
        }

        var trimmed = samAccountName.Trim();
        if (trimmed.Length > AdGroupNameNormalizer.SamAccountNameMaxLength)
        {
            messageKey = AdManagementApiMessageKeys.Groups.SamAccountNameTooLong;
            return false;
        }

        if (trimmed.Any(static ch => ForbiddenCharacters.Contains(ch) || char.IsControl(ch)))
        {
            messageKey = AdManagementApiMessageKeys.Groups.SamAccountNameInvalidCharacters;
            return false;
        }

        return true;
    }
}
