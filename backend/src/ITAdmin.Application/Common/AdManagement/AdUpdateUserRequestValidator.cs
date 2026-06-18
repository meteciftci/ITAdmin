using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdUpdateUserRequestValidator
{
    public static bool TryValidate(
        UpdateAdUserRequest request,
        IReadOnlyList<AdAttributeMappingItem> mappings,
        out string messageKey,
        out IReadOnlyDictionary<string, object>? messageParams)
    {
        messageKey = string.Empty;
        messageParams = null;

        if (string.IsNullOrWhiteSpace(request.GivenName))
        {
            messageKey = AdManagementApiMessageKeys.Users.GivenNameRequired;
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Surname))
        {
            messageKey = AdManagementApiMessageKeys.Users.SurnameRequired;
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            messageKey = AdManagementApiMessageKeys.Users.DisplayNameRequired;
            return false;
        }

        if (!AdSamAccountNameValidator.IsValid(request.SamAccountName, out messageKey))
        {
            return false;
        }

        if (!AdUserPrincipalNameValidator.IsValid(request.UserPrincipalName, out messageKey))
        {
            return false;
        }

        return AdUpdateUserMappedAttributeValidator.TryValidate(
            request.MappedAttributes,
            mappings,
            out messageKey,
            out messageParams);
    }

    public static string DeriveCommonNameFromDisplayName(string displayName) => displayName.Trim();
}
