using System.DirectoryServices.Protocols;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private sealed record AdGroupUpdatePreflightFailure(
        string AttributeName,
        string UserMessage,
        string EnglishDiagnosticMessage);

    private AdGroupUpdatePreflightFailure? RunGroupUpdatePreflightChecks(
        LdapConnection ldapConnection,
        string searchBase,
        AdGroupUpdateChangePlan changePlan)
    {
        foreach (var scalarChange in changePlan.ScalarChanges)
        {
            if (string.Equals(scalarChange.AttributeName, "sAMAccountName", StringComparison.OrdinalIgnoreCase))
            {
                if (HasDuplicateGroupAttributeValue(
                        ldapConnection,
                        searchBase,
                        "sAMAccountName",
                        scalarChange.NewValues[0],
                        changePlan.GroupObjectGuid))
                {
                    return new AdGroupUpdatePreflightFailure(
                        "sAMAccountName",
                        AdManagementApiMessageKeys.OperationFailures.PreflightGroupSamAccountNameDuplicate,
                        "The sAMAccountName value is already used by another AD group.");
                }
            }
        }

        if (changePlan.RequiresRename && changePlan.RenameChange is not null)
        {
            if (HasDuplicateGroupCnInParentOu(
                    ldapConnection,
                    changePlan.RenameChange.ParentDistinguishedName,
                    changePlan.RenameChange.RequestedCommonName,
                    changePlan.GroupObjectGuid))
            {
                return new AdGroupUpdatePreflightFailure(
                    "cn",
                    AdManagementApiMessageKeys.OperationFailures.PreflightGroupCnDuplicate,
                    "A group with the same technical name already exists in the target OU.");
            }
        }

        return null;
    }

    private static bool HasDuplicateGroupAttributeValue(
        LdapConnection ldapConnection,
        string searchBase,
        string attributeName,
        string value,
        Guid excludeObjectGuid)
    {
        var escapedValue = AdLdapFilterHelper.EscapeFilterValue(value);
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(excludeObjectGuid);
        var filter =
            $"(&(objectCategory=group)(objectClass=group)({attributeName}={escapedValue})(!(objectGUID={guidFilter})))";

        return ExistsForGroupPreflight(ldapConnection, searchBase, filter, SearchScope.Subtree);
    }

    private static bool HasDuplicateGroupCnInParentOu(
        LdapConnection ldapConnection,
        string parentDistinguishedName,
        string commonName,
        Guid excludeObjectGuid)
    {
        var escapedCn = AdLdapFilterHelper.EscapeFilterValue(commonName);
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(excludeObjectGuid);
        var filter =
            $"(&(objectCategory=group)(objectClass=group)(cn={escapedCn})(!(objectGUID={guidFilter})))";

        return ExistsForGroupPreflight(ldapConnection, parentDistinguishedName, filter, SearchScope.OneLevel);
    }
}
