using System.DirectoryServices.Protocols;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private static class AdUserUpdateSteps
    {
        public const string LoadUser = "LoadUser";
        public const string Preflight = "Preflight";
        public const string RenameCn = "RenameCn";
        public const string UpdateBasicAttribute = "UpdateBasicAttribute";
        public const string UpdateMappedAttribute = "UpdateMappedAttribute";
        public const string ReloadUser = "ReloadUser";
        public const string UpdateUser = "UpdateUser";
    }

    private AdUserUpdateChangePlan BuildUpdateChangePlan(
        UpdateAdUserRequest request,
        SearchResultEntry entry,
        string distinguishedName,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var currentScalars = BuildCurrentScalarValues(entry);
        var mappedValues = BuildCurrentMappedValuesByAttribute(entry, mappings);
        return AdUserUpdateChangePlanBuilder.Build(
            request,
            currentScalars,
            mappedValues,
            distinguishedName,
            mappings);
    }

    private static IReadOnlyDictionary<string, string?> BuildCurrentScalarValues(SearchResultEntry entry) =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["givenName"] = GetFirstString(entry, "givenName"),
            ["sn"] = GetFirstString(entry, "sn"),
            ["displayName"] = GetFirstString(entry, "displayName"),
            ["sAMAccountName"] = GetFirstString(entry, "sAMAccountName"),
            ["userPrincipalName"] = GetFirstString(entry, "userPrincipalName"),
            ["mail"] = GetFirstString(entry, "mail"),
            ["department"] = GetFirstString(entry, "department"),
        };

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildCurrentMappedValuesByAttribute(
        SearchResultEntry entry,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings.Where(static m => m.IsEnabled))
        {
            result[mapping.AttributeName] = GetAllStrings(entry, mapping.AttributeName);
        }

        return result;
    }

    private void ExecuteUpdateChangePlan(
        LdapConnection ldapConnection,
        ref string distinguishedName,
        AdUserUpdateChangePlan changePlan,
        UpdateAdUserRequest request,
        IList<AdUserUpdateAppliedChange> appliedChanges)
    {
        foreach (var scalarChange in changePlan.GetOrderedScalarChanges())
        {
            ExecuteScalarChange(
                ldapConnection,
                distinguishedName,
                scalarChange,
                request,
                appliedChanges);
        }

        foreach (var mappedChange in changePlan.MappedChanges)
        {
            ExecuteMappedChange(
                ldapConnection,
                distinguishedName,
                mappedChange,
                request,
                appliedChanges);
        }

        if (changePlan.RequiresRename && changePlan.RenameChange is not null)
        {
            distinguishedName = ExecuteRenameChange(
                ldapConnection,
                distinguishedName,
                changePlan.RenameChange,
                request,
                appliedChanges);
        }
    }

    private void ExecuteScalarChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdUserUpdateScalarChange change,
        UpdateAdUserRequest request,
        IList<AdUserUpdateAppliedChange> appliedChanges)
    {
        if (change.ChangeKind == AdUserUpdateScalarChangeKind.Delete)
        {
            ExecuteLdapModification(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Delete,
                change.AttributeName,
                change.UpdateStep,
                request);
            appliedChanges.Add(
                new AdUserUpdateAppliedChange
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

        ExecuteLdapModification(
            ldapConnection,
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            change.AttributeName,
            change.UpdateStep,
            request,
            change.NewValues);
        appliedChanges.Add(
            new AdUserUpdateAppliedChange
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

    private void ExecuteMappedChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdUserUpdateMappedChange change,
        UpdateAdUserRequest request,
        IList<AdUserUpdateAppliedChange> appliedChanges)
    {
        if (change.ChangeKind == AdUserUpdateScalarChangeKind.Delete)
        {
            ExecuteLdapModification(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Delete,
                change.AttributeName,
                change.UpdateStep,
                request);
            appliedChanges.Add(
                new AdUserUpdateAppliedChange
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

        ExecuteLdapModification(
            ldapConnection,
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            change.AttributeName,
            change.UpdateStep,
            request,
            change.NewValues);
        appliedChanges.Add(
            new AdUserUpdateAppliedChange
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

    private string ExecuteRenameChange(
        LdapConnection ldapConnection,
        string distinguishedName,
        AdUserUpdateRenameChange renameChange,
        UpdateAdUserRequest request,
        IList<AdUserUpdateAppliedChange> appliedChanges)
    {
        var parentDn = renameChange.ParentDistinguishedName;
        if (string.IsNullOrWhiteSpace(parentDn))
        {
            throw CreateUpdateUserLdapException(
                AdLdapErrorNormalizer.InvalidDnSyntaxMessage,
                AdDirectoryFailureKind.InvalidRequest,
                new AdUserUpdateFailureContext(
                    AdUserUpdateSteps.RenameCn,
                    AttributeName: "cn",
                    TargetObjectGuid: request.UserId,
                    TargetDistinguishedName: distinguishedName,
                    NormalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax,
                    EnglishMessageOverride:
                        "The display name or distinguished name is not valid for Active Directory.",
                    RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));
        }

        var newRdn = AdLdapDnHelper.BuildCommonNameRdn(renameChange.RequestedCommonName);
        var modifyDnRequest = new ModifyDNRequest(distinguishedName, parentDn, newRdn);
        SendLdapRequest(
            ldapConnection,
            modifyDnRequest,
            request,
            AdUserUpdateSteps.RenameCn,
            "cn",
            request.UserId,
            distinguishedName);

        var newDn = AdLdapDnHelper.BuildUserDistinguishedName(renameChange.RequestedCommonName, parentDn);
        appliedChanges.Add(
            new AdUserUpdateAppliedChange
            {
                LogAttributeName = "cn",
                UpdateStep = AdUserUpdateSteps.RenameCn,
                ChangeKind = AdUserUpdateScalarChangeKind.Replace,
                IsRename = true,
                PreviousDistinguishedName = renameChange.CurrentDistinguishedName,
                PreviousCommonName = renameChange.CurrentCommonName,
                ParentDistinguishedName = parentDn,
                NewCommonName = renameChange.RequestedCommonName,
            });

        return newDn;
    }

    private void ExecuteLdapModification(
        LdapConnection ldapConnection,
        string distinguishedName,
        DirectoryAttributeOperation operation,
        string attributeName,
        string updateStep,
        UpdateAdUserRequest request,
        params string[] values)
    {
        var modifyRequest = values.Length == 0
            ? new ModifyRequest(distinguishedName, operation, attributeName)
            : new ModifyRequest(distinguishedName, operation, attributeName, values);

        SendLdapRequest(
            ldapConnection,
            modifyRequest,
            request,
            updateStep,
            attributeName,
            request.UserId,
            distinguishedName,
            allowNoSuchAttributeOnDelete: operation == DirectoryAttributeOperation.Delete);
    }

    private void ExecuteLdapModificationWithoutTracking(
        LdapConnection ldapConnection,
        string distinguishedName,
        DirectoryAttributeOperation operation,
        string attributeName,
        string updateStep,
        UpdateAdUserRequest updateRequest,
        params string[] values)
    {
        var modifyRequest = values.Length == 0
            ? new ModifyRequest(distinguishedName, operation, attributeName)
            : new ModifyRequest(distinguishedName, operation, attributeName, values);

        SendLdapRequest(
            ldapConnection,
            modifyRequest,
            updateRequest: null,
            updateStep,
            attributeName,
            updateRequest.UserId,
            distinguishedName,
            allowNoSuchAttributeOnDelete: operation == DirectoryAttributeOperation.Delete);
    }

    private void SendLdapRequest(
        LdapConnection ldapConnection,
        DirectoryRequest directoryRequest,
        UpdateAdUserRequest? updateRequest,
        string updateStep,
        string? attributeName,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        bool allowNoSuchAttributeOnDelete = false)
    {
        try
        {
            var response = (DirectoryResponse)ldapConnection.SendRequest(directoryRequest);
            EnsureSuccessfulLdapResponse(
                response,
                updateRequest,
                updateStep,
                attributeName,
                targetObjectGuid,
                targetDistinguishedName,
                allowNoSuchAttributeOnDelete);
        }
        catch (DirectoryOperationException ex)
        {
            ThrowUpdateUserLdapExceptionFromDirectoryOperation(
                ex,
                updateRequest,
                updateStep,
                attributeName,
                targetObjectGuid,
                targetDistinguishedName,
                allowNoSuchAttributeOnDelete);
        }
        catch (LdapException ex) when (allowNoSuchAttributeOnDelete && ex.ErrorCode == LdapNoSuchAttribute)
        {
            return;
        }
        catch (LdapException ex)
        {
            LogLdapFailure(
                updateRequest,
                updateStep,
                attributeName,
                ex.ErrorCode,
                ex.Message,
                ex.ErrorCode,
                targetObjectGuid,
                targetDistinguishedName);

            throw CreateUpdateUserLdapException(
                AdLdapErrorNormalizer.Normalize(ex.ErrorCode, ex.Message),
                MapFailureKind((ResultCode)ex.ErrorCode),
                new AdUserUpdateFailureContext(
                    updateStep,
                    AttributeName: attributeName,
                    LdapResultCode: ex.ErrorCode,
                    LdapExceptionErrorCode: ex.ErrorCode,
                    LdapDiagnosticMessage: ex.Message,
                    TargetObjectGuid: targetObjectGuid,
                    TargetDistinguishedName: targetDistinguishedName));
        }
    }

    private static void SendLdapRequestUnchecked(
        LdapConnection ldapConnection,
        DirectoryRequest request) =>
        ldapConnection.SendRequest(request);

    private void EnsureSuccessfulLdapResponse(
        DirectoryResponse response,
        UpdateAdUserRequest? updateRequest,
        string updateStep,
        string? attributeName,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        bool allowNoSuchAttributeOnDelete)
    {
        if (response.ResultCode == ResultCode.Success)
        {
            return;
        }

        if (allowNoSuchAttributeOnDelete && response.ResultCode == ResultCode.NoSuchAttribute)
        {
            return;
        }

        var ldapResultCode = (int)response.ResultCode;
        LogLdapFailure(
            updateRequest,
            updateStep,
            attributeName,
            ldapResultCode,
            response.ErrorMessage,
            null,
            targetObjectGuid,
            targetDistinguishedName);

        throw CreateUpdateUserLdapException(
            AdLdapErrorNormalizer.Normalize(ldapResultCode, response.ErrorMessage),
            MapFailureKind(response.ResultCode),
            new AdUserUpdateFailureContext(
                updateStep,
                AttributeName: attributeName,
                LdapResultCode: ldapResultCode,
                LdapDiagnosticMessage: response.ErrorMessage,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName));
    }

    private void ThrowUpdateUserLdapExceptionFromDirectoryOperation(
        DirectoryOperationException exception,
        UpdateAdUserRequest? updateRequest,
        string updateStep,
        string? attributeName,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        bool allowNoSuchAttributeOnDelete)
    {
        var response = exception.Response;
        if (allowNoSuchAttributeOnDelete && response?.ResultCode == ResultCode.NoSuchAttribute)
        {
            return;
        }

        var ldapResultCode = response is not null ? (int)response.ResultCode : (int?)null;
        var ldapExceptionErrorCode = ldapResultCode;
        var diagnosticMessage = response?.ErrorMessage ?? exception.Message;

        LogLdapFailure(
            updateRequest,
            updateStep,
            attributeName,
            ldapResultCode,
            diagnosticMessage,
            ldapExceptionErrorCode,
            targetObjectGuid,
            targetDistinguishedName);

        var userMessage = ldapResultCode is not null
            ? AdLdapErrorNormalizer.Normalize(ldapResultCode.Value, diagnosticMessage)
            : AdLdapErrorNormalizer.UpdateUserFailedMessage;

        var failureKind = response is not null
            ? MapFailureKind(response.ResultCode)
            : AdDirectoryFailureKind.ConnectionFailed;

        throw CreateUpdateUserLdapException(
            userMessage,
            failureKind,
            new AdUserUpdateFailureContext(
                updateStep,
                AttributeName: attributeName,
                LdapResultCode: ldapResultCode,
                LdapExceptionErrorCode: ldapExceptionErrorCode,
                LdapDiagnosticMessage: diagnosticMessage,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName));
    }

}
