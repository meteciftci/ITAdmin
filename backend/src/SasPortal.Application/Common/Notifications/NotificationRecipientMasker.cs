namespace SasPortal.Application.Common.Notifications;

public static class NotificationRecipientMasker
{
    public static string MaskPhone(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return "<empty>";
        }

        var trimmed = phoneNumber.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        var visiblePrefix = Math.Min(4, trimmed.Length);
        var visibleSuffix = trimmed.Length > 6 ? 2 : 0;
        var maskedLength = Math.Max(trimmed.Length - visiblePrefix - visibleSuffix, 4);

        return trimmed[..visiblePrefix]
            + new string('*', maskedLength)
            + (visibleSuffix > 0 ? trimmed[^visibleSuffix..] : string.Empty);
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "<empty>";
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0)
        {
            return "***";
        }

        var local = trimmed[..atIndex];
        var domain = trimmed[atIndex..];

        if (local.Length == 1)
        {
            return $"{local[0]}***{domain}";
        }

        return $"{local[0]}***{local[^1]}{domain}";
    }
}
