using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string UpdateGroupSuccessLoggingFailedMessage =
        "AD group update operation succeeded but logging failed.";
    private const string UpdateGroupFailureLoggingFailedMessage =
        "AD group update operation failed but logging failed.";

    public async Task<AdGroupDirectoryDetailResult> UpdateGroupAsync(
        UpdateAdGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeUpdateGroupRequest(request);

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdGroupDirectoryDetailResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind);
        }

        if (!AdUpdateGroupRequestValidator.TryValidate(normalizedRequest, out var validationMessage))
        {
            return new AdGroupDirectoryDetailResult(
                false,
                validationMessage,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdGroupDirectoryDetailResult(
                false,
                AdManagementNotConfiguredMessage,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        var context = connectionResult.Context;
        var domainSearchBase = context.Connection.DefaultNamingContext ?? context.Connection.BaseDn;

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            if (!TryLoadGroupForUpdate(
                    ldapConnection,
                    groupsSearchBase,
                    normalizedRequest.GroupId,
                    out var beforeDetail,
                    out var beforeEntry))
            {
                return await FailGroupUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    AdLdapErrorNormalizer.GroupNotFoundMessage,
                    AdDirectoryFailureKind.NotFound,
                    AdGroupUpdateOperationDiagnosticBuilder.BuildNotFoundJson(
                        AdGroupUpdateSteps.LoadGroup,
                        normalizedRequest.GroupId),
                    beforeDetail,
                    null,
                    null,
                    null,
                    cancellationToken);
            }

            var distinguishedName = beforeDetail!.DistinguishedName;
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                return await FailGroupUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    AdLdapErrorNormalizer.GroupNotFoundMessage,
                    AdDirectoryFailureKind.NotFound,
                    AdGroupUpdateOperationDiagnosticBuilder.BuildNotFoundJson(
                        AdGroupUpdateSteps.LoadGroup,
                        normalizedRequest.GroupId),
                    beforeDetail,
                    distinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            var changePlan = BuildGroupUpdateChangePlan(
                normalizedRequest,
                beforeEntry!,
                distinguishedName);

            if (!changePlan.HasChanges)
            {
                await WriteGroupUpdateNoChangesLogsAsync(
                    normalizedRequest,
                    context.Connection,
                    beforeDetail,
                    distinguishedName,
                    cancellationToken);

                return new AdGroupDirectoryDetailResult(true, string.Empty, beforeDetail);
            }

            var preflightFailure = RunGroupUpdatePreflightChecks(
                ldapConnection,
                domainSearchBase ?? groupsSearchBase,
                changePlan);
            if (preflightFailure is not null)
            {
                return await FailGroupUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    preflightFailure.UserMessage,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdGroupUpdateOperationDiagnosticBuilder.BuildPreflightDuplicateJson(
                        preflightFailure.AttributeName,
                        preflightFailure.EnglishDiagnosticMessage,
                        normalizedRequest.GroupId),
                    beforeDetail,
                    distinguishedName,
                    beforeDetail,
                    distinguishedName,
                    cancellationToken);
            }

            var appliedChanges = new List<AdGroupUpdateAppliedChange>();
            var currentDn = distinguishedName;

            try
            {
                ExecuteGroupUpdateChangePlan(
                    ldapConnection,
                    ref currentDn,
                    changePlan,
                    normalizedRequest,
                    appliedChanges);
            }
            catch (UpdateGroupLdapException ex)
            {
                return await HandleGroupUpdateWriteFailureAsync(
                    ldapConnection,
                    groupsSearchBase,
                    normalizedRequest,
                    context.Connection,
                    beforeDetail,
                    currentDn,
                    appliedChanges,
                    ex,
                    cancellationToken);
            }

            distinguishedName = currentDn;

            if (!TryLoadGroupForUpdate(
                    ldapConnection,
                    groupsSearchBase,
                    normalizedRequest.GroupId,
                    out var afterDetail,
                    out _)
                || afterDetail is null)
            {
                return await FailGroupUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    AdLdapErrorNormalizer.UpdateGroupFailedMessage,
                    AdDirectoryFailureKind.ConnectionFailed,
                    AdGroupUpdateOperationDiagnosticBuilder.BuildGenericFailureJson(
                        AdGroupUpdateSteps.ReloadGroup,
                        AdUserUpdateNormalizedReasons.ConnectionFailed,
                        "The AD security group could not be reloaded after update.",
                        normalizedRequest.GroupId,
                        distinguishedName,
                        afterReloadFailed: true),
                    beforeDetail,
                    distinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            await WriteGroupUpdateSuccessLogsAsync(
                normalizedRequest,
                context.Connection,
                beforeDetail,
                afterDetail,
                distinguishedName,
                cancellationToken);

            return new AdGroupDirectoryDetailResult(true, string.Empty, afterDetail);
        }
        catch (LdapException ex)
        {
            return await FailGroupUpdateAsync(
                normalizedRequest,
                context.Connection,
                AdLdapErrorNormalizer.Normalize(ex.ErrorCode, ex.Message),
                AdDirectoryFailureKind.ConnectionFailed,
                AdGroupUpdateOperationDiagnosticBuilder.BuildGenericFailureJson(
                    AdGroupUpdateSteps.UpdateGroup,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The AD security group update failed.",
                    normalizedRequest.GroupId,
                    null),
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
                "AD group update unexpected failure. ActorUserId={ActorUserId}",
                normalizedRequest.ActorUserId);

            return await FailGroupUpdateAsync(
                normalizedRequest,
                context.Connection,
                AdLdapErrorNormalizer.UpdateGroupFailedMessage,
                AdDirectoryFailureKind.ConnectionFailed,
                AdGroupUpdateOperationDiagnosticBuilder.BuildGenericFailureJson(
                    AdGroupUpdateSteps.UpdateGroup,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The AD security group update failed.",
                    normalizedRequest.GroupId,
                    null),
                null,
                null,
                null,
                null,
                cancellationToken);
        }
    }

    private static UpdateAdGroupRequest NormalizeUpdateGroupRequest(UpdateAdGroupRequest request) =>
        request with
        {
            DisplayName = request.DisplayName.Trim(),
            Name = AdGroupNameNormalizer.NormalizeTechnicalName(request.Name),
            SamAccountName = request.SamAccountName.Trim(),
            Description = request.Description is null
                ? null
                : (string.IsNullOrWhiteSpace(request.Description) ? string.Empty : request.Description.Trim()),
        };

    private AdGroupUpdateChangePlan BuildGroupUpdateChangePlan(
        UpdateAdGroupRequest request,
        SearchResultEntry entry,
        string distinguishedName)
    {
        var currentScalars = BuildGroupCurrentScalarValues(entry);
        return AdGroupUpdateChangePlanBuilder.Build(request, currentScalars, distinguishedName);
    }

    private static IReadOnlyDictionary<string, string?> BuildGroupCurrentScalarValues(SearchResultEntry entry) =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["displayName"] = GetFirstString(entry, "displayName"),
            ["sAMAccountName"] = GetFirstString(entry, "sAMAccountName"),
            ["description"] = GetFirstString(entry, "description"),
        };

    private static bool TryLoadGroupForUpdate(
        LdapConnection ldapConnection,
        string groupsSearchBase,
        Guid objectGuid,
        out AdGroupDetail? detail,
        out SearchResultEntry? entry)
    {
        detail = null;
        entry = null;
        var filter = AdLdapGroupFilterHelper.BuildSecurityGroupObjectGuidFilter(objectGuid);
        var searchRequest = new SearchRequest(
            groupsSearchBase,
            filter,
            SearchScope.Subtree,
            GroupDetailAttributes)
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
        return TryMapGroupDetail(ldapConnection, entry, out detail);
    }

    private async Task<AdGroupDirectoryDetailResult> HandleGroupUpdateWriteFailureAsync(
        LdapConnection ldapConnection,
        string groupsSearchBase,
        UpdateAdGroupRequest request,
        AdManagementConnectionParameters connection,
        AdGroupDetail beforeDetail,
        string currentDistinguishedName,
        IReadOnlyList<AdGroupUpdateAppliedChange> appliedChanges,
        UpdateGroupLdapException exception,
        CancellationToken cancellationToken)
    {
        var rollbackDn = currentDistinguishedName;
        var rollbackResult = TryRollbackGroupAppliedChanges(
            ldapConnection,
            ref rollbackDn,
            appliedChanges,
            request);

        var partialUpdate = rollbackResult.Status is AdUserUpdateRollbackStatus.Failed
            or AdUserUpdateRollbackStatus.PartiallySucceeded;

        AdGroupDetail? afterDetail = null;
        var afterReloadFailed = false;
        if (TryLoadGroupForUpdate(
                ldapConnection,
                groupsSearchBase,
                request.GroupId,
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

        var appliedChangeNames = appliedChanges
            .Select(static change => change.LogAttributeName)
            .ToList();

        var diagnosticJson = AdGroupUpdateOperationDiagnosticBuilder.BuildJson(
            new AdGroupUpdateFailureContext(
                exception.FailureContext.Step,
                AttributeName: exception.FailureContext.AttributeName,
                LdapResultCode: exception.FailureContext.LdapResultCode,
                LdapExceptionErrorCode: exception.FailureContext.LdapExceptionErrorCode,
                LdapDiagnosticMessage: exception.FailureContext.LdapDiagnosticMessage,
                TargetObjectGuid: request.GroupId,
                TargetDistinguishedName: currentDistinguishedName,
                DiagnosticCode: AdGroupUpdateDiagnosticCodes.UpdateFailed,
                NormalizedReasonOverride: exception.FailureContext.NormalizedReasonOverride,
                EnglishMessageOverride: exception.UserMessage,
                PartialUpdate: partialUpdate,
                RollbackStatus: rollbackResult.Status,
                AppliedChanges: appliedChangeNames,
                RolledBackChanges: rollbackResult.RolledBackChanges,
                RollbackErrors: rollbackResult.Errors,
                AfterReloadFailed: afterReloadFailed ? true : null));

        return await FailGroupUpdateAsync(
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

    private async Task<AdGroupDirectoryDetailResult> FailGroupUpdateAsync(
        UpdateAdGroupRequest request,
        AdManagementConnectionParameters connection,
        string message,
        AdDirectoryFailureKind failureKind,
        string operationDiagnosticJson,
        AdGroupDetail? beforeDetail,
        string? targetDistinguishedName,
        AdGroupDetail? afterDetail,
        string? afterDistinguishedName,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupUpdateFailureLogsAsync(
                request,
                connection,
                beforeDetail,
                targetDistinguishedName,
                afterDetail,
                afterDistinguishedName,
                operationDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                UpdateGroupFailureLoggingFailedMessage,
                request.GroupId,
                request.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Update",
                    EntityName = "AdGroup",
                    EntityId = request.GroupId.ToString("D"),
                    Description = "AD security group update failed.",
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                UpdateGroupFailureLoggingFailedMessage,
                request.GroupId,
                request.SamAccountName,
                request.ActorUserId);
        }

        return new AdGroupDirectoryDetailResult(false, message, null, failureKind);
    }

    private async Task WriteGroupUpdateNoChangesLogsAsync(
        UpdateAdGroupRequest request,
        AdManagementConnectionParameters connection,
        AdGroupDetail beforeDetail,
        string targetDistinguishedName,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupUpdateOperationLogAsync(
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
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                UpdateGroupSuccessLoggingFailedMessage,
                beforeDetail.Id,
                beforeDetail.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Update",
                    EntityName = "AdGroup",
                    EntityId = beforeDetail.Id,
                    Description = "AD security group update skipped: no changes detected.",
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} ActorUserId={ActorUserId}",
                UpdateGroupSuccessLoggingFailedMessage,
                beforeDetail.Id,
                request.ActorUserId);
        }
    }

    private async Task WriteGroupUpdateSuccessLogsAsync(
        UpdateAdGroupRequest request,
        AdManagementConnectionParameters connection,
        AdGroupDetail beforeDetail,
        AdGroupDetail afterDetail,
        string targetDistinguishedName,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupUpdateOperationLogAsync(
                request,
                connection,
                AdManagementOperationStatuses.Succeeded,
                beforeDetail,
                targetDistinguishedName,
                afterDetail,
                afterDetail.DistinguishedName,
                errorMessage: null,
                requestSummaryJson: AdGroupUpdateSnapshotBuilder.BuildRequestSummary(request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                UpdateGroupSuccessLoggingFailedMessage,
                afterDetail.Id,
                afterDetail.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Update",
                    EntityName = "AdGroup",
                    EntityId = afterDetail.Id,
                    Description = "AD security group updated.",
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                UpdateGroupSuccessLoggingFailedMessage,
                afterDetail.Id,
                afterDetail.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteGroupUpdateFailureLogsAsync(
        UpdateAdGroupRequest request,
        AdManagementConnectionParameters connection,
        AdGroupDetail? beforeDetail,
        string? targetDistinguishedName,
        AdGroupDetail? afterDetail,
        string? afterDistinguishedName,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await WriteGroupUpdateOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Failed,
            beforeDetail,
            targetDistinguishedName,
            afterDetail,
            afterDistinguishedName,
            errorMessage: operationDiagnosticJson,
            requestSummaryJson: AdGroupUpdateSnapshotBuilder.BuildRequestSummary(request),
            cancellationToken);
    }

    private async Task WriteGroupUpdateOperationLogAsync(
        UpdateAdGroupRequest request,
        AdManagementConnectionParameters connection,
        string status,
        AdGroupDetail? beforeDetail,
        string? targetDistinguishedName,
        AdGroupDetail? afterDetail,
        string? afterDistinguishedName,
        string? errorMessage,
        string requestSummaryJson,
        CancellationToken cancellationToken = default)
    {
        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.GroupUpdate,
                Status = status,
                TargetObjectType = AdManagementTargetGroupTypes.AdGroup,
                TargetDistinguishedName = afterDistinguishedName ?? targetDistinguishedName,
                TargetObjectGuid = afterDetail?.Id ?? beforeDetail?.Id,
                TargetSamAccountName = afterDetail?.SamAccountName ?? beforeDetail?.SamAccountName ?? request.SamAccountName,
                RequestSummaryJson = requestSummaryJson,
                BeforeSnapshotJson = AdGroupUpdateSnapshotBuilder.Build(beforeDetail),
                AfterSnapshotJson = AdGroupUpdateSnapshotBuilder.Build(afterDetail),
                ErrorCode = string.IsNullOrWhiteSpace(errorMessage)
                    ? null
                    : AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(errorMessage),
                ErrorMessage = errorMessage,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private sealed class UpdateGroupLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind,
        AdGroupUpdateFailureContext failureContext) : Exception(userMessage)
    {
        public string UserMessage { get; } = userMessage;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
        public AdGroupUpdateFailureContext FailureContext { get; } = failureContext;
    }
}
