using System.DirectoryServices.Protocols;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private sealed record AdUserUpdatePreflightFailure(
        string AttributeName,
        string UserMessage,
        string EnglishDiagnosticMessage);

    private AdUserUpdatePreflightFailure? RunUpdatePreflightChecks(
        LdapConnection ldapConnection,
        string searchBase,
        AdUserUpdateChangePlan changePlan)
    {
        foreach (var scalarChange in changePlan.ScalarChanges)
        {
            if (string.Equals(scalarChange.AttributeName, "sAMAccountName", StringComparison.OrdinalIgnoreCase))
            {
                if (HasDuplicateAttributeValue(
                        ldapConnection,
                        searchBase,
                        "sAMAccountName",
                        scalarChange.NewValues[0],
                        changePlan.UserObjectGuid))
                {
                    return new AdUserUpdatePreflightFailure(
                        "sAMAccountName",
                        AdManagementApiMessageKeys.OperationFailures.PreflightSamAccountNameDuplicate,
                        "The sAMAccountName value is already used by another AD object.");
                }
            }

            if (string.Equals(scalarChange.AttributeName, "userPrincipalName", StringComparison.OrdinalIgnoreCase))
            {
                if (HasDuplicateAttributeValue(
                        ldapConnection,
                        searchBase,
                        "userPrincipalName",
                        scalarChange.NewValues[0],
                        changePlan.UserObjectGuid))
                {
                    return new AdUserUpdatePreflightFailure(
                        "userPrincipalName",
                        AdManagementApiMessageKeys.OperationFailures.PreflightUpnDuplicate,
                        "The userPrincipalName value is already used by another AD object.");
                }
            }
        }

        if (changePlan.RequiresRename && changePlan.RenameChange is not null)
        {
            if (HasDuplicateCnInParentOu(
                    ldapConnection,
                    changePlan.RenameChange.ParentDistinguishedName,
                    changePlan.RenameChange.RequestedCommonName,
                    changePlan.UserObjectGuid))
            {
                return new AdUserUpdatePreflightFailure(
                    "cn",
                    AdManagementApiMessageKeys.OperationFailures.PreflightCnDuplicate,
                    "The CN value is already used by another AD object in the target OU.");
            }
        }

        return null;
    }

    private static bool HasDuplicateAttributeValue(
        LdapConnection ldapConnection,
        string searchBase,
        string attributeName,
        string value,
        Guid excludeObjectGuid)
    {
        var escapedValue = AdLdapFilterHelper.EscapeFilterValue(value);
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(excludeObjectGuid);
        var filter =
            $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))({attributeName}={escapedValue})(!(objectGUID={guidFilter})))";

        return ExistsForPreflight(ldapConnection, searchBase, filter, SearchScope.Subtree);
    }

    private static bool HasDuplicateCnInParentOu(
        LdapConnection ldapConnection,
        string parentDistinguishedName,
        string commonName,
        Guid excludeObjectGuid)
    {
        var escapedCn = AdLdapFilterHelper.EscapeFilterValue(commonName);
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(excludeObjectGuid);
        var filter =
            $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))(cn={escapedCn})(!(objectGUID={guidFilter})))";

        return ExistsForPreflight(ldapConnection, parentDistinguishedName, filter, SearchScope.OneLevel);
    }

    private static bool ExistsForPreflight(
        LdapConnection ldapConnection,
        string searchBase,
        string filter,
        SearchScope scope)
    {
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            scope,
            "objectGUID",
            "distinguishedName")
        {
            SizeLimit = 2,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        return response.ResultCode == ResultCode.Success && response.Entries.Count > 0;
    }
}
