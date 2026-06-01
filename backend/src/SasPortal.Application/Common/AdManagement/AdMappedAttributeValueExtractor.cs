namespace SasPortal.Application.Common.AdManagement;

public static class AdMappedAttributeValueExtractor
{
    public static string? ExtractScalar(object? value) =>
        value switch
        {
            null => null,
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            IEnumerable<string> values => values
                .FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item))
                ?.Trim(),
            _ => string.IsNullOrWhiteSpace(value.ToString()) ? null : value.ToString()!.Trim(),
        };
}
