using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private AdGroupUpdateRollbackResult TryRollbackGroupAppliedChanges(
        LdapConnection ldapConnection,
        ref string currentDistinguishedName,
        IReadOnlyList<AdGroupUpdateAppliedChange> appliedChanges,
        UpdateAdGroupRequest request)
    {
        if (appliedChanges.Count == 0)
        {
            return AdGroupUpdateRollbackResult.NotRequired();
        }

        var rolledBack = new List<string>();
        var errors = new List<AdUserUpdateRollbackError>();

        for (var index = appliedChanges.Count - 1; index >= 0; index--)
        {
            var appliedChange = appliedChanges[index];
            try
            {
                if (appliedChange.IsRename)
                {
                    RollbackGroupRename(ldapConnection, ref currentDistinguishedName, appliedChange);
                }
                else if (appliedChange.ChangeKind == AdUserUpdateScalarChangeKind.Delete)
                {
                    RollbackGroupDeleteChange(
                        ldapConnection,
                        currentDistinguishedName,
                        appliedChange,
                        request);
                }
                else
                {
                    RollbackGroupReplaceChange(
                        ldapConnection,
                        currentDistinguishedName,
                        appliedChange,
                        request);
                }

                rolledBack.Add(appliedChange.LogAttributeName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "AD group update rollback failed for attribute {AttributeName}. ActorUserId={ActorUserId}",
                    appliedChange.LogAttributeName,
                    request.ActorUserId);

                errors.Add(
                    new AdUserUpdateRollbackError(
                        appliedChange.LogAttributeName,
                        $"Rollback failed for attribute {appliedChange.LogAttributeName}."));
            }
        }

        if (errors.Count == 0)
        {
            return new AdGroupUpdateRollbackResult
            {
                Status = AdUserUpdateRollbackStatus.Succeeded,
                RolledBackChanges = rolledBack,
                Errors = errors,
            };
        }

        if (rolledBack.Count > 0)
        {
            return new AdGroupUpdateRollbackResult
            {
                Status = AdUserUpdateRollbackStatus.PartiallySucceeded,
                RolledBackChanges = rolledBack,
                Errors = errors,
            };
        }

        return new AdGroupUpdateRollbackResult
        {
            Status = AdUserUpdateRollbackStatus.Failed,
            RolledBackChanges = rolledBack,
            Errors = errors,
        };
    }

    private void RollbackGroupReplaceChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdGroupUpdateAppliedChange appliedChange,
        UpdateAdGroupRequest request)
    {
        if (appliedChange.OldValues is null || appliedChange.OldValues.Length == 0)
        {
            ExecuteGroupLdapModificationWithoutTracking(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Delete,
                appliedChange.AttributeName!,
                appliedChange.UpdateStep,
                request);
            return;
        }

        ExecuteGroupLdapModificationWithoutTracking(
            ldapConnection,
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            appliedChange.AttributeName!,
            appliedChange.UpdateStep,
            request,
            appliedChange.OldValues);
    }

    private void RollbackGroupDeleteChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdGroupUpdateAppliedChange appliedChange,
        UpdateAdGroupRequest request)
    {
        if (appliedChange.OldValues is null || appliedChange.OldValues.Length == 0)
        {
            return;
        }

        ExecuteGroupLdapModificationWithoutTracking(
            ldapConnection,
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            appliedChange.AttributeName!,
            appliedChange.UpdateStep,
            request,
            appliedChange.OldValues);
    }

    private void RollbackGroupRename(
        LdapConnection ldapConnection,
        ref string currentDistinguishedName,
        AdGroupUpdateAppliedChange appliedChange)
    {
        if (string.IsNullOrWhiteSpace(appliedChange.PreviousCommonName)
            || string.IsNullOrWhiteSpace(appliedChange.ParentDistinguishedName))
        {
            return;
        }

        var newRdn = AdLdapDnHelper.BuildCommonNameRdn(appliedChange.PreviousCommonName);
        var modifyDnRequest = new ModifyDNRequest(
            currentDistinguishedName,
            appliedChange.ParentDistinguishedName,
            newRdn);
        ldapConnection.SendRequest(modifyDnRequest);
        currentDistinguishedName = AdLdapDnHelper.BuildUserDistinguishedName(
            appliedChange.PreviousCommonName,
            appliedChange.ParentDistinguishedName);
    }
}
