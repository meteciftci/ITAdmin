using System.Text.Json;
using ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Infrastructure.Notifications;

internal static class NotificationTemplateReplacer
{
    internal const string InvalidJsonBodyMessage =
        "Custom HTTP SMS body template rendered invalid JSON. Check JSON body template and placeholders.";

    public static string Apply(
        string? template,
        string phone,
        string message,
        SmsCustomHttpPublicSettings publicSettings,
        SmsCustomHttpSecretSettings secrets)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return template
            .Replace("{{phone}}", phone, StringComparison.OrdinalIgnoreCase)
            .Replace("{{message}}", message, StringComparison.OrdinalIgnoreCase)
            .Replace("{{sender}}", publicSettings.Sender ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{settings.sender}}", publicSettings.Sender ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{secret.basicUserName}}", secrets.BasicUserName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{secret.basicPassword}}", secrets.BasicPassword ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{secret.bearerToken}}", secrets.BearerToken ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{secret.apiKeyValue}}", secrets.ApiKeyValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Replaces placeholders with JSON string inner values (escaped, without surrounding quotes).
    /// Placeholders must appear inside JSON string literals, e.g. "content": "{{message}}".
    /// </summary>
    public static string ApplyForJsonBody(
        string? template,
        string phone,
        string message,
        SmsCustomHttpPublicSettings publicSettings,
        SmsCustomHttpSecretSettings secrets)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var replacements = new (string Placeholder, string EscapedValue)[]
        {
            ("{{settings.sender}}", ToJsonStringInnerValue(publicSettings.Sender ?? string.Empty)),
            ("{{secret.basicUserName}}", ToJsonStringInnerValue(secrets.BasicUserName ?? string.Empty)),
            ("{{secret.basicPassword}}", ToJsonStringInnerValue(secrets.BasicPassword ?? string.Empty)),
            ("{{secret.bearerToken}}", ToJsonStringInnerValue(secrets.BearerToken ?? string.Empty)),
            ("{{secret.apiKeyValue}}", ToJsonStringInnerValue(secrets.ApiKeyValue ?? string.Empty)),
            ("{{sender}}", ToJsonStringInnerValue(publicSettings.Sender ?? string.Empty)),
            ("{{phone}}", ToJsonStringInnerValue(phone)),
            ("{{message}}", ToJsonStringInnerValue(message)),
        };

        var result = template;
        foreach (var (placeholder, escapedValue) in replacements)
        {
            result = result.Replace(placeholder, escapedValue, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    public static bool TryParseJsonDocument(string body, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(body))
        {
            errorMessage = InvalidJsonBodyMessage;
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(body);
            return true;
        }
        catch (JsonException)
        {
            errorMessage = InvalidJsonBodyMessage;
            return false;
        }
    }

    internal static string ToJsonStringInnerValue(string value)
    {
        var serialized = JsonSerializer.Serialize(value);
        if (serialized.Length >= 2 && serialized[0] == '"' && serialized[^1] == '"')
        {
            return serialized[1..^1];
        }

        return serialized;
    }
}
