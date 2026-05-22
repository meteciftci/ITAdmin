using SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Infrastructure.Notifications;

internal static class NotificationTemplateReplacer
{
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
}
