using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdUpdateUserRequestValidator
{
    public static bool TryValidate(
        UpdateAdUserRequest request,
        IReadOnlyList<AdAttributeMappingItem> mappings,
        out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(request.GivenName))
        {
            message = "Ad zorunludur.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Surname))
        {
            message = "Soyad zorunludur.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            message = "Görünen ad zorunludur.";
            return false;
        }

        if (!AdSamAccountNameValidator.IsValid(request.SamAccountName, out message))
        {
            return false;
        }

        if (!AdUserPrincipalNameValidator.IsValid(request.UserPrincipalName, out message))
        {
            return false;
        }

        return AdUpdateUserMappedAttributeValidator.TryValidate(request.MappedAttributes, mappings, out message);
    }

    public static string DeriveCommonNameFromDisplayName(string displayName) => displayName.Trim();
}
