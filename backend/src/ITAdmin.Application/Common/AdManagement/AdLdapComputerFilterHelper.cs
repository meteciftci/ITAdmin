using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdLdapComputerFilterHelper
{
    public const string ComputerOperatingSystemOptionsBaseFilter =
        "(&(objectCategory=computer)(objectClass=computer)(operatingSystem=*))";

    public static string BuildComputerOperatingSystemOptionsFilter() =>
        ComputerOperatingSystemOptionsBaseFilter;

    public static string BuildComputerDirectorySearchFilter(
        string searchTerm,
        AdUserStatusFilter status,
        string? operatingSystem = null)
    {
        var parts = new List<string>
        {
            "(objectCategory=computer)",
            "(objectClass=computer)",
        };

        parts.Add(status switch
        {
            AdUserStatusFilter.Active => "(!(userAccountControl:1.2.840.113556.1.4.803:=2))",
            AdUserStatusFilter.Disabled => "(userAccountControl:1.2.840.113556.1.4.803:=2)",
            _ => string.Empty,
        });

        if (!string.IsNullOrWhiteSpace(operatingSystem))
        {
            var escapedOperatingSystem = AdLdapFilterHelper.EscapeFilterValue(operatingSystem.Trim());
            parts.Add($"(operatingSystem={escapedOperatingSystem})");
        }

        var escaped = AdLdapFilterHelper.EscapeFilterValue(searchTerm.Trim());
        parts.Add(
            "(|" +
            $"(cn=*{escaped}*)" +
            $"(name=*{escaped}*)" +
            $"(sAMAccountName=*{escaped}*)" +
            $"(dNSHostName=*{escaped}*)" +
            $"(operatingSystem=*{escaped}*)" +
            ")");

        return $"(&{string.Concat(parts.Where(static part => !string.IsNullOrEmpty(part)))})";
    }

    public static string BuildComputerObjectGuidFilter(Guid objectGuid)
    {
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid);
        return $"(&(objectCategory=computer)(objectClass=computer)(objectGUID={guidFilter}))";
    }
}
