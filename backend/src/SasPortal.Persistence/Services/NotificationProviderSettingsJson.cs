using System.Text.Json;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Persistence.Services;

internal static class NotificationProviderSettingsJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static string SerializePublic<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? DeserializePublic<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, Options);

    public static string? ProtectSecrets<T>(T secrets, ISecretProtector secretProtector)
    {
        var json = JsonSerializer.Serialize(secrets, Options);
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return null;
        }

        return secretProtector.Protect(json);
    }

    public static T? UnprotectSecrets<T>(string? protectedJson, ISecretProtector secretProtector) where T : class
    {
        if (string.IsNullOrWhiteSpace(protectedJson))
        {
            return null;
        }

        try
        {
            var json = secretProtector.Unprotect(protectedJson);
            return DeserializePublic<T>(json);
        }
        catch (Exception)
        {
            // Corrupt or incompatible protected provider settings are treated as unavailable.
            return null;
        }
    }
}
