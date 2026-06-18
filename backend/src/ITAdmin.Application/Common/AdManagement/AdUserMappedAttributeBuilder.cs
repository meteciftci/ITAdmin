using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

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
            if (AdReservedCoreAttributes.IsReserved(attributeName)
                || !AdLdapAttributeCatalog.IsValidAttributeName(attributeName))
            {
                continue;
            }

            var rawValues = getAttributeValues(attributeName)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList();

            IReadOnlyList<string>? responseValues;
            if (rawValues.Count == 0)
            {
                responseValues = null;
            }
            else
            {
                responseValues = AdAttributeValueMasker.MaskValues(
                    rawValues,
                    mapping.IsSensitive,
                    mapping.MaskingStrategy);
            }

            result.Add(new MappedAdUserAttribute(
                mapping.LogicalField,
                mapping.DisplayName,
                attributeName,
                responseValues,
                mapping.IsSensitive,
                mapping.MaskingStrategy,
                mapping.IsEditable,
                mapping.IsSearchable,
                mapping.SortOrder));
        }

        return result;
    }
}
