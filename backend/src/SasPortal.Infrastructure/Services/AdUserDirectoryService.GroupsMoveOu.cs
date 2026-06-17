using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string GroupOuMoveSuccessLoggingFailedMessage =
        "AD group OU move operation succeeded but logging failed.";
    private const string GroupOuMoveFailureLoggingFailedMessage =
        "AD group OU move operation failed but logging failed.";
    private const string GroupOuMoveValidateStep = "ValidateRequest";
    private const string GroupOuMoveLoadGroupStep = "LoadGroup";
    private const string GroupOuMoveValidateTargetOuStep = "ValidateTargetOu";
    private const string GroupOuMoveMoveGroupStep = "MoveGroup";
    private const string GroupOuMoveReloadGroupStep = "ReloadGroup";

    public async Task<MoveAdGroupOuResult> MoveGroupOuAsync(
        MoveAdGroupOuRequest request,
        CancellationToken cancellationToken = default)
    {
        const string auditAction = "MoveOu";

        var targetOuDn = request.TargetOuDistinguishedName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetOuDn))
        {
            return await FailGroupOuMoveAsync(
        request,
                auditAction,
                "AD group OU move failed.",
                AdManagementApiMessageKeys.Groups.TargetOuRequired,
                BuildGroupOuMoveFailureDiagnostic(
                    GroupOuMoveValidateStep,
                    request.GroupId,
                    null,
                    englishMessageOverride: "Target OU distinguished name is required.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                null,
                targetOuDn,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailGroupOuMoveAsync(
        request,
                auditAction,
                "AD group OU move failed.",
                connectionResult.MessageKey,
                BuildGroupOuMoveFailureDiagnostic(
                    GroupOuMoveValidateStep,
                    request.GroupId,
                    null,
                    englishMessageOverride: "The LDAP connection failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                null,
                null,
                targetOuDn,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return await FailGroupOuMoveAsync(
        request,
                auditAction,
                "AD group OU move failed.",
                AdManagementApiMessageKeys.Common.NotConfigured,
                BuildGroupOuMoveFailureDiagnostic(
                    GroupOuMoveValidateTargetOuStep,
                    request.GroupId,
                    null,
                    englishMessageOverride: "AD management is not configured.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                null,
                targetOuDn,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        if (!AdLdapDnHelper.IsEqualOrDescendantOf(targetOuDn, groupsSearchBase))
        {
            return await FailGroupOuMoveAsync(
        request,
                auditAction,
                "AD group OU move failed.",
                AdManagementApiMessageKeys.Groups.InvalidTargetOu,
                BuildGroupOuMoveFailureDiagnostic(
                    GroupOuMoveValidateTargetOuStep,
                    request.GroupId,
                    null,
                    englishMessageOverride:
                        "The target OU must be within the configured groups search base.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                null,
                targetOuDn,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        AdGroupDetail? loadedBeforeDetail = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);

            if (!TryLoadGroupForUpdate(
                    ldapConnection,
                    groupsSearchBase,
                    request.GroupId,
                    out var groupDetail,
                    out _))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Groups.NotFound,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveLoadGroupStep,
                        request.GroupId,
                        null,
                        englishMessageOverride: "The AD security group could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    null,
                    null,
                    targetOuDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            loadedBeforeDetail = groupDetail!;

            var distinguishedName = loadedBeforeDetail.DistinguishedName;
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Groups.NotFound,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveLoadGroupStep,
                        request.GroupId,
                        null,
                        englishMessageOverride: "The AD security group distinguished name is missing.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    loadedBeforeDetail,
                    null,
                    targetOuDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            if (!loadedBeforeDetail.SecurityEnabled)
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Groups.OuMoveFailed,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveValidateStep,
                        request.GroupId,
                        distinguishedName,
                        englishMessageOverride: "Only AD security groups can be moved through this operation.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    loadedBeforeDetail,
                    distinguishedName,
                    targetOuDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            if (!AdLdapDnHelper.IsEqualOrDescendantOf(distinguishedName, groupsSearchBase))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Groups.OuMoveFailed,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveValidateStep,
                        request.GroupId,
                        distinguishedName,
                        englishMessageOverride:
                            "The source group must be within the configured groups search base.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    loadedBeforeDetail,
                    distinguishedName,
                    targetOuDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            var sourceParentOu = AdLdapDnHelper.GetParentDistinguishedName(distinguishedName);
            if (AdLdapDnHelper.AreDistinguishedNamesEqual(sourceParentOu, targetOuDn))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Groups.AlreadyInTargetOu,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveValidateTargetOuStep,
                        request.GroupId,
                        distinguishedName,
                        englishMessageOverride: "The target OU cannot be the same as the current OU.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    loadedBeforeDetail,
                    distinguishedName,
                    targetOuDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            if (!TryLoadOrganizationalUnit(ldapConnection, targetOuDn))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Ldap.NoSuchObject,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveValidateTargetOuStep,
                        request.GroupId,
                        distinguishedName,
                        englishMessageOverride: "The target organizational unit could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    loadedBeforeDetail,
                    distinguishedName,
                    targetOuDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            var commonName = loadedBeforeDetail.Cn ?? loadedBeforeDetail.Name;
            if (HasDuplicateGroupCnInParentOu(ldapConnection, targetOuDn, commonName))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.OperationFailures.PreflightGroupCnDuplicate,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveValidateTargetOuStep,
                        request.GroupId,
                        distinguishedName,
                        englishMessageOverride:
                            "A group with the same CN already exists in the target OU.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    loadedBeforeDetail,
                    distinguishedName,
                    targetOuDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            var currentRdn = AdLdapDnHelper.GetRelativeDistinguishedName(distinguishedName);
            if (string.IsNullOrWhiteSpace(currentRdn))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Groups.OuMoveFailed,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveMoveGroupStep,
                        request.GroupId,
                        distinguishedName,
                        englishMessageOverride:
                            "The group distinguished name is not valid for Active Directory.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax),
                    loadedBeforeDetail,
                    distinguishedName,
                    targetOuDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            var modifyDnRequest = new ModifyDNRequest(
                distinguishedName,
                targetOuDn,
                currentRdn)
            {
                DeleteOldRdn = true,
            };
            ldapConnection.SendRequest(modifyDnRequest);

            if (!TryLoadGroupForUpdate(
                    ldapConnection,
                    groupsSearchBase,
                    request.GroupId,
                    out var afterDetail,
                    out _))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Groups.OuMoveFailed,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveReloadGroupStep,
                        request.GroupId,
                        distinguishedName,
                        englishMessageOverride: "The AD group OU move operation failed after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    loadedBeforeDetail,
                    distinguishedName,
                    targetOuDn,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            var afterParentOu = AdLdapDnHelper.GetParentDistinguishedName(afterDetail!.DistinguishedName);
            if (!AdLdapDnHelper.AreDistinguishedNamesEqual(afterParentOu, targetOuDn))
            {
                return await FailGroupOuMoveAsync(
        request,
                    auditAction,
                    "AD group OU move failed.",
                    AdManagementApiMessageKeys.Groups.OuMoveFailed,
                    BuildGroupOuMoveFailureDiagnostic(
                        GroupOuMoveReloadGroupStep,
                        request.GroupId,
                        afterDetail.DistinguishedName,
                        englishMessageOverride: "The AD group OU move could not be verified after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    loadedBeforeDetail,
                    distinguishedName,
                    targetOuDn,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            return await CompleteGroupOuMoveAsync(
                request,
                auditAction,
                $"AD group moved to OU. Group: {afterDetail.SamAccountName}.",
                AdManagementApiMessageKeys.Groups.OuMoveSuccess,
                connectionResult.Context.Connection,
                loadedBeforeDetail,
                afterDetail,
                sourceParentOu,
                afterParentOu,
                targetOuDn,
                cancellationToken);
        }
        catch (LdapException ex)
        {
            return await FailGroupOuMoveAsync(
        request,
                auditAction,
                "AD group OU move failed.",
                SanitizeGroupOuMoveLdapError(ex),
                AdOperationErrorDiagnosticBuilder.BuildGroupOuMoveFailureJson(
                    GroupOuMoveMoveGroupStep,
                    request.GroupId,
                    loadedBeforeDetail?.DistinguishedName,
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                loadedBeforeDetail,
                loadedBeforeDetail?.DistinguishedName,
                targetOuDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD group OU move unexpected failure. GroupId={GroupId} ActorUserId={ActorUserId}",
                request.GroupId,
                request.ActorUserId);

            return await FailGroupOuMoveAsync(
        request,
                auditAction,
                "AD group OU move failed.",
                AdManagementApiMessageKeys.Groups.OuMoveFailed,
                BuildGroupOuMoveFailureDiagnostic(
                    GroupOuMoveMoveGroupStep,
                    request.GroupId,
                    loadedBeforeDetail?.DistinguishedName,
                    englishMessageOverride: "The AD group OU move operation failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                loadedBeforeDetail,
                loadedBeforeDetail?.DistinguishedName,
                targetOuDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private async Task<MoveAdGroupOuResult> CompleteGroupOuMoveAsync(
        MoveAdGroupOuRequest request,
        string auditAction,
        string auditDescription,
        string messageKey,
        AdManagementConnectionParameters connection,
        AdGroupDetail beforeDetail,
        AdGroupDetail afterDetail,
        string? sourceParentOu,
        string? targetParentOu,
        string targetOuDistinguishedName,
        CancellationToken cancellationToken)
    {
        await WriteGroupOuMoveSuccessLogsSafelyAsync(
            request,
            auditAction,
            auditDescription,
            connection,
            beforeDetail,
            afterDetail,
            sourceParentOu,
            targetParentOu,
            cancellationToken);

        return new MoveAdGroupOuResult(
            true,
            messageKey,
            afterDetail.Id,
            afterDetail.DisplayName,
            afterDetail.Name,
            afterDetail.SamAccountName,
            afterDetail.DistinguishedName,
            beforeDetail.DistinguishedName,
            targetOuDistinguishedName);
    }

    private async Task<MoveAdGroupOuResult> FailGroupOuMoveAsync(
        MoveAdGroupOuRequest request,
        string auditAction,
        string auditDescription,
        string messageKey,
        string errorDiagnosticJson,
        AdGroupDetail? beforeDetail,
        string? targetDistinguishedName,
        string? targetOuDistinguishedName,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteGroupOuMoveFailureLogsSafelyAsync(
        request,
            auditAction,
            auditDescription,
            beforeDetail,
            targetDistinguishedName,
            targetOuDistinguishedName,
            errorDiagnosticJson,
            cancellationToken);

        return new MoveAdGroupOuResult(
            false,
            messageKey,
            beforeDetail?.Id ?? request.GroupId.ToString("D"),
            beforeDetail?.DisplayName,
            beforeDetail?.Name,
            beforeDetail?.SamAccountName,
            beforeDetail?.DistinguishedName,
            beforeDetail?.DistinguishedName,
            targetOuDistinguishedName,
            failureKind,
            messageParams);
    }

    private async Task WriteGroupOuMoveSuccessLogsSafelyAsync(
        MoveAdGroupOuRequest request,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdGroupDetail beforeDetail,
        AdGroupDetail afterDetail,
        string? sourceParentOu,
        string? targetParentOu,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupOuMoveOperationLogsAsync(
                request,
                AdManagementOperationStatuses.Succeeded,
                connection,
                beforeDetail,
                afterDetail,
                sourceParentOu,
                targetParentOu,
                errorDiagnosticJson: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                GroupOuMoveSuccessLoggingFailedMessage,
                afterDetail.Id,
                afterDetail.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildGroupOuMoveAuditRequest(
                    auditAction,
                    afterDetail.Id,
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                GroupOuMoveSuccessLoggingFailedMessage,
                afterDetail.Id,
                afterDetail.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteGroupOuMoveFailureLogsSafelyAsync(
        MoveAdGroupOuRequest request,
        string auditAction,
        string auditDescription,
        AdGroupDetail? beforeDetail,
        string? targetDistinguishedName,
        string? targetOuDistinguishedName,
        string errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupOuMoveOperationLogsAsync(
                request,
                AdManagementOperationStatuses.Failed,
                connection: null,
                beforeDetail,
                afterDetail: beforeDetail,
                beforeDetail is null ? null : AdLdapDnHelper.GetParentDistinguishedName(beforeDetail.DistinguishedName),
                targetOuDistinguishedName,
                errorDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} ActorUserId={ActorUserId}",
                GroupOuMoveFailureLoggingFailedMessage,
                request.GroupId,
                request.ActorUserId);
        }

        if (string.IsNullOrWhiteSpace(auditDescription))
        {
            return;
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildGroupOuMoveAuditRequest(
                    auditAction,
                    beforeDetail?.Id ?? request.GroupId.ToString("D"),
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} ActorUserId={ActorUserId}",
                GroupOuMoveFailureLoggingFailedMessage,
                request.GroupId,
                request.ActorUserId);
        }
    }

    private async Task WriteGroupOuMoveOperationLogsAsync(
        MoveAdGroupOuRequest request,
        string status,
        AdManagementConnectionParameters? connection,
        AdGroupDetail? beforeDetail,
        AdGroupDetail? afterDetail,
        string? sourceParentOu,
        string? targetParentOu,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var targetOuDn = request.TargetOuDistinguishedName.Trim();
        var requestSummary = AdOperationLogSnapshotBuilder.BuildGroupOuMoveRequestSummary(
            request.GroupId,
            beforeDetail?.DistinguishedName,
            targetOuDn);

        string? beforeSnapshot = null;
        if (beforeDetail is not null)
        {
            beforeSnapshot = AdOperationLogSnapshotBuilder.BuildGroupOuMoveBeforeSnapshot(
                beforeDetail,
                sourceParentOu);
        }

        string? afterSnapshot = null;
        if (afterDetail is not null)
        {
            afterSnapshot = AdOperationLogSnapshotBuilder.BuildGroupOuMoveAfterSnapshot(
                afterDetail,
                targetParentOu);
        }

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.GroupMoveOu,
                Status = status,
                TargetObjectType = AdManagementTargetGroupTypes.AdGroup,
                TargetObjectGuid = beforeDetail?.Id ?? request.GroupId.ToString("D"),
                TargetDistinguishedName = beforeDetail?.DistinguishedName,
                TargetSamAccountName = beforeDetail?.SamAccountName,
                RequestSummaryJson = requestSummary,
                BeforeSnapshotJson = beforeSnapshot,
                AfterSnapshotJson = afterSnapshot,
                ErrorCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(errorDiagnosticJson),
                ErrorMessage = errorDiagnosticJson,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = connection is null ? null : ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private static AuditLogWriteRequest BuildGroupOuMoveAuditRequest(
        string auditAction,
        string entityId,
        string auditDescription,
        MoveAdGroupOuRequest request) =>
        new()
        {
            Action = auditAction,
            EntityName = "AdGroup",
            EntityId = entityId,
            Description = auditDescription,
            ActorUserId = request.ActorUserId,
            ActorUserName = request.ActorUserName,
            IpAddress = request.ActorIpAddress,
            UserAgent = request.ActorUserAgent,
        };

    private static string BuildGroupOuMoveFailureDiagnostic(
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        string? normalizedReasonOverride = null) =>
        AdOperationErrorDiagnosticBuilder.BuildGroupOuMoveFailureJson(
            step,
            targetObjectGuid,
            targetDistinguishedName,
            englishMessageOverride: englishMessageOverride,
            normalizedReasonOverride: normalizedReasonOverride);

    private static string SanitizeGroupOuMoveLdapError(LdapException exception)
    {
        var normalized = AdLdapErrorNormalizer.NormalizeMessageKey(exception.ErrorCode, exception.Message);
        return string.Equals(normalized, AdManagementApiMessageKeys.Users.UpdateFailed, StringComparison.Ordinal)
            ? AdManagementApiMessageKeys.Groups.OuMoveFailed
            : normalized;
    }
}
