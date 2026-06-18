using System.Text.RegularExpressions;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

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
        out string messageKey,
        out IReadOnlyDictionary<string, object>? messageParams)
    {
        messageKey = string.Empty;
        messageParams = null;
        var editableMappings = mappings
            .Where(static mapping => mapping.IsEnabled && mapping.IsEditable)
            .ToDictionary(static mapping => mapping.LogicalField, StringComparer.Ordinal);

        foreach (var attribute in mappedAttributes)
        {
            var logicalField = attribute.LogicalField?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(logicalField))
            {
                messageKey = AdManagementApiMessageKeys.MappedAttributes.InvalidLogicalField;
                return false;
            }

            if (!editableMappings.TryGetValue(logicalField, out var mapping))
            {
                var existsButNotEditable = mappings.Any(mappingItem =>
                    mappingItem.IsEnabled
                    && string.Equals(mappingItem.LogicalField, logicalField, StringComparison.Ordinal));

                messageKey = existsButNotEditable
                    ? AdManagementApiMessageKeys.MappedAttributes.NotEditable
                    : AdManagementApiMessageKeys.MappedAttributes.NotFound;
                messageParams = new Dictionary<string, object> { ["logicalField"] = logicalField };
                return false;
            }

            if (AdReservedCoreAttributes.IsReserved(mapping.AttributeName))
            {
                messageKey = AdReservedCoreAttributes.ReservedAttributeMappingMessageKey;
                return false;
            }

            if (!AdLdapAttributeCatalog.IsValidAttributeName(mapping.AttributeName))
            {
                messageKey = AdManagementApiMessageKeys.MappedAttributes.InvalidAttributeName;
                messageParams = new Dictionary<string, object> { ["attributeName"] = mapping.AttributeName };
                return false;
            }

            if (IsEmptyValue(attribute.Value))
            {
                continue;
            }

            if (!ValidateValue(mapping.ValidationType, attribute.Value, out messageKey))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateValue(string validationType, object? value, out string messageKey)
    {
        messageKey = string.Empty;
        var text = ExtractSingleValue(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return validationType switch
        {
            "Phone" when !PhoneRegex.IsMatch(text) =>
                Fail(AdManagementApiMessageKeys.MappedAttributes.InvalidPhoneFormat, out messageKey),
            "Email" when !EmailRegex.IsMatch(text) =>
                Fail(AdManagementApiMessageKeys.MappedAttributes.InvalidEmailFormat, out messageKey),
            "Number" when !NumberRegex.IsMatch(text) =>
                Fail(AdManagementApiMessageKeys.MappedAttributes.InvalidNumberFormat, out messageKey),
            _ => true,
        };
    }

    private static bool Fail(string key, out string messageKey)
    {
        messageKey = key;
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
