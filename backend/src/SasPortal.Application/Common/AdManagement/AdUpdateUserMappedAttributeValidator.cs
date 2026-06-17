using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdUpdateUserMappedAttributeValidator
{
    public static bool TryValidate(
        IReadOnlyList<UpdateAdUserMappedAttributeRequest> mappedAttributes,
        IReadOnlyList<AdAttributeMappingItem> mappings,
        out string messageKey,
        out IReadOnlyDictionary<string, object>? messageParams) =>
        AdCreateUserMappedAttributeValidator.TryValidate(
            mappedAttributes
                .Select(static attribute => new CreateAdUserMappedAttributeRequest(
                    attribute.LogicalField,
                    attribute.Value))
                .ToList(),
            mappings,
            out messageKey,
            out messageParams);
}
