using SasPortal.Application.Common.Constants;

namespace SasPortal.Application.Common.AdManagement;

public static class AdOrganizationalUnitRequestValidator
{
    private const int MaxOuNameLength = 64;

    public static bool TryValidateName(string? name, out string messageKey)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            messageKey = AdManagementApiMessageKeys.OrganizationalUnits.NameRequired;
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxOuNameLength)
        {
            messageKey = AdManagementApiMessageKeys.OrganizationalUnits.NameTooLong;
            return false;
        }

        if (trimmed.Any(static ch => ",=+<>#;\"\\".Contains(ch)))
        {
            messageKey = AdManagementApiMessageKeys.OrganizationalUnits.NameInvalidCharacters;
            return false;
        }

        messageKey = string.Empty;
        return true;
    }

    public static bool TryValidateParentDistinguishedName(string? parentDistinguishedName, out string messageKey)
    {
        if (string.IsNullOrWhiteSpace(parentDistinguishedName))
        {
            messageKey = AdManagementApiMessageKeys.OrganizationalUnits.ParentRequired;
            return false;
        }

        if (!AdOrganizationalUnitGuard.IsValidParentCandidate(parentDistinguishedName))
        {
            messageKey = AdManagementApiMessageKeys.OrganizationalUnits.InvalidParent;
            return false;
        }

        messageKey = string.Empty;
        return true;
    }

    public static bool TryValidateTargetParentDistinguishedName(
        string? targetParentDistinguishedName,
        out string messageKey)
    {
        if (string.IsNullOrWhiteSpace(targetParentDistinguishedName))
        {
            messageKey = AdManagementApiMessageKeys.OrganizationalUnits.TargetParentRequired;
            return false;
        }

        if (!AdOrganizationalUnitGuard.IsValidParentCandidate(targetParentDistinguishedName))
        {
            messageKey = AdManagementApiMessageKeys.OrganizationalUnits.InvalidTargetParent;
            return false;
        }

        messageKey = string.Empty;
        return true;
    }
}
