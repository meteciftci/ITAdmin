using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdUpdateGroupRequestValidator
{
    public static bool TryValidate(UpdateAdGroupRequest request, out string messageKey)
    {
        messageKey = string.Empty;

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            messageKey = AdManagementApiMessageKeys.Groups.DisplayNameRequired;
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            messageKey = AdManagementApiMessageKeys.Groups.TechnicalNameRequired;
            return false;
        }

        return AdGroupSamAccountNameValidator.IsValid(request.SamAccountName, out messageKey);
    }
}
