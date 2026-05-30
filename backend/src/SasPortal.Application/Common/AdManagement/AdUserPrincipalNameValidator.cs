namespace SasPortal.Application.Common.AdManagement;

public static class AdUserPrincipalNameValidator
{
    public const string EmptyMessage = "UPN (userPrincipalName) zorunludur.";
    public const string InvalidFormatMessage = "UPN (userPrincipalName) geçerli bir e-posta benzeri formatta olmalıdır.";

    public static bool IsValid(string? userPrincipalName, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            message = EmptyMessage;
            return false;
        }

        var trimmed = userPrincipalName.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0
            || atIndex != trimmed.LastIndexOf('@')
            || atIndex >= trimmed.Length - 1)
        {
            message = InvalidFormatMessage;
            return false;
        }

        var localPart = trimmed[..atIndex].Trim();
        var domainPart = trimmed[(atIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(localPart)
            || string.IsNullOrWhiteSpace(domainPart)
            || localPart.Contains('@', StringComparison.Ordinal)
            || domainPart.Contains('@', StringComparison.Ordinal))
        {
            message = InvalidFormatMessage;
            return false;
        }

        return true;
    }
}
