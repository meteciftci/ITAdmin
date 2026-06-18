using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdManagementNotificationRecipientResolver
{
    public static string? Resolve(
        AdManagementNotificationRecipientSource? source,
        string channel,
        AdManagementNotificationUserContext userContext)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.Type))
        {
            return null;
        }

        var type = source.Type.Trim();

        return type switch
        {
            AdManagementNotificationRecipientSourceTypes.UserPrincipalName =>
                userContext.UserPrincipalName,
            AdManagementNotificationRecipientSourceTypes.MailAttribute =>
                userContext.Mail
                ?? userContext.AttributeValuesByName.GetValueOrDefault("mail"),
            AdManagementNotificationRecipientSourceTypes.MappedAttribute =>
                ResolveMappedAttributeRecipient(source.Value, userContext),
            AdManagementNotificationRecipientSourceTypes.AdAttribute =>
                ResolveAdAttributeRecipient(source.Value, userContext.AttributeValuesByName),
            _ => null,
        };
    }

    private static string? ResolveMappedAttributeRecipient(
        string? mappingReference,
        AdManagementNotificationUserContext userContext)
    {
        if (string.IsNullOrWhiteSpace(mappingReference))
        {
            return null;
        }

        var reference = mappingReference.Trim();
        var mapping = userContext.AttributeMappings.FirstOrDefault(item =>
            string.Equals(item.Id.ToString(), reference, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.LogicalField, reference, StringComparison.OrdinalIgnoreCase));

        if (mapping is null)
        {
            return null;
        }

        return userContext.MappedValuesByLogicalField.GetValueOrDefault(mapping.LogicalField)
            ?? userContext.AttributeValuesByName.GetValueOrDefault(mapping.AttributeName);
    }

    private static string? ResolveAdAttributeRecipient(
        string? attributeName,
        IReadOnlyDictionary<string, string> attributeValuesByName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        return attributeValuesByName.GetValueOrDefault(attributeName.Trim());
    }
}
