using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdCreateGroupRequestValidator
{
    public static bool TryValidate(
        CreateAdGroupRequest request,
        string? groupsSearchBase,
        out string messageKey)
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

        if (!AdGroupSamAccountNameValidator.IsValid(request.SamAccountName, out messageKey))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.GroupScope))
        {
            messageKey = AdManagementApiMessageKeys.Groups.GroupScopeRequired;
            return false;
        }

        if (!AdGroupTypeHelper.TryParseScopeCode(request.GroupScope, out _))
        {
            messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupScope;
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            messageKey = AdManagementApiMessageKeys.Groups.TargetOuRequired;
            return false;
        }

        if (string.IsNullOrWhiteSpace(groupsSearchBase)
            || !AdLdapDnHelper.IsEqualOrDescendantOf(request.TargetOuDistinguishedName, groupsSearchBase))
        {
            messageKey = AdManagementApiMessageKeys.Groups.InvalidTargetOu;
            return false;
        }

        return true;
    }
}
