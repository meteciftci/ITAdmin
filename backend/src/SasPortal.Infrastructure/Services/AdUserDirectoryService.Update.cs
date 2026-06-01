using System.DirectoryServices.Protocols;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string UpdateUserFailedMessage = AdLdapErrorNormalizer.UpdateUserFailedMessage;
    private const int LdapNoSuchAttribute = 16;

    private static class AdUserUpdateSteps
    {
        public const string LoadUser = "LoadUser";
        public const string RenameCn = "RenameCn";
        public const string UpdateBasicAttribute = "UpdateBasicAttribute";
        public const string UpdateMappedAttribute = "UpdateMappedAttribute";
        public const string ReloadUser = "ReloadUser";
        public const string UpdateUser = "UpdateUser";
    }

    public async Task<AdUserDirectoryDetailResult> UpdateUserAsync(
        UpdateAdUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeUpdateRequest(request);

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdUserDirectoryDetailResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind);
        }

        var mappings = await attributeMappingService.GetMappingsAsync(cancellationToken);
        if (!AdUpdateUserRequestValidator.TryValidate(normalizedRequest, mappings, out var validationMessage))
        {
            return new AdUserDirectoryDetailResult(
                false,
                validationMessage,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var context = connectionResult.Context;
        var searchBase = ResolveDetailSearchBase(context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdUserDirectoryDetailResult(
                false,
                AdManagementNotConfiguredMessage,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        var activeMappings = mappings.Where(static mapping => mapping.IsEnabled).ToList();

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            if (!TryLoadUserForUpdate(
                    ldapConnection,
                    searchBase,
                    normalizedRequest.UserId,
                    activeMappings,
                    out var beforeDetail,
                    out var beforeEntry))
            {
                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    UserNotFoundMessage,
                    AdDirectoryFailureKind.NotFound,
                    AdUserUpdateOperationDiagnosticBuilder.BuildNotFoundJson(
                        AdUserUpdateSteps.LoadUser,
                        normalizedRequest.UserId),
                    beforeDetail,
                    beforeDetail?.DistinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            var distinguishedName = beforeDetail!.DistinguishedName;
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    UserNotFoundMessage,
                    AdDirectoryFailureKind.NotFound,
                    AdUserUpdateOperationDiagnosticBuilder.BuildNotFoundJson(
                        AdUserUpdateSteps.LoadUser,
                        normalizedRequest.UserId),
                    beforeDetail,
                    distinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            var newCommonName = AdUpdateUserRequestValidator.DeriveCommonNameFromDisplayName(normalizedRequest.DisplayName);
            var currentCommonName = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName);
            var renamed = false;
            var attributeWrites = false;
            try
            {
                if (!string.Equals(currentCommonName, newCommonName, StringComparison.OrdinalIgnoreCase))
                {
                    distinguishedName = RenameUserCommonName(
                        ldapConnection,
                        distinguishedName,
                        newCommonName,
                        normalizedRequest.UserId);
                    renamed = true;
                }

                attributeWrites = ApplyUserAttributeUpdates(
                    ldapConnection,
                    distinguishedName,
                    normalizedRequest,
                    beforeEntry!,
                    mappings);
            }
            catch (UpdateUserLdapException ex)
            {
                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    ex.UserMessage,
                    ex.FailureKind,
                    ex.OperationDiagnosticJson,
                    beforeDetail,
                    distinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            if (!renamed && !attributeWrites)
            {
                await WriteUpdateNoChangesLogsAsync(
                    normalizedRequest,
                    context.Connection,
                    beforeDetail,
                    distinguishedName,
                    cancellationToken);

                return new AdUserDirectoryDetailResult(true, string.Empty, beforeDetail);
            }

            if (!TryLoadUserForUpdate(
                    ldapConnection,
                    searchBase,
                    normalizedRequest.UserId,
                    activeMappings,
                    out var afterDetail,
                    out _)
                || afterDetail is null)
            {
                LogLdapDiagnostic(
                    normalizedRequest,
                    AdUserUpdateSteps.ReloadUser,
                    null,
                    null,
                    null,
                    null,
                    normalizedRequest.UserId,
                    distinguishedName);

                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    UpdateUserFailedMessage,
                    AdDirectoryFailureKind.ConnectionFailed,
                    AdUserUpdateOperationDiagnosticBuilder.BuildGenericFailureJson(
                        AdUserUpdateSteps.ReloadUser,
                        AdUserUpdateNormalizedReasons.ConnectionFailed,
                        "The AD user could not be reloaded after update.",
                        normalizedRequest.UserId,
                        distinguishedName),
                    beforeDetail,
                    distinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            await WriteUpdateSuccessLogsAsync(
                normalizedRequest,
                context.Connection,
                beforeDetail,
                afterDetail,
                distinguishedName,
                cancellationToken);

            return new AdUserDirectoryDetailResult(true, string.Empty, afterDetail);
        }
        catch (LdapException ex)
        {
            LogLdapDiagnostic(
                normalizedRequest,
                AdUserUpdateSteps.UpdateUser,
                null,
                ex.ErrorCode,
                ex.Message,
                ex.ErrorCode,
                normalizedRequest.UserId,
                null);

            return await FailUpdateAsync(
                normalizedRequest,
                context.Connection,
                AdLdapErrorNormalizer.Normalize(ex.ErrorCode, ex.Message),
                AdDirectoryFailureKind.ConnectionFailed,
                AdUserUpdateOperationDiagnosticBuilder.BuildJson(
                    new AdUserUpdateFailureContext(
                        AdUserUpdateSteps.UpdateUser,
                        LdapResultCode: ex.ErrorCode,
                        LdapExceptionErrorCode: ex.ErrorCode,
                        LdapDiagnosticMessage: ex.Message,
                        TargetObjectGuid: normalizedRequest.UserId)),
                null,
                null,
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD user update unexpected failure. ActorUserId={ActorUserId}; {Diagnostic}",
                normalizedRequest.ActorUserId,
                AdLdapUpdateDiagnosticLog.Format(
                    AdUserUpdateSteps.UpdateUser,
                    null,
                    null,
                    null,
                    null,
                    normalizedRequest.UserId,
                    null));

            return await FailUpdateAsync(
                normalizedRequest,
                context.Connection,
                UpdateUserFailedMessage,
                AdDirectoryFailureKind.ConnectionFailed,
                AdUserUpdateOperationDiagnosticBuilder.BuildGenericFailureJson(
                    AdUserUpdateSteps.UpdateUser,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The AD user update failed.",
                    normalizedRequest.UserId),
                null,
                null,
                null,
                null,
                cancellationToken);
        }
    }

    private static UpdateAdUserRequest NormalizeUpdateRequest(UpdateAdUserRequest request) =>
        request with
        {
            GivenName = request.GivenName.Trim(),
            Surname = request.Surname.Trim(),
            DisplayName = request.DisplayName.Trim(),
            SamAccountName = request.SamAccountName.Trim(),
            UserPrincipalName = request.UserPrincipalName.Trim(),
            Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
            Mail = request.Mail is null ? null : (string.IsNullOrWhiteSpace(request.Mail) ? string.Empty : request.Mail.Trim()),
            MappedAttributes = request.MappedAttributes
                .Select(static attribute => new UpdateAdUserMappedAttributeRequest(
                    attribute.LogicalField.Trim(),
                    attribute.Value))
                .ToList(),
        };

    private static bool TryLoadUserForUpdate(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        IReadOnlyList<AdAttributeMappingItem> activeMappings,
        out AdUserDetail? detail,
        out SearchResultEntry? entry)
    {
        detail = null;
        entry = null;
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid);
        var filter =
            $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))(objectGUID={guidFilter}))";

        var detailAttributes = AdLdapAttributeCatalog.BuildDetailLdapAttributeNames(activeMappings);
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            detailAttributes)
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
        return TryMapDetailItem(entry, activeMappings, out detail);
    }

    private string RenameUserCommonName(
        LdapConnection ldapConnection,
        string distinguishedName,
        string newCommonName,
        Guid targetObjectGuid)
    {
        var parentDn = AdLdapDnHelper.GetParentDistinguishedName(distinguishedName);
        if (string.IsNullOrWhiteSpace(parentDn))
        {
            throw CreateUpdateUserLdapException(
                AdLdapErrorNormalizer.InvalidDnSyntaxMessage,
                AdDirectoryFailureKind.InvalidRequest,
                new AdUserUpdateFailureContext(
                    AdUserUpdateSteps.RenameCn,
                    AttributeName: "cn",
                    TargetObjectGuid: targetObjectGuid,
                    TargetDistinguishedName: distinguishedName,
                    NormalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax,
                    EnglishMessageOverride:
                        "The display name or distinguished name is not valid for Active Directory."));
        }

        var newRdn = AdLdapDnHelper.BuildCommonNameRdn(newCommonName);
        var modifyDnRequest = new ModifyDNRequest(distinguishedName, parentDn, newRdn);
        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyDnRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            var ldapResultCode = (int)response.ResultCode;
            LogLdapFailure(
                null,
                AdUserUpdateSteps.RenameCn,
                "cn",
                ldapResultCode,
                response.ErrorMessage,
                null,
                targetObjectGuid,
                distinguishedName);

            throw CreateUpdateUserLdapException(
                AdLdapErrorNormalizer.Normalize(ldapResultCode, response.ErrorMessage),
                MapFailureKind(response.ResultCode),
                new AdUserUpdateFailureContext(
                    AdUserUpdateSteps.RenameCn,
                    AttributeName: "cn",
                    LdapResultCode: ldapResultCode,
                    LdapDiagnosticMessage: response.ErrorMessage,
                    TargetObjectGuid: targetObjectGuid,
                    TargetDistinguishedName: distinguishedName));
        }

        return AdLdapDnHelper.BuildUserDistinguishedName(newCommonName, parentDn);
    }

    private bool ApplyUserAttributeUpdates(
        LdapConnection ldapConnection,
        string distinguishedName,
        UpdateAdUserRequest request,
        SearchResultEntry entry,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var wrote = false;

        if (ScalarValueChanged(entry, "givenName", request.GivenName))
        {
            ApplyLdapModification(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Replace,
                "givenName",
                AdUserUpdateSteps.UpdateBasicAttribute,
                request,
                request.GivenName);
            wrote = true;
        }

        if (ScalarValueChanged(entry, "sn", request.Surname))
        {
            ApplyLdapModification(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Replace,
                "sn",
                AdUserUpdateSteps.UpdateBasicAttribute,
                request,
                request.Surname);
            wrote = true;
        }

        if (ScalarValueChanged(entry, "displayName", request.DisplayName))
        {
            ApplyLdapModification(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Replace,
                "displayName",
                AdUserUpdateSteps.UpdateBasicAttribute,
                request,
                request.DisplayName);
            wrote = true;
        }

        if (ScalarValueChanged(entry, "sAMAccountName", request.SamAccountName))
        {
            ApplyLdapModification(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Replace,
                "sAMAccountName",
                AdUserUpdateSteps.UpdateBasicAttribute,
                request,
                request.SamAccountName);
            wrote = true;
        }

        if (ScalarValueChanged(entry, "userPrincipalName", request.UserPrincipalName))
        {
            ApplyLdapModification(
                ldapConnection,
                distinguishedName,
                DirectoryAttributeOperation.Replace,
                "userPrincipalName",
                AdUserUpdateSteps.UpdateBasicAttribute,
                request,
                request.UserPrincipalName);
            wrote = true;
        }

        if (request.Mail is not null
            && ApplyOptionalScalarAttributeUpdate(
                ldapConnection,
                distinguishedName,
                entry,
                "mail",
                request.Mail,
                request))
        {
            wrote = true;
        }

        if (request.Department is not null
            && ApplyOptionalScalarAttributeUpdate(
                ldapConnection,
                distinguishedName,
                entry,
                "department",
                request.Department,
                request))
        {
            wrote = true;
        }

        if (ApplyMappedAttributeUpdates(
                ldapConnection,
                distinguishedName,
                entry,
                request.MappedAttributes,
                mappings,
                request))
        {
            wrote = true;
        }

        return wrote;
    }

    private static bool ScalarValueChanged(
        SearchResultEntry entry,
        string attributeName,
        string requestedValue) =>
        AdScalarAttributeComparer.HasChanged(GetFirstString(entry, attributeName), requestedValue);

    private bool ApplyOptionalScalarAttributeUpdate(
        LdapConnection ldapConnection,
        string distinguishedName,
        SearchResultEntry entry,
        string attributeName,
        string requestedValue,
        UpdateAdUserRequest request)
    {
        var existing = GetAllStrings(entry, attributeName);
        var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(requestedValue, existing);
        switch (action)
        {
            case AdMappedAttributeLdapAction.Skip:
                return false;
            case AdMappedAttributeLdapAction.Delete:
                ApplyLdapModification(
                    ldapConnection,
                    distinguishedName,
                    DirectoryAttributeOperation.Delete,
                    attributeName,
                    AdUserUpdateSteps.UpdateBasicAttribute,
                    request);
                return true;
            case AdMappedAttributeLdapAction.Replace:
                ApplyLdapModification(
                    ldapConnection,
                    distinguishedName,
                    DirectoryAttributeOperation.Replace,
                    attributeName,
                    AdUserUpdateSteps.UpdateBasicAttribute,
                    request,
                    requestedValue.Trim());
                return true;
            default:
                return false;
        }
    }

    private bool ApplyMappedAttributeUpdates(
        LdapConnection ldapConnection,
        string distinguishedName,
        SearchResultEntry entry,
        IReadOnlyList<UpdateAdUserMappedAttributeRequest> mappedAttributes,
        IReadOnlyList<AdAttributeMappingItem> mappings,
        UpdateAdUserRequest request)
    {
        var wrote = false;
        var editableMappings = mappings
            .Where(static mapping =>
                mapping.IsEnabled
                && mapping.IsEditable
                && !AdReservedCoreAttributes.IsReserved(mapping.AttributeName))
            .ToDictionary(static mapping => mapping.LogicalField, StringComparer.Ordinal);

        foreach (var mappedAttribute in mappedAttributes)
        {
            if (!editableMappings.TryGetValue(mappedAttribute.LogicalField, out var mapping))
            {
                continue;
            }

            var existingValues = GetAllStrings(entry, mapping.AttributeName);
            var requestedValue = ExtractMappedAttributeValue(mappedAttribute.Value);
            var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(requestedValue, existingValues);

            switch (action)
            {
                case AdMappedAttributeLdapAction.Skip:
                    continue;
                case AdMappedAttributeLdapAction.Delete:
                    ApplyLdapModification(
                        ldapConnection,
                        distinguishedName,
                        DirectoryAttributeOperation.Delete,
                        mapping.AttributeName,
                        AdUserUpdateSteps.UpdateMappedAttribute,
                        request);
                    wrote = true;
                    continue;
                case AdMappedAttributeLdapAction.Replace:
                    ApplyLdapModification(
                        ldapConnection,
                        distinguishedName,
                        DirectoryAttributeOperation.Replace,
                        mapping.AttributeName,
                        AdUserUpdateSteps.UpdateMappedAttribute,
                        request,
                        requestedValue!);
                    wrote = true;
                    continue;
                default:
                    continue;
            }
        }

        return wrote;
    }

    private void ApplyLdapModification(
        LdapConnection ldapConnection,
        string distinguishedName,
        DirectoryAttributeOperation operation,
        string attributeName,
        string updateStep,
        UpdateAdUserRequest request,
        params string[] values)
    {
        try
        {
            var modifyRequest = values.Length == 0
                ? new ModifyRequest(distinguishedName, operation, attributeName)
                : new ModifyRequest(distinguishedName, operation, attributeName, values);

            var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
            if (response.ResultCode == ResultCode.Success)
            {
                return;
            }

            if (operation == DirectoryAttributeOperation.Delete
                && response.ResultCode == ResultCode.NoSuchAttribute)
            {
                return;
            }

            var ldapResultCode = (int)response.ResultCode;
            LogLdapFailure(
                request,
                updateStep,
                attributeName,
                ldapResultCode,
                response.ErrorMessage,
                null,
                request.UserId,
                distinguishedName);

            throw CreateUpdateUserLdapException(
                AdLdapErrorNormalizer.Normalize(ldapResultCode, response.ErrorMessage),
                MapFailureKind(response.ResultCode),
                new AdUserUpdateFailureContext(
                    updateStep,
                    AttributeName: attributeName,
                    LdapResultCode: ldapResultCode,
                    LdapDiagnosticMessage: response.ErrorMessage,
                    TargetObjectGuid: request.UserId,
                    TargetDistinguishedName: distinguishedName));
        }
        catch (LdapException ex) when (operation == DirectoryAttributeOperation.Delete && ex.ErrorCode == LdapNoSuchAttribute)
        {
            return;
        }
        catch (LdapException ex)
        {
            LogLdapFailure(
                request,
                updateStep,
                attributeName,
                ex.ErrorCode,
                ex.Message,
                ex.ErrorCode,
                request.UserId,
                distinguishedName);

            throw CreateUpdateUserLdapException(
                AdLdapErrorNormalizer.Normalize(ex.ErrorCode, ex.Message),
                MapFailureKind((ResultCode)ex.ErrorCode),
                new AdUserUpdateFailureContext(
                    updateStep,
                    AttributeName: attributeName,
                    LdapResultCode: ex.ErrorCode,
                    LdapExceptionErrorCode: ex.ErrorCode,
                    LdapDiagnosticMessage: ex.Message,
                    TargetObjectGuid: request.UserId,
                    TargetDistinguishedName: distinguishedName));
        }
    }

    private static UpdateUserLdapException CreateUpdateUserLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind,
        AdUserUpdateFailureContext diagnosticContext) =>
        new(
            userMessage,
            failureKind,
            AdUserUpdateOperationDiagnosticBuilder.BuildJson(diagnosticContext));

    private void LogLdapFailure(
        UpdateAdUserRequest? request,
        string updateStep,
        string? attributeName,
        int? ldapResultCode,
        string? ldapErrorMessage,
        int? ldapExceptionErrorCode,
        Guid targetObjectGuid,
        string? targetDistinguishedName)
    {
        LogLdapDiagnostic(
            request,
            updateStep,
            attributeName,
            ldapResultCode,
            ldapErrorMessage,
            ldapExceptionErrorCode,
            targetObjectGuid,
            targetDistinguishedName);
    }

    private void LogLdapDiagnostic(
        UpdateAdUserRequest? request,
        string updateStep,
        string? attributeName,
        int? ldapResultCode,
        string? ldapErrorMessage,
        int? ldapExceptionErrorCode,
        Guid targetObjectGuid,
        string? targetDistinguishedName)
    {
        logger.LogWarning(
            "AD user LDAP update step failed. ActorUserId={ActorUserId} ActorUserName={ActorUserName}; {Diagnostic}",
            request?.ActorUserId,
            request?.ActorUserName,
            AdLdapUpdateDiagnosticLog.Format(
                updateStep,
                attributeName,
                ldapResultCode,
                ldapErrorMessage,
                ldapExceptionErrorCode,
                targetObjectGuid,
                targetDistinguishedName));
    }

    private static AdDirectoryFailureKind MapFailureKind(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdDirectoryFailureKind.NotFound,
            ResultCode.EntryAlreadyExists
                or ResultCode.AttributeOrValueExists
                or ResultCode.ConstraintViolation
                or ResultCode.InvalidDNSyntax
                or ResultCode.NamingViolation
                or ResultCode.UnwillingToPerform => AdDirectoryFailureKind.InvalidRequest,
            _ => AdDirectoryFailureKind.ConnectionFailed,
        };

    private async Task<AdUserDirectoryDetailResult> FailUpdateAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        string message,
        AdDirectoryFailureKind failureKind,
        string operationDiagnosticJson,
        AdUserDetail? beforeDetail,
        string? targetDistinguishedName,
        AdUserDetail? afterDetail,
        string? afterDistinguishedName,
        CancellationToken cancellationToken)
    {
        await WriteUpdateFailureLogsAsync(
            request,
            connection,
            beforeDetail,
            targetDistinguishedName,
            afterDetail,
            afterDistinguishedName,
            operationDiagnosticJson,
            cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = "Update",
                EntityName = "AdUser",
                EntityId = request.UserId.ToString("D"),
                Description = $"AD user update failed: {request.SamAccountName}.",
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
            },
            cancellationToken);

        return new AdUserDirectoryDetailResult(false, message, null, failureKind);
    }

    private async Task WriteUpdateNoChangesLogsAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        AdUserDetail beforeDetail,
        string targetDistinguishedName,
        CancellationToken cancellationToken)
    {
        await WriteUpdateOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Succeeded,
            beforeDetail,
            targetDistinguishedName,
            beforeDetail,
            beforeDetail.DistinguishedName,
            errorMessage: null,
            requestSummaryJson: """{"changeStatus":"NoChangesDetected"}""",
            cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = "Update",
                EntityName = "AdUser",
                EntityId = beforeDetail.Id,
                Description = $"AD user update skipped (no changes): {beforeDetail.SamAccountName}.",
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
            },
            cancellationToken);
    }

    private async Task WriteUpdateSuccessLogsAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        AdUserDetail beforeDetail,
        AdUserDetail afterDetail,
        string targetDistinguishedName,
        CancellationToken cancellationToken)
    {
        await WriteUpdateOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Succeeded,
            beforeDetail,
            targetDistinguishedName,
            afterDetail,
            afterDetail.DistinguishedName,
            errorMessage: null,
            requestSummaryJson: null,
            cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = "Update",
                EntityName = "AdUser",
                EntityId = afterDetail.Id,
                Description = $"AD user updated: {afterDetail.SamAccountName}.",
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
            },
            cancellationToken);
    }

    private Task WriteUpdateFailureLogsAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        AdUserDetail? beforeDetail,
        string? targetDistinguishedName,
        AdUserDetail? afterDetail,
        string? afterDistinguishedName,
        string operationDiagnosticJson,
        CancellationToken cancellationToken) =>
        WriteUpdateOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Failed,
            beforeDetail,
            targetDistinguishedName,
            afterDetail,
            afterDistinguishedName,
            errorMessage: operationDiagnosticJson,
            requestSummaryJson: null,
            cancellationToken);

    private async Task WriteUpdateOperationLogAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        string status,
        AdUserDetail? beforeDetail,
        string? targetDistinguishedName,
        AdUserDetail? afterDetail,
        string? afterDistinguishedName,
        string? errorMessage,
        string? requestSummaryJson,
        CancellationToken cancellationToken)
    {
        var beforeSnapshot = beforeDetail is null ? null : SerializeUpdateSnapshot(beforeDetail);
        var afterSnapshot = afterDetail is null ? null : SerializeUpdateSnapshot(afterDetail);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.UserUpdate,
                Status = status,
                TargetObjectType = AdManagementTargetUserTypes.AdUser,
                TargetObjectGuid = afterDetail?.Id ?? beforeDetail?.Id ?? request.UserId.ToString("D"),
                TargetDistinguishedName = afterDistinguishedName
                    ?? targetDistinguishedName
                    ?? beforeDetail?.DistinguishedName,
                TargetSamAccountName = afterDetail?.SamAccountName ?? beforeDetail?.SamAccountName ?? request.SamAccountName,
                BeforeSnapshotJson = beforeSnapshot,
                AfterSnapshotJson = afterSnapshot,
                ErrorMessage = errorMessage,
                RequestSummaryJson = requestSummaryJson,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private static string SerializeUpdateSnapshot(AdUserDetail detail) =>
        JsonSerializer.Serialize(AdUserUpdateSnapshotBuilder.Build(
            detail.GivenName,
            detail.Surname,
            detail.DisplayName,
            detail.SamAccountName,
            detail.UserPrincipalName,
            detail.Mail,
            detail.Department,
            detail.DistinguishedName,
            detail.MappedAttributes));

    private sealed class UpdateUserLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind,
        string operationDiagnosticJson)
        : Exception(userMessage)
    {
        public string UserMessage { get; } = userMessage;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
        public string OperationDiagnosticJson { get; } = operationDiagnosticJson;
    }
}
