using System.DirectoryServices.Protocols;
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

            var changePlan = BuildUpdateChangePlan(
                normalizedRequest,
                beforeEntry!,
                distinguishedName,
                activeMappings);

            if (!changePlan.HasChanges)
            {
                await WriteUpdateNoChangesLogsAsync(
                    normalizedRequest,
                    context.Connection,
                    beforeDetail,
                    distinguishedName,
                    cancellationToken);

                return new AdUserDirectoryDetailResult(true, string.Empty, beforeDetail);
            }

            var preflightFailure = RunUpdatePreflightChecks(ldapConnection, searchBase, changePlan);
            if (preflightFailure is not null)
            {
                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    preflightFailure.UserMessage,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdUserUpdateOperationDiagnosticBuilder.BuildPreflightDuplicateJson(
                        preflightFailure.AttributeName,
                        preflightFailure.EnglishDiagnosticMessage,
                        normalizedRequest.UserId),
                    beforeDetail,
                    distinguishedName,
                    beforeDetail,
                    distinguishedName,
                    cancellationToken);
            }

            var appliedChanges = new List<AdUserUpdateAppliedChange>();
            var currentDn = distinguishedName;

            try
            {
                ExecuteUpdateChangePlan(
                    ldapConnection,
                    ref currentDn,
                    changePlan,
                    normalizedRequest,
                    appliedChanges);
            }
            catch (UpdateUserLdapException ex)
            {
                return await HandleUpdateWriteFailureAsync(
                    ldapConnection,
                    searchBase,
                    normalizedRequest,
                    context.Connection,
                    beforeDetail,
                    currentDn,
                    appliedChanges,
                    ex,
                    activeMappings,
                    cancellationToken);
            }

            distinguishedName = currentDn;

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
                        distinguishedName,
                        afterReloadFailed: true),
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
                        TargetObjectGuid: normalizedRequest.UserId,
                        RollbackStatus: AdUserUpdateRollbackStatus.NotRequired)),
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

    private async Task<AdUserDirectoryDetailResult> HandleUpdateWriteFailureAsync(
        LdapConnection ldapConnection,
        string searchBase,
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        AdUserDetail beforeDetail,
        string currentDistinguishedName,
        IReadOnlyList<AdUserUpdateAppliedChange> appliedChanges,
        UpdateUserLdapException exception,
        IReadOnlyList<AdAttributeMappingItem> activeMappings,
        CancellationToken cancellationToken)
    {
        var rollbackDn = currentDistinguishedName;
        var rollbackResult = TryRollbackAppliedChanges(
            ldapConnection,
            ref rollbackDn,
            appliedChanges,
            request);

        var partialUpdate = rollbackResult.Status is AdUserUpdateRollbackStatus.Failed
            or AdUserUpdateRollbackStatus.PartiallySucceeded;

        AdUserDetail? afterDetail = null;
        var afterReloadFailed = false;
        if (TryLoadUserForUpdate(
                ldapConnection,
                searchBase,
                request.UserId,
                activeMappings,
                out var reloadedDetail,
                out _)
            && reloadedDetail is not null)
        {
            afterDetail = reloadedDetail;
        }
        else
        {
            afterReloadFailed = true;
            afterDetail = partialUpdate ? null : beforeDetail;
        }

        var appliedChangeNames = GetAppliedChangeLogNames(appliedChanges);
        var diagnosticJson = AdUserUpdateOperationDiagnosticBuilder.BuildWithRollback(
            exception.FailureContext with
            {
                TargetObjectGuid = request.UserId,
                TargetDistinguishedName = currentDistinguishedName,
                AfterReloadFailed = afterReloadFailed ? true : null,
            },
            rollbackResult,
            appliedChangeNames);

        return await FailUpdateAsync(
            request,
            connection,
            exception.UserMessage,
            exception.FailureKind,
            diagnosticJson,
            beforeDetail,
            rollbackDn,
            afterDetail,
            afterDetail?.DistinguishedName ?? rollbackDn,
            cancellationToken);
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
                ErrorCode = string.Equals(status, AdManagementOperationStatuses.Failed, StringComparison.Ordinal)
                    ? AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(errorMessage)
                    : null,
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
        System.Text.Json.JsonSerializer.Serialize(AdUserUpdateSnapshotBuilder.Build(
            detail.GivenName,
            detail.Surname,
            detail.DisplayName,
            detail.SamAccountName,
            detail.UserPrincipalName,
            detail.Mail,
            detail.Department,
            detail.DistinguishedName,
            detail.MappedAttributes));

    private static UpdateUserLdapException CreateUpdateUserLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind,
        AdUserUpdateFailureContext failureContext) =>
        new(userMessage, failureKind, failureContext);

    private sealed class UpdateUserLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind,
        AdUserUpdateFailureContext failureContext)
        : Exception(userMessage)
    {
        public string UserMessage { get; } = userMessage;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
        public AdUserUpdateFailureContext FailureContext { get; } = failureContext;
    }
}
