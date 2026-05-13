using System.Globalization;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Enums;

namespace SasPortal.Persistence.Services;

internal static class SessionSecuritySettingsHelper
{
    public static SessionSecuritySettings ReadFromItems(
        IEnumerable<ApplicationSettingItem> items,
        ILogger logger)
    {
        var map = items
            .Where(x => x.IsActive && SecuritySettingKeys.AllSet.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x, StringComparer.Ordinal);

        var defaults = SessionSecurityDefaults.AsSettings();

        return new SessionSecuritySettings(
            ParseInt(map, SecuritySettingKeys.AccessTokenMinutes, defaults.AccessTokenMinutes, logger),
            ParseInt(map, SecuritySettingKeys.IdleTimeoutMinutes, defaults.IdleTimeoutMinutes, logger),
            ParseInt(map, SecuritySettingKeys.IdleWarningSeconds, defaults.IdleWarningSeconds, logger),
            ParseInt(map, SecuritySettingKeys.SessionRefreshTokenHours, defaults.SessionRefreshTokenHours, logger),
            ParseInt(map, SecuritySettingKeys.RememberMeRefreshTokenDays, defaults.RememberMeRefreshTokenDays, logger),
            ParseBool(map, SecuritySettingKeys.RememberMeEnabled, defaults.RememberMeEnabled, logger));
    }

    public static string? ValidateUpdate(
        int accessTokenMinutes,
        int idleTimeoutMinutes,
        int idleWarningSeconds,
        int sessionRefreshTokenHours,
        int rememberMeRefreshTokenDays)
    {
        if (accessTokenMinutes is < 5 or > 240)
        {
            return "Access token duration must be between 5 and 240 minutes.";
        }

        if (idleTimeoutMinutes is < 5 or > 480)
        {
            return "Idle timeout must be between 5 and 480 minutes.";
        }

        if (idleWarningSeconds is < 10 or > 300)
        {
            return "Session warning duration must be between 10 and 300 seconds.";
        }

        if (sessionRefreshTokenHours is < 1 or > 24)
        {
            return "Browser session refresh token duration must be between 1 and 24 hours.";
        }

        if (rememberMeRefreshTokenDays is < 1 or > 30)
        {
            return "Remember me refresh token duration must be between 1 and 30 days.";
        }

        var idleSeconds = idleTimeoutMinutes * 60L;
        if (idleWarningSeconds >= idleSeconds)
        {
            return "Session warning duration must be less than the idle timeout.";
        }

        return null;
    }

    public static string BuildAuditDescription(SessionSecuritySettings before, SessionSecuritySettings after)
    {
        var parts = new List<string>();
        AppendChange(parts, nameof(SessionSecuritySettings.AccessTokenMinutes), before.AccessTokenMinutes, after.AccessTokenMinutes);
        AppendChange(parts, nameof(SessionSecuritySettings.IdleTimeoutMinutes), before.IdleTimeoutMinutes, after.IdleTimeoutMinutes);
        AppendChange(parts, nameof(SessionSecuritySettings.IdleWarningSeconds), before.IdleWarningSeconds, after.IdleWarningSeconds);
        AppendChange(parts, nameof(SessionSecuritySettings.SessionRefreshTokenHours), before.SessionRefreshTokenHours, after.SessionRefreshTokenHours);
        AppendChange(parts, nameof(SessionSecuritySettings.RememberMeRefreshTokenDays), before.RememberMeRefreshTokenDays, after.RememberMeRefreshTokenDays);
        AppendChange(parts, nameof(SessionSecuritySettings.RememberMeEnabled), before.RememberMeEnabled, after.RememberMeEnabled);

        return parts.Count == 0
            ? string.Empty
            : $"Session security settings updated. {string.Join(", ", parts)}";
    }

    private static void AppendChange<T>(ICollection<string> parts, string fieldName, T before, T after)
    {
        if (!Equals(before, after))
        {
            parts.Add($"{fieldName}: {before} -> {after}");
        }
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, ApplicationSettingItem> map,
        string key,
        int fallback,
        ILogger logger)
    {
        if (!map.TryGetValue(key, out var item) || string.IsNullOrWhiteSpace(item.Value))
        {
            return fallback;
        }

        if (item.ValueType == SettingValueType.Number || item.ValueType == SettingValueType.String)
        {
            if (int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        logger.LogWarning(
            "Invalid or unsupported application setting value for {SettingKey}. Using default {DefaultValue}.",
            key,
            fallback);
        return fallback;
    }

    private static bool ParseBool(
        IReadOnlyDictionary<string, ApplicationSettingItem> map,
        string key,
        bool fallback,
        ILogger logger)
    {
        if (!map.TryGetValue(key, out var item) || string.IsNullOrWhiteSpace(item.Value))
        {
            return fallback;
        }

        if (item.ValueType == SettingValueType.Boolean || item.ValueType == SettingValueType.String)
        {
            if (bool.TryParse(item.Value, out var parsed))
            {
                return parsed;
            }
        }

        logger.LogWarning(
            "Invalid or unsupported application setting value for {SettingKey}. Using default {DefaultValue}.",
            key,
            fallback);
        return fallback;
    }
}
