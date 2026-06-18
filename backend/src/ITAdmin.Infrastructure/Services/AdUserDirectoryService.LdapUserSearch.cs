using System.DirectoryServices.Protocols;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private static string BuildUserObjectGuidFilter(Guid objectGuid) =>
        $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))(objectGUID={AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid)}))";

    private static bool TryFindUserEntryByObjectGuid(
        LdapConnection ldapConnection,
        AdManagementConnectionParameters connection,
        Guid objectGuid,
        string[] attributes,
        out SearchResultEntry entry)
    {
        entry = null!;
        foreach (var searchBase in AdLdapUserSearchBases.ResolveDistinctSearchBases(connection))
        {
            if (TrySearchUserEntryByObjectGuid(ldapConnection, searchBase, objectGuid, attributes, out entry))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySearchUserEntryByObjectGuid(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        string[] attributes,
        out SearchResultEntry entry)
    {
        entry = null!;
        var searchRequest = new SearchRequest(
            searchBase,
            BuildUserObjectGuidFilter(objectGuid),
            SearchScope.Subtree,
            attributes)
        {
            SizeLimit = 2,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        entry = response.Entries[0];
        return true;
    }
}
