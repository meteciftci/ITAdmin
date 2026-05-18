using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdUserMappedAttributeBuilder
{
    public static IReadOnlyList<MappedAdUserAttribute> Build(
        Func<string, IReadOnlyList<string>> getAttributeValues,
        IEnumerable<AdAttributeMappingItem> mappings)
    {
        var result = new List<MappedAdUserAttribute>();

        foreach (var mapping in mappings
                     .Where(static mapping => mapping.IsEnabled)
                     .OrderBy(static mapping => mapping.SortOrder))
        {
            var attributeName = mapping.AttributeName.Trim();
            if (!AdLdapAttributeCatalog.IsValidAttributeName(attributeName))
            {
                continue;
            }

            var rawValues = getAttributeValues(attributeName)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList();

            if (rawValues.Count == 0)
            {
                continue;
            }

            var maskedValues = AdAttributeValueMasker.MaskValues(
                rawValues,
                mapping.IsSensitive,
                mapping.MaskingStrategy);

            result.Add(new MappedAdUserAttribute(
                mapping.LogicalField,
                mapping.DisplayName,
                attributeName,
                maskedValues,
                mapping.IsSensitive,
                mapping.MaskingStrategy,
                mapping.IsEditable,
                mapping.IsSearchable,
                mapping.SortOrder));
        }

        return result;
    }
}
