using System.Text.Json;

namespace SasPortal.Application.Common.AdManagement;

public static class AdOperationLogErrorCodeExtractor
{
    public static string? TryExtractFromDiagnosticJson(string? diagnosticJson)
    {
        if (string.IsNullOrWhiteSpace(diagnosticJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(diagnosticJson);
            if (document.RootElement.TryGetProperty("code", out var codeElement)
                && codeElement.ValueKind == JsonValueKind.String)
            {
                var code = codeElement.GetString();
                return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
