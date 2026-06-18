using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private AdUserUpdateRollbackResult TryRollbackAppliedChanges(
        LdapConnection ldapConnection,
        ref string currentDistinguishedName,
        IReadOnlyList<AdUserUpdateAppliedChange> appliedChanges,
        UpdateAdUserRequest request)
    {
        if (appliedChanges.Count == 0)
        {
            return AdUserUpdateRollbackResult.NotRequired();
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
                    RollbackRename(ldapConnection, ref currentDistinguishedName, appliedChange);
                }
                else if (appliedChange.ChangeKind == AdUserUpdateScalarChangeKind.Delete)
                {
                    RollbackDeleteChange(
                        ldapConnection,
                        currentDistinguishedName,
                        appliedChange,
                        request);
                }
                else
                {
                    RollbackReplaceChange(
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
                    "AD user update rollback failed for attribute {AttributeName}. ActorUserId={ActorUserId}",
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
            return new AdUserUpdateRollbackResult
            {
                Status = AdUserUpdateRollbackStatus.Succeeded,
                RolledBackChanges = rolledBack,
                Errors = errors,
            };
        }

        if (rolledBack.Count > 0)
        {
            return new AdUserUpdateRollbackResult
            {
                Status = AdUserUpdateRollbackStatus.PartiallySucceeded,
                RolledBackChanges = rolledBack,
                Errors = errors,
            };
        }

        return new AdUserUpdateRollbackResult
        {
            Status = AdUserUpdateRollbackStatus.Failed,
            RolledBackChanges = rolledBack,
            Errors = errors,
        };
    }

    private void RollbackReplaceChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdUserUpdateAppliedChange appliedChange,
        UpdateAdUserRequest request)
    {
        if (appliedChange.OldValues is null || appliedChange.OldValues.Length == 0)
        {
            ExecuteLdapModificationWithoutTracking(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Delete,
                appliedChange.AttributeName!,
                appliedChange.UpdateStep,
                request);
            return;
        }

        ExecuteLdapModificationWithoutTracking(
            ldapConnection,
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            appliedChange.AttributeName!,
            appliedChange.UpdateStep,
            request,
            appliedChange.OldValues);
    }

    private void RollbackDeleteChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdUserUpdateAppliedChange appliedChange,
        UpdateAdUserRequest request)
    {
        if (appliedChange.OldValues is null || appliedChange.OldValues.Length == 0)
        {
            return;
        }

        ExecuteLdapModificationWithoutTracking(
            ldapConnection,
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            appliedChange.AttributeName!,
            appliedChange.UpdateStep,
            request,
            appliedChange.OldValues);
    }

    private void RollbackRename(
        LdapConnection ldapConnection,
        ref string currentDistinguishedName,
        AdUserUpdateAppliedChange appliedChange)
    {
        if (string.IsNullOrWhiteSpace(appliedChange.PreviousDistinguishedName)
            || string.IsNullOrWhiteSpace(appliedChange.PreviousCommonName)
            || string.IsNullOrWhiteSpace(appliedChange.ParentDistinguishedName))
        {
            return;
        }

        var oldRdn = AdLdapDnHelper.BuildCommonNameRdn(appliedChange.PreviousCommonName);
        var modifyDnRequest = new ModifyDNRequest(
            currentDistinguishedName,
            appliedChange.ParentDistinguishedName,
            oldRdn);
        SendLdapRequestUnchecked(ldapConnection, modifyDnRequest);
        currentDistinguishedName = appliedChange.PreviousDistinguishedName;
    }

    private static IReadOnlyList<string> GetAppliedChangeLogNames(
        IReadOnlyList<AdUserUpdateAppliedChange> appliedChanges) =>
        appliedChanges.Select(static change => change.LogAttributeName).ToList();
}
