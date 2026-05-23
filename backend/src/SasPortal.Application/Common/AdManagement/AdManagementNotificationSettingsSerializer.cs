using System.Text.Json;
using System.Text.Json.Serialization;

namespace SasPortal.Application.Common.AdManagement;

public static class AdManagementNotificationSettingsSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static AdManagementNotificationSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateDefault();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AdManagementNotificationSettings>(json, JsonOptions);
            return Normalize(parsed ?? CreateDefault());
        }
        catch
        {
            return CreateDefault();
        }
    }

    public static string Serialize(AdManagementNotificationSettings settings) =>
        JsonSerializer.Serialize(Normalize(settings), JsonOptions);

    public static AdManagementNotificationSettings CreateDefault() =>
        new()
        {
            UserCreated = AdManagementUserCreatedNotificationSettings.Disabled,
        };

    private static AdManagementNotificationSettings Normalize(AdManagementNotificationSettings settings)
    {
        settings.UserCreated ??= AdManagementUserCreatedNotificationSettings.Disabled;

        if (!settings.UserCreated.IsEnabled)
        {
            settings.UserCreated.SmsEnabled = false;
            settings.UserCreated.EmailEnabled = false;
        }

        return settings;
    }
}
