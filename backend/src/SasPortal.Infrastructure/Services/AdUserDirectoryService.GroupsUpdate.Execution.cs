using System.DirectoryServices.Protocols;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private static class AdGroupUpdateSteps
    {
        public const string LoadGroup = "LoadGroup";
        public const string Preflight = "Preflight";
        public const string RenameCn = "RenameCn";
        public const string UpdateBasicAttribute = "UpdateBasicAttribute";
        public const string ReloadGroup = "ReloadGroup";
        public const string UpdateGroup = "UpdateGroup";
    }

    private void ExecuteGroupUpdateChangePlan(
        LdapConnection ldapConnection,
        ref string distinguishedName,
        AdGroupUpdateChangePlan changePlan,
        UpdateAdGroupRequest request,
        IList<AdGroupUpdateAppliedChange> appliedChanges)
    {
        foreach (var scalarChange in changePlan.GetOrderedScalarChanges())
        {
            ExecuteGroupScalarChange(
                ldapConnection,
                distinguishedName,
                scalarChange,
                request,
                appliedChanges);
        }

        if (changePlan.RequiresRename && changePlan.RenameChange is not null)
        {
            distinguishedName = ExecuteGroupRenameChange(
                ldapConnection,
                distinguishedName,
                changePlan.RenameChange,
                request,
                appliedChanges);
        }
    }

    private void ExecuteGroupScalarChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdGroupUpdateScalarChange change,
        UpdateAdGroupRequest request,
        IList<AdGroupUpdateAppliedChange> appliedChanges)
    {
        if (change.ChangeKind == AdUserUpdateScalarChangeKind.Delete)
        {
            ExecuteGroupLdapModification(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Delete,
                change.AttributeName,
                change.UpdateStep,
                request);
            appliedChanges.Add(
                new AdGroupUpdateAppliedChange
                {
                    LogAttributeName = change.AttributeName,
                    UpdateStep = change.UpdateStep,
                    ChangeKind = change.ChangeKind,
                    IsRename = false,
                    AttributeName = change.AttributeName,
                    OldValues = change.OldValues,
                });
            return;
        }

        ExecuteGroupLdapModification(
            ldapConnection,
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            change.AttributeName,
            change.UpdateStep,
            request,
            change.NewValues);
        appliedChanges.Add(
            new AdGroupUpdateAppliedChange
            {
                LogAttributeName = change.AttributeName,
                UpdateStep = change.UpdateStep,
                ChangeKind = change.ChangeKind,
                IsRename = false,
                AttributeName = change.AttributeName,
                OldValues = change.OldValues,
                NewValues = change.NewValues,
            });
    }

    private string ExecuteGroupRenameChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdGroupUpdateRenameChange renameChange,
        UpdateAdGroupRequest request,
        IList<AdGroupUpdateAppliedChange> appliedChanges)
    {
        var parentDn = renameChange.ParentDistinguishedName;
        if (string.IsNullOrWhiteSpace(parentDn))
        {
            throw CreateUpdateGroupLdapException(
                AdLdapErrorNormalizer.InvalidDnSyntaxMessage,
                AdDirectoryFailureKind.InvalidRequest,
                new AdGroupUpdateFailureContext(
                    AdGroupUpdateSteps.RenameCn,
                    AttributeName: "cn",
                    TargetObjectGuid: request.GroupId,
                    TargetDistinguishedName: distinguishedName,
                    NormalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax,
                    EnglishMessageOverride:
                        "The technical name or distinguished name is not valid for Active Directory."));
        }

        var newRdn = AdLdapDnHelper.BuildCommonNameRdn(renameChange.RequestedCommonName);
        var modifyDnRequest = new ModifyDNRequest(distinguishedName, parentDn, newRdn);
        SendGroupLdapRequest(
            ldapConnection,
            modifyDnRequest,
            request,
            AdGroupUpdateSteps.RenameCn,
            "cn",
            request.GroupId,
            distinguishedName);

        var newDn = AdLdapDnHelper.BuildUserDistinguishedName(renameChange.RequestedCommonName, parentDn);
        appliedChanges.Add(
            new AdGroupUpdateAppliedChange
            {
                LogAttributeName = "cn",
                UpdateStep = AdGroupUpdateSteps.RenameCn,
                ChangeKind = AdUserUpdateScalarChangeKind.Replace,
                IsRename = true,
                PreviousDistinguishedName = renameChange.CurrentDistinguishedName,
                PreviousCommonName = renameChange.CurrentCommonName,
                ParentDistinguishedName = parentDn,
                NewCommonName = renameChange.RequestedCommonName,
            });

        return newDn;
    }

    private void ExecuteGroupLdapModification(
        LdapConnection ldapConnection,
        string distinguishedName,
        DirectoryAttributeOperation operation,
        string attributeName,
        string updateStep,
        UpdateAdGroupRequest request,
        params string[] values)
    {
        var modifyRequest = values.Length == 0
            ? new ModifyRequest(distinguishedName, operation, attributeName)
            : new ModifyRequest(distinguishedName, operation, attributeName, values);

        SendGroupLdapRequest(
            ldapConnection,
            modifyRequest,
            request,
            updateStep,
            attributeName,
            request.GroupId,
            distinguishedName,
            allowNoSuchAttributeOnDelete: operation == DirectoryAttributeOperation.Delete);
    }

    private void ExecuteGroupLdapModificationWithoutTracking(
        LdapConnection ldapConnection,
        string distinguishedName,
        DirectoryAttributeOperation operation,
        string attributeName,
        string updateStep,
        UpdateAdGroupRequest request,
        params string[] values)
    {
        var modifyRequest = values.Length == 0
            ? new ModifyRequest(distinguishedName, operation, attributeName)
            : new ModifyRequest(distinguishedName, operation, attributeName, values);

        SendGroupLdapRequest(
            ldapConnection,
            modifyRequest,
            request,
            updateStep,
            attributeName,
            request.GroupId,
            distinguishedName,
            allowNoSuchAttributeOnDelete: operation == DirectoryAttributeOperation.Delete);
    }

    private void SendGroupLdapRequest(
        LdapConnection ldapConnection,
        DirectoryRequest directoryRequest,
        UpdateAdGroupRequest request,
        string updateStep,
        string? attributeName,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        bool allowNoSuchAttributeOnDelete = false)
    {
        const int ldapNoSuchAttribute = 16;

        try
        {
            var response = (DirectoryResponse)ldapConnection.SendRequest(directoryRequest);
            if (response.ResultCode == ResultCode.Success)
            {
                return;
            }

            if (allowNoSuchAttributeOnDelete && response.ResultCode == ResultCode.NoSuchAttribute)
            {
                return;
            }

            var ldapResultCode = (int)response.ResultCode;
            throw CreateUpdateGroupLdapException(
                AdLdapErrorNormalizer.NormalizeMessageKey(ldapResultCode, response.ErrorMessage),
                MapGroupFailureKind(response.ResultCode),
                new AdGroupUpdateFailureContext(
                    updateStep,
                    AttributeName: attributeName,
                    LdapResultCode: ldapResultCode,
                    LdapDiagnosticMessage: response.ErrorMessage,
                    TargetObjectGuid: targetObjectGuid,
                    TargetDistinguishedName: targetDistinguishedName));
        }
        catch (DirectoryOperationException ex)
        {
            var response = ex.Response;
            if (allowNoSuchAttributeOnDelete && response?.ResultCode == ResultCode.NoSuchAttribute)
            {
                return;
            }

            var ldapResultCode = response is not null ? (int)response.ResultCode : (int?)null;
            var diagnosticMessage = response?.ErrorMessage ?? ex.Message;
            var userMessage = ldapResultCode is not null
                ? AdLdapErrorNormalizer.NormalizeMessageKey(ldapResultCode.Value, diagnosticMessage)
                : AdManagementApiMessageKeys.Groups.UpdateFailed;

            throw CreateUpdateGroupLdapException(
                userMessage,
                response is not null
                    ? MapGroupFailureKind(response.ResultCode)
                    : AdDirectoryFailureKind.ConnectionFailed,
                new AdGroupUpdateFailureContext(
                    updateStep,
                    AttributeName: attributeName,
                    LdapResultCode: ldapResultCode,
                    LdapExceptionErrorCode: ldapResultCode,
                    LdapDiagnosticMessage: diagnosticMessage,
                    TargetObjectGuid: targetObjectGuid,
                    TargetDistinguishedName: targetDistinguishedName));
        }
        catch (LdapException ex) when (allowNoSuchAttributeOnDelete && ex.ErrorCode == ldapNoSuchAttribute)
        {
            return;
        }
        catch (LdapException ex)
        {
            throw CreateUpdateGroupLdapException(
                AdLdapErrorNormalizer.NormalizeMessageKey(ex.ErrorCode, ex.Message),
                MapGroupFailureKind((ResultCode)ex.ErrorCode),
                new AdGroupUpdateFailureContext(
                    updateStep,
                    AttributeName: attributeName,
                    LdapResultCode: ex.ErrorCode,
                    LdapExceptionErrorCode: ex.ErrorCode,
                    LdapDiagnosticMessage: ex.Message,
                    TargetObjectGuid: targetObjectGuid,
                    TargetDistinguishedName: targetDistinguishedName));
        }
    }

    private UpdateGroupLdapException CreateUpdateGroupLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind,
        AdGroupUpdateFailureContext failureContext) =>
        new(userMessage, failureKind, failureContext);
}
