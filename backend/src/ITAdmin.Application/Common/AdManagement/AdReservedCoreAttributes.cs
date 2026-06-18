using ITAdmin.Application.Common.Constants;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdReservedCoreAttributes
{
    public const string ReservedAttributeMappingMessageKey =
        AdManagementApiMessageKeys.MappedAttributes.ReservedAttribute;

    private static readonly HashSet<string> ReservedAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "givenName",
        "sn",
        "displayName",
        "cn",
        "name",
        "sAMAccountName",
        "userPrincipalName",
        "mail",
        "department",
        "distinguishedName",
        "objectGUID",
        "memberOf",
        "userAccountControl",
        "lockoutTime",
        "pwdLastSet",
        "lastLogonTimestamp",
        "whenCreated",
        "whenChanged",
        "unicodePwd",
    };

    public static bool IsReserved(string? attributeName) =>
        !string.IsNullOrWhiteSpace(attributeName)
        && ReservedAttributeNames.Contains(attributeName.Trim());
}
