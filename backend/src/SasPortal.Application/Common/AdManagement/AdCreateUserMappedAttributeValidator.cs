using System.Text.RegularExpressions;
using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdCreateUserMappedAttributeValidator
{
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9\s().-]{7,20}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumberRegex = new(@"^-?\d+(\.\d+)?$", RegexOptions.Compiled);

    public static bool TryValidate(
        IReadOnlyList<CreateAdUserMappedAttributeRequest> mappedAttributes,
        IReadOnlyList<AdAttributeMappingItem> mappings,
        out string message)
    {
        message = string.Empty;
        var editableMappings = mappings
            .Where(static mapping => mapping.IsEnabled && mapping.IsEditable)
            .ToDictionary(static mapping => mapping.LogicalField, StringComparer.Ordinal);

        foreach (var attribute in mappedAttributes)
        {
            var logicalField = attribute.LogicalField?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(logicalField))
            {
                message = "Eşleştirilmiş attribute alanı geçersiz.";
                return false;
            }

            if (!editableMappings.TryGetValue(logicalField, out var mapping))
            {
                var existsButNotEditable = mappings.Any(mappingItem =>
                    mappingItem.IsEnabled
                    && string.Equals(mappingItem.LogicalField, logicalField, StringComparison.Ordinal));

                message = existsButNotEditable
                    ? $"Eşleştirilmiş attribute düzenlenemez: {logicalField}."
                    : $"Eşleştirilmiş attribute bulunamadı: {logicalField}.";
                return false;
            }

            if (!AdLdapAttributeCatalog.IsValidAttributeName(mapping.AttributeName))
            {
                message = $"AD attribute adı geçersiz: {mapping.AttributeName}.";
                return false;
            }

            if (IsEmptyValue(attribute.Value))
            {
                continue;
            }

            if (!ValidateValue(mapping.ValidationType, attribute.Value, out message))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateValue(string validationType, object? value, out string message)
    {
        message = string.Empty;
        var text = ExtractSingleValue(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return validationType switch
        {
            "Phone" when !PhoneRegex.IsMatch(text) =>
                Fail("Telefon formatı geçersiz.", out message),
            "Email" when !EmailRegex.IsMatch(text) =>
                Fail("E-posta formatı geçersiz.", out message),
            "Number" when !NumberRegex.IsMatch(text) =>
                Fail("Sayı formatı geçersiz.", out message),
            _ => true,
        };
    }

    private static bool Fail(string error, out string message)
    {
        message = error;
        return false;
    }

    private static bool IsEmptyValue(object? value) =>
        value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text),
            IEnumerable<string> values => !values.Any(static item => !string.IsNullOrWhiteSpace(item)),
            _ => string.IsNullOrWhiteSpace(value.ToString()),
        };

    private static string? ExtractSingleValue(object? value) =>
        value switch
        {
            null => null,
            string text => text.Trim(),
            IEnumerable<string> values => values.FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item))?.Trim(),
            _ => value.ToString()?.Trim(),
        };
}
