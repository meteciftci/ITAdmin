using System.Text.RegularExpressions;

namespace ITAdmin.Application.Common.LicenseManagement;

public static partial class LicenseManagementValidation
{
    [GeneratedRegex(@"^https?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlPrefixRegex();

    public static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 250)
        {
            return false;
        }

        try
        {
            _ = new System.Net.Mail.MailAddress(trimmed);
            return trimmed.Contains('@', StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValidUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 1000)
        {
            return false;
        }

        if (!HttpUrlPrefixRegex().IsMatch(trimmed))
        {
            return false;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static bool IsValidDateRange(DateOnly? start, DateOnly? end, out string? errorMessage)
    {
        errorMessage = null;
        if (start is null || end is null)
        {
            return true;
        }

        if (end < start)
        {
            errorMessage = "End date cannot be earlier than start date.";
            return false;
        }

        return true;
    }

    public static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
