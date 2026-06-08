using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdCreateGroupRequestValidator
{
    public const string DisplayNameRequiredMessage = "Görünen ad zorunludur.";
    public const string TechnicalNameRequiredMessage = "Grup teknik adı zorunludur.";
    public const string GroupScopeRequiredMessage = "Grup kapsamı zorunludur.";
    public const string InvalidGroupScopeMessage = "Grup kapsamı geçersiz.";
    public const string TargetOuRequiredMessage = "Hedef OU zorunludur.";
    public const string InvalidTargetOuMessage = "Seçilen OU geçersiz.";

    public static bool TryValidate(
        CreateAdGroupRequest request,
        string? groupsSearchBase,
        out string message)
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

        if (!AdGroupSamAccountNameValidator.IsValid(request.SamAccountName, out message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.GroupScope))
        {
            message = GroupScopeRequiredMessage;
            return false;
        }

        if (!AdGroupTypeHelper.TryParseScopeCode(request.GroupScope, out _))
        {
            message = InvalidGroupScopeMessage;
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            message = TargetOuRequiredMessage;
            return false;
        }

        if (string.IsNullOrWhiteSpace(groupsSearchBase)
            || !AdLdapDnHelper.IsEqualOrDescendantOf(request.TargetOuDistinguishedName, groupsSearchBase))
        {
            message = InvalidTargetOuMessage;
            return false;
        }

        return true;
    }
}
