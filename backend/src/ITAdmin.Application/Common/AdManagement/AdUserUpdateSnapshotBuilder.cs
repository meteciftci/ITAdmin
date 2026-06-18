using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdUserUpdateSnapshotBuilder
{
    public static object Build(
        string? givenName,
        string? surname,
        string? displayName,
        string? samAccountName,
        string? userPrincipalName,
        string? mail,
        string? department,
        string? distinguishedName,
        IReadOnlyList<MappedAdUserAttribute> mappedAttributes) =>
        new
        {
            givenName,
            surname,
            displayName,
            samAccountName,
            userPrincipalName,
            mail,
            department,
            distinguishedName,
            mappedAttributes = mappedAttributes
                .Select(static attribute => new
                {
                    logicalField = attribute.LogicalField,
                    values = attribute.Value,
                })
                .ToList(),
        };

    public static IReadOnlyList<MappedAdUserAttribute> BuildMappedAttributesForSnapshot(
        Func<string, IReadOnlyList<string>> getAttributeValues,
        IEnumerable<AdAttributeMappingItem> mappings) =>
        AdUserMappedAttributeBuilder.Build(getAttributeValues, mappings);
}
