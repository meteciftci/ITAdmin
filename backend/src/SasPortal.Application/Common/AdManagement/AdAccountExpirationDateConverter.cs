using System.Globalization;

namespace SasPortal.Application.Common.AdManagement;

public static class AdAccountExpirationDateConverter
{
    public static TimeZoneInfo ResolveExpirationTimeZone() => TimeZoneInfo.Local;

    public static bool TryParseSelectedDate(
        string? expiresAt,
        out DateOnly selectedDate,
        out string? errorMessage)
    {
        selectedDate = default;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(expiresAt))
        {
            errorMessage = "Account expiration date is required.";
            return false;
        }

        if (!DateOnly.TryParse(expiresAt.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out selectedDate))
        {
            errorMessage = "Account expiration date is invalid.";
            return false;
        }

        return true;
    }

    public static long ToAccountExpiresFileTime(DateOnly selectedDate)
    {
        var timeZone = ResolveExpirationTimeZone();
        var boundaryLocal = selectedDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var boundaryUtc = TimeZoneInfo.ConvertTimeToUtc(boundaryLocal, timeZone);
        return boundaryUtc.ToFileTimeUtc();
    }

    public static DateOnly? ToDisplayDateFromFileTime(long? fileTime)
    {
        if (AdLdapValueConverter.IsNeverExpiresFileTime(fileTime))
        {
            return null;
        }

        var timeZone = ResolveExpirationTimeZone();
        var boundaryUtc = DateTime.FromFileTimeUtc(fileTime!.Value);
        var boundaryLocal = TimeZoneInfo.ConvertTimeFromUtc(boundaryUtc, timeZone);
        return DateOnly.FromDateTime(boundaryLocal.Date).AddDays(-1);
    }

    public static string? ToDisplayDateString(long? fileTime)
    {
        var displayDate = ToDisplayDateFromFileTime(fileTime);
        return displayDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static bool IsNeverExpires(long? fileTime) =>
        AdLdapValueConverter.IsNeverExpiresFileTime(fileTime);
}
