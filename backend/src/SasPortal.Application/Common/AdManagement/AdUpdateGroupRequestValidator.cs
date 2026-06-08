using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdUpdateGroupRequestValidator
{
    public const string DisplayNameRequiredMessage = "Görünen ad zorunludur.";
    public const string TechnicalNameRequiredMessage = "Grup teknik adı zorunludur.";

    public static bool TryValidate(UpdateAdGroupRequest request, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            message = DisplayNameRequiredMessage;
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            message = TechnicalNameRequiredMessage;
            return false;
        }

        return AdGroupSamAccountNameValidator.IsValid(request.SamAccountName, out message);
    }
}
