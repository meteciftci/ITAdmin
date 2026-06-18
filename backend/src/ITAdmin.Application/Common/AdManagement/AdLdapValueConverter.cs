using System.Globalization;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdLdapValueConverter
{
    public const int UserAccountControlDisabledFlag = 0x0002;
    private const int UserAccountControlNormalAccountFlag = 0x0200;
    public const long NeverExpiresFileTime = 9223372036854775807L;

    public static bool IsAccountEnabled(int? userAccountControl) =>
        userAccountControl is null || (userAccountControl.Value & UserAccountControlDisabledFlag) == 0;

    public static int ApplyAccountDisabledFlag(int? userAccountControl, bool disabled)
    {
        var current = userAccountControl ?? UserAccountControlNormalAccountFlag;
        return disabled
            ? current | UserAccountControlDisabledFlag
            : current & ~UserAccountControlDisabledFlag;
    }

    public static bool IsAccountLockedOut(long? lockoutTime) =>
        lockoutTime is > 0;

    public static bool IsNeverExpiresFileTime(long? fileTime) =>
        fileTime is null or <= 0 or NeverExpiresFileTime;

    public static DateTimeOffset? FromAdFileTime(long? fileTime)
    {
        if (IsNeverExpiresFileTime(fileTime))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromFileTime(fileTime!.Value).ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static long ToAdFileTime(DateTimeOffset expiresAtUtc) =>
        expiresAtUtc.ToUniversalTime().ToFileTime();

    public static long ToNeverExpiresFileTime() => NeverExpiresFileTime;

    public static bool TryParseAccountExpirationDate(
        string? expiresAt,
        out DateOnly selectedDate,
        out string? errorMessage) =>
        AdAccountExpirationDateConverter.TryParseSelectedDate(expiresAt, out selectedDate, out errorMessage);

    public static DateTimeOffset? ParseGeneralizedTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var formats = new[]
        {
            "yyyyMMddHHmmss.fff'Z'",
            "yyyyMMddHHmmss.ff'Z'",
            "yyyyMMddHHmmss.f'Z'",
            "yyyyMMddHHmmss'Z'",
            "yyyyMMddHHmmss.fffK",
            "yyyyMMddHHmmssK",
        };

        if (DateTimeOffset.TryParseExact(
                trimmed,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    public static int ClampPageSize(int pageSize, int min = 5, int max = 100, int defaultSize = 20)
    {
        if (pageSize <= 0)
        {
            return defaultSize;
        }

        return Math.Clamp(pageSize, min, max);
    }

    public static int NormalizePageNumber(int pageNumber) =>
        pageNumber < 1 ? 1 : pageNumber;
}
