using System.Text.RegularExpressions;
using SasPortal.Application.Abstractions.Notifications;

namespace SasPortal.Application.Notifications;

public sealed partial class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    private static readonly Regex PlaceholderRegex = PlaceholderPattern();

    public string Render(string template, IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!variables.TryGetValue(key, out var value) || value is null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        });
    }

    public IReadOnlyList<string> ExtractVariables(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return [];
        }

        return PlaceholderRegex.Matches(template)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
