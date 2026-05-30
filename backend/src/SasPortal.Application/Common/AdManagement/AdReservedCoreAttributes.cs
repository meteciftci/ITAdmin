namespace SasPortal.Application.Common.AdManagement;

public static class AdReservedCoreAttributes
{
    public const string ReservedAttributeMappingMessage =
        "Bu AD attribute temel kullanıcı bilgisi veya sistem alanı olarak yönetiliyor. Mapped attributes içinde kullanılamaz.";

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
