namespace ITAdmin.Application.Notifications;

public static class NotificationOutboxRetryHelper
{
    public static DateTimeOffset CalculateNextAttemptUtc(int attemptCount, DateTimeOffset nowUtc)
    {
        var delayMinutes = attemptCount switch
        {
            1 => 1,
            2 => 5,
            _ => 15,
        };

        return nowUtc.AddMinutes(delayMinutes);
    }

    public static string? SanitizeErrorMessage(string? message, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 3)]}...";
    }

    public static string? SanitizeProviderSummary(string? summary, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var trimmed = summary.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 3)]}...";
    }
}
