namespace SasPortal.Application.Common.AdManagement;

public static class AdLdapDiagnosticSanitizer
{
    private const int MaxDiagnosticLength = 500;

    private static readonly string[] SensitiveTokens =
    [
        "password",
        "pwd=",
        "token",
        "secret",
        "credential",
        "refresh_token",
        "access_token",
        "authorization",
        "connection string",
        "service account",
    ];

    public static string? SanitizeLdapDiagnosticMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim().Replace('\r', ' ').Replace('\n', ' ');
        while (trimmed.Contains("  ", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("  ", " ", StringComparison.Ordinal);
        }

        var lower = trimmed.ToLowerInvariant();
        foreach (var token in SensitiveTokens)
        {
            if (lower.Contains(token, StringComparison.Ordinal))
            {
                return "[redacted]";
            }
        }

        if (trimmed.Length > MaxDiagnosticLength)
        {
            trimmed = $"{trimmed[..MaxDiagnosticLength]}…";
        }

        return trimmed;
    }

    public static string? SanitizeDistinguishedName(string? distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return null;
        }

        var trimmed = distinguishedName.Trim();
        if (trimmed.Length > 512)
        {
            trimmed = $"{trimmed[..512]}…";
        }

        return trimmed;
    }
}
