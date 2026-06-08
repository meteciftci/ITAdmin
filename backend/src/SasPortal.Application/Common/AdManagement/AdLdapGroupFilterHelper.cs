namespace SasPortal.Application.Common.AdManagement;

public static class AdLdapGroupFilterHelper
{
    private const string SecurityGroupBaseFilter =
        "(&(objectCategory=group)(objectClass=group)(groupType:1.2.840.113556.1.4.803:=2147483648))";

    public static string BuildSecurityGroupSearchFilter(string searchTerm)
    {
        var escaped = AdLdapFilterHelper.EscapeFilterValue(searchTerm.Trim());
        return
            "(&(objectCategory=group)(objectClass=group)(groupType:1.2.840.113556.1.4.803:=2147483648)" +
            "(|(displayName=*" + escaped + "*)(name=*" + escaped + "*)(cn=*" + escaped + "*)" +
            "(sAMAccountName=*" + escaped + "*)(description=*" + escaped + "*)(distinguishedName=*" + escaped + "*)))";
    }

    public static string BuildSecurityGroupObjectGuidFilter(Guid objectGuid)
    {
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid);
        return SecurityGroupBaseFilter[..^1] + $"(objectGUID={guidFilter}))";
    }

    public static string BuildComputerSearchFilter(string searchTerm)
    {
        var escaped = AdLdapFilterHelper.EscapeFilterValue(searchTerm.Trim());
        return
            "(&(objectCategory=computer)(objectClass=computer)" +
            "(|(displayName=*" + escaped + "*)(name=*" + escaped + "*)(cn=*" + escaped + "*)" +
            "(sAMAccountName=*" + escaped + "*)(userPrincipalName=*" + escaped + "*)" +
            "(dNSHostName=*" + escaped + "*)(description=*" + escaped + "*)))";
    }

    public static string BuildUserMemberCandidateSearchFilter(string searchTerm) =>
        "(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))" +
        BuildMemberSearchClause(searchTerm) + ")";

    public static string BuildSecurityGroupMemberCandidateSearchFilter(string searchTerm) =>
        SecurityGroupBaseFilter[..^1] + BuildMemberSearchClause(searchTerm) + ")";

    public static string BuildComputerMemberCandidateSearchFilter(string searchTerm) =>
        "(&(objectCategory=computer)(objectClass=computer)" + BuildMemberSearchClause(searchTerm) + ")";

    private static string BuildMemberSearchClause(string searchTerm)
    {
        var escaped = AdLdapFilterHelper.EscapeFilterValue(searchTerm.Trim());
        return
            "(|(displayName=*" + escaped + "*)(name=*" + escaped + "*)(cn=*" + escaped + "*)" +
            "(sAMAccountName=*" + escaped + "*)(userPrincipalName=*" + escaped + "*)" +
            "(dNSHostName=*" + escaped + "*)(description=*" + escaped + "*))";
    }
}
