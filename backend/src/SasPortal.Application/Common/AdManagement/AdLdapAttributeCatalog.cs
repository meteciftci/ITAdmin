using System.Text.RegularExpressions;
using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static partial class AdLdapAttributeCatalog
{
    public const int MinimumSearchLength = 2;

    private static readonly string[] StandardSearchAttributeNames =
    [
        "sAMAccountName",
        "userPrincipalName",
        "displayName",
        "mail",
        "cn",
        "department",
        "title",
        "employeeType",
        "employeeNumber",
        "employeeID",
        "l",
        "telephoneNumber",
        "mobile",
    ];

    private static readonly string[] CoreListAttributeNames =
    [
        "objectGUID",
        "distinguishedName",
        "sAMAccountName",
        "userPrincipalName",
        "displayName",
        "mail",
        "department",
        "userAccountControl",
        "lockoutTime",
        "whenCreated",
        "whenChanged",
        "lastLogonTimestamp",
    ];

    private static readonly string[] CoreDetailAttributeNames =
    [
        "objectGUID",
        "distinguishedName",
        "givenName",
        "sn",
        "displayName",
        "sAMAccountName",
        "userPrincipalName",
        "mail",
        "department",
        "userAccountControl",
        "lockoutTime",
        "pwdLastSet",
        "lastLogonTimestamp",
        "whenCreated",
        "whenChanged",
        "memberOf",
    ];

    public static readonly HashSet<string> ExcludedAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "userCertificate",
        "thumbnailPhoto",
        "jpegPhoto",
        "logonHours",
        "unicodePwd",
        "supplementalCredentials",
        "msDS-KeyCredentialLink",
        "nTSecurityDescriptor",
        "replPropertyMetaData",
        "userPassword",
        "currentPassword",
        "unicodePassword",
        "ntPwdHistory",
        "lmPwdHistory",
        "trustAuthOutgoing",
        "trustAuthIncoming",
    };

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeNameRegex();

    public static bool IsValidAttributeName(string? attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        var trimmed = attributeName.Trim();
        if (ExcludedAttributeNames.Contains(trimmed) || IsSensitiveAttributeName(trimmed))
        {
            return false;
        }

        return AttributeNameRegex().IsMatch(trimmed);
    }

    public static bool IsSearchTermValid(string? search) =>
        !string.IsNullOrWhiteSpace(search) && search.Trim().Length >= MinimumSearchLength;

    public static IReadOnlyList<string> GetSearchableMappingAttributeNames(
        IEnumerable<AdAttributeMappingItem> mappings) =>
        mappings
            .Where(static mapping =>
                mapping.IsEnabled
                && mapping.IsSearchable
                && !mapping.IsSensitive)
            .Select(static mapping => mapping.AttributeName.Trim())
            .Where(IsValidAttributeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> GetActiveMappingAttributeNames(
        IEnumerable<AdAttributeMappingItem> mappings) =>
        mappings
            .Where(static mapping => mapping.IsEnabled)
            .Select(static mapping => mapping.AttributeName.Trim())
            .Where(IsValidAttributeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string[] BuildListLdapAttributeNames(IEnumerable<AdAttributeMappingItem> mappings) =>
        MergeAttributeNames(CoreListAttributeNames, GetSearchableMappingAttributeNames(mappings));

    public static string[] BuildDetailLdapAttributeNames(IEnumerable<AdAttributeMappingItem> mappings) =>
        MergeAttributeNames(CoreDetailAttributeNames, GetActiveMappingAttributeNames(mappings));

    public static string BuildUserSearchFilter(
        string search,
        AdUserStatusFilter status,
        IEnumerable<string> additionalSearchAttributes)
    {
        var parts = new List<string>
        {
            "(objectCategory=person)",
            "(objectClass=user)",
            "(!(isDeleted=TRUE))",
        };

        parts.Add(status switch
        {
            AdUserStatusFilter.Active => "(!(userAccountControl:1.2.840.113556.1.4.803:=2))",
            AdUserStatusFilter.Disabled => "(userAccountControl:1.2.840.113556.1.4.803:=2)",
            _ => string.Empty,
        });

        var escaped = AdLdapFilterHelper.EscapeFilterValue(search.Trim());
        var searchAttributes = MergeAttributeNames(
            StandardSearchAttributeNames,
            additionalSearchAttributes);

        var searchClauses = searchAttributes
            .Select(attribute => $"({attribute}=*{escaped}*)")
            .ToList();

        if (searchClauses.Count > 0)
        {
            parts.Add($"(|{string.Concat(searchClauses)})");
        }

        return $"(&{string.Concat(parts.Where(static part => !string.IsNullOrEmpty(part)))})";
    }

    public static string[] MergeAttributeNames(params IEnumerable<string>[] sources)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var source in sources)
        {
            foreach (var name in source)
            {
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                {
                    continue;
                }

                result.Add(name);
            }
        }

        return result.ToArray();
    }

    public static bool IsSensitiveAttributeName(string attributeName) =>
        attributeName.Contains("password", StringComparison.OrdinalIgnoreCase)
        || attributeName.Contains("credential", StringComparison.OrdinalIgnoreCase)
        || attributeName.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || attributeName.Contains("token", StringComparison.OrdinalIgnoreCase);
}
