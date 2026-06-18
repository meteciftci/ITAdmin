using ITAdmin.Application.Common.Constants;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdUserPrincipalNameValidator
{
    public static bool IsValid(string? userPrincipalName, out string messageKey)
    {
        messageKey = string.Empty;
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            messageKey = AdManagementApiMessageKeys.Users.UpnRequired;
            return false;
        }

        var trimmed = userPrincipalName.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0
            || atIndex != trimmed.LastIndexOf('@')
            || atIndex >= trimmed.Length - 1)
        {
            messageKey = AdManagementApiMessageKeys.Users.UpnInvalid;
            return false;
        }

        var localPart = trimmed[..atIndex].Trim();
        var domainPart = trimmed[(atIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(localPart)
            || string.IsNullOrWhiteSpace(domainPart)
            || localPart.Contains('@', StringComparison.Ordinal)
            || domainPart.Contains('@', StringComparison.Ordinal))
        {
            messageKey = AdManagementApiMessageKeys.Users.UpnInvalid;
            return false;
        }

        return true;
    }
}
