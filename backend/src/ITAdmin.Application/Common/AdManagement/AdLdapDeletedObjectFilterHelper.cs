using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdLdapDeletedObjectFilterHelper
{
    public const string ShowDeletedControlOid = "1.2.840.113556.1.4.417";

    private static readonly string[] SearchAttributeNames =
    [
        "name",
        "displayName",
        "sAMAccountName",
        "userPrincipalName",
        "cn",
        "distinguishedName",
    ];

    public static string ResolveDeletedObjectsSearchBase(string? defaultNamingContext)
    {
        if (string.IsNullOrWhiteSpace(defaultNamingContext))
        {
            return string.Empty;
        }

        return $"CN=Deleted Objects,{defaultNamingContext.Trim()}";
    }

    public static bool IsQueryEnabled(
        string? search,
        AdDeletedObjectTypeFilter type,
        bool includeAll = false) =>
        includeAll
        || type != AdDeletedObjectTypeFilter.All
        || AdLdapAttributeCatalog.IsSearchTermValid(search);

    public static string BuildDeletedObjectSearchFilter(
        string? search,
        AdDeletedObjectTypeFilter type,
        bool includeAll = false)
    {
        var parts = new List<string> { "(isDeleted=TRUE)" };
        parts.Add(BuildTypeFilter(type));

        if (!includeAll && AdLdapAttributeCatalog.IsSearchTermValid(search))
        {
            var escaped = AdLdapFilterHelper.EscapeFilterValue(search!.Trim());
            var searchClauses = SearchAttributeNames
                .Select(attribute => $"({attribute}=*{escaped}*)")
                .ToList();
            parts.Add($"(|{string.Concat(searchClauses)})");
        }

        return $"(&{string.Concat(parts)})";
    }

    public static string BuildDeletedObjectGuidFilter(Guid objectGuid)
    {
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid);
        return $"(&(isDeleted=TRUE)(objectGUID={guidFilter}))";
    }

    private static string BuildTypeFilter(AdDeletedObjectTypeFilter type) =>
        type switch
        {
            AdDeletedObjectTypeFilter.User =>
                "(&(objectClass=user)(!(objectClass=computer)))",
            AdDeletedObjectTypeFilter.Group => "(objectClass=group)",
            AdDeletedObjectTypeFilter.Computer => "(objectClass=computer)",
            _ =>
                "(|(&(objectClass=user)(!(objectClass=computer)))(objectClass=group)(objectClass=computer))",
        };
}
