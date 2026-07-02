using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUsersDirectoryService : IAdUserOuMoveService
{
    private const string OuMoveSuccessLoggingFailedMessage =
        "AD user OU move operation succeeded but logging failed.";
    private const string OuMoveFailureLoggingFailedMessage =
        "AD user OU move operation failed but logging failed.";
    private const string OuMoveValidateStep = "ValidateRequest";
    private const string OuMoveLoadUserStep = "LoadUser";
    private const string OuMoveValidateTargetOuStep = "ValidateTargetOu";
    private const string OuMoveMoveUserStep = "MoveUser";
    private const string OuMoveReloadUserStep = "ReloadUser";

    public Task<MoveAdUserOuResult> MoveOuAsync(
        MoveAdUserOuRequest request,
        CancellationToken cancellationToken = default) =>
        MoveUserOuAsync(request, cancellationToken);

    private async Task<MoveAdUserOuResult> MoveUserOuAsync(
        MoveAdUserOuRequest request,
        CancellationToken cancellationToken)
    {
        const string auditAction = "MoveOu";

        var targetOuDn = request.TargetOuDistinguishedName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetOuDn))
        {
            return await FailOuMoveAsync(
        request,
                auditAction,
                "AD user OU move failed.",
                AdManagementApiMessageKeys.Users.TargetOuRequired,
                BuildOuMoveFailureDiagnostic(
                    OuMoveValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "Target OU distinguished name is required.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailOuMoveAsync(
        request,
                auditAction,
                "AD user OU move failed.",
                connectionResult.MessageKey,
                BuildOuMoveFailureDiagnostic(
                    OuMoveValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "The LDAP connection failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                null,
                targetOuDn,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var connection = connectionResult.Context.Connection;
        var usersRootOu = connection.UsersRootOu;
        if (string.IsNullOrWhiteSpace(usersRootOu))
        {
            return await FailOuMoveAsync(
        request,
                auditAction,
                "AD user OU move failed.",
                AdManagementApiMessageKeys.Common.NotConfigured,
                BuildOuMoveFailureDiagnostic(
                    OuMoveValidateTargetOuStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "AD management is not configured.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                targetOuDn,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        if (!AdLdapDnHelper.IsEqualOrDescendantOf(targetOuDn, usersRootOu))
        {
            return await FailOuMoveAsync(
        request,
                auditAction,
                "AD user OU move failed.",
                AdManagementApiMessageKeys.Users.InvalidTargetOu,
                BuildOuMoveFailureDiagnostic(
                    OuMoveValidateTargetOuStep,
                    request.UserId,
                    null,
                    englishMessageOverride:
                        "The target OU must be within the configured users root OU.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                targetOuDn,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        var searchBase = ResolveDetailSearchBase(connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return await FailOuMoveAsync(
        request,
                auditAction,
                "AD user OU move failed.",
                AdManagementApiMessageKeys.Common.NotConfigured,
                BuildOuMoveFailureDiagnostic(
                    OuMoveValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "AD management is not configured.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                targetOuDn,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        AdUserOuMoveContext? loadedBeforeContext = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadUserOuMoveContext(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var beforeContext))
            {
                return await FailOuMoveAsync(
        request,
                    auditAction,
                    "AD user OU move failed.",
                    AdManagementApiMessageKeys.Users.NotFound,
                    BuildOuMoveFailureDiagnostic(
                        OuMoveLoadUserStep,
                        request.UserId,
                        null,
                        englishMessageOverride: "The AD user could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    null,
                    targetOuDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            loadedBeforeContext = beforeContext;

            if (!TryLoadOrganizationalUnit(ldapConnection, targetOuDn))
            {
                return await FailOuMoveAsync(
        request,
                    auditAction,
                    "AD user OU move failed.",
                    AdManagementApiMessageKeys.Ldap.NoSuchObject,
                    BuildOuMoveFailureDiagnostic(
                        OuMoveValidateTargetOuStep,
                        request.UserId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "The target organizational unit could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    beforeContext,
                    targetOuDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            if (AdLdapDnHelper.AreDistinguishedNamesEqual(beforeContext.ParentOuDistinguishedName, targetOuDn))
            {
                return await CompleteOuMoveAsync(
                    request,
                    auditAction,
                    $"AD user OU move skipped (no changes): {beforeContext.SamAccountName}.",
                    AdManagementApiMessageKeys.Users.AlreadyInTargetOu,
                    connection,
                    beforeContext,
                    beforeContext with { ParentOuDistinguishedName = targetOuDn },
                    targetOuDn,
                    cancellationToken);
            }

            var currentRdn = AdLdapDnHelper.GetRelativeDistinguishedName(beforeContext.DistinguishedName);
            if (string.IsNullOrWhiteSpace(currentRdn))
            {
                return await FailOuMoveAsync(
        request,
                    auditAction,
                    "AD user OU move failed.",
                    AdManagementApiMessageKeys.Users.OuMoveFailed,
                    BuildOuMoveFailureDiagnostic(
                        OuMoveMoveUserStep,
                        request.UserId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "The user distinguished name is not valid for Active Directory.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax),
                    beforeContext,
                    targetOuDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            var modifyDnRequest = new ModifyDNRequest(
                beforeContext.DistinguishedName,
                targetOuDn,
                currentRdn);
            ldapConnection.SendRequest(modifyDnRequest);

            if (!TryLoadUserOuMoveContext(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var afterContext))
            {
                return await FailOuMoveAsync(
        request,
                    auditAction,
                    "AD user OU move failed.",
                    AdManagementApiMessageKeys.Users.OuMoveFailed,
                    BuildOuMoveFailureDiagnostic(
                        OuMoveReloadUserStep,
                        request.UserId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "The AD user OU move operation failed after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    beforeContext,
                    targetOuDn,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            if (!AdLdapDnHelper.AreDistinguishedNamesEqual(afterContext.ParentOuDistinguishedName, targetOuDn))
            {
                return await FailOuMoveAsync(
        request,
                    auditAction,
                    "AD user OU move failed.",
                    AdManagementApiMessageKeys.Users.OuMoveFailed,
                    BuildOuMoveFailureDiagnostic(
                        OuMoveReloadUserStep,
                        request.UserId,
                        afterContext.DistinguishedName,
                        englishMessageOverride: "The AD user OU move could not be verified after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    beforeContext,
                    targetOuDn,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            return await CompleteOuMoveAsync(
                request,
                auditAction,
                $"AD user moved to OU. User: {afterContext.SamAccountName}.",
                AdManagementApiMessageKeys.Users.OuMoveSuccess,
                connection,
                beforeContext,
                afterContext,
                targetOuDn,
                cancellationToken);
        }
        catch (LdapException ex)
        {
            return await FailOuMoveAsync(
        request,
                auditAction,
                "AD user OU move failed.",
                SanitizeOuMoveLdapError(ex),
                AdOperationErrorDiagnosticBuilder.BuildUserOuMoveFailureJson(
                    OuMoveMoveUserStep,
                    request.UserId,
                    loadedBeforeContext?.DistinguishedName,
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                loadedBeforeContext,
                targetOuDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return await FailOuMoveAsync(
        request,
                auditAction,
                "AD user OU move failed.",
                AdManagementApiMessageKeys.Users.OuMoveFailed,
                BuildOuMoveFailureDiagnostic(
                    OuMoveMoveUserStep,
                    request.UserId,
                    loadedBeforeContext?.DistinguishedName,
                    englishMessageOverride: "The AD user OU move operation failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                loadedBeforeContext,
                targetOuDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private async Task<MoveAdUserOuResult> CompleteOuMoveAsync(
        MoveAdUserOuRequest request,
        string auditAction,
        string auditDescription,
        string messageKey,
        AdManagementConnectionParameters connection,
        AdUserOuMoveContext beforeContext,
        AdUserOuMoveContext afterContext,
        string targetOuDistinguishedName,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteOuMoveSuccessLogsSafelyAsync(
            request,
            auditAction,
            auditDescription,
            connection,
            beforeContext,
            afterContext,
            cancellationToken);

        return new MoveAdUserOuResult(
            true,
            messageKey,
            afterContext.UserId,
            afterContext.SamAccountName,
            afterContext.UserPrincipalName,
            afterContext.DistinguishedName,
            beforeContext.DistinguishedName,
            targetOuDistinguishedName,
            null,
            messageParams);
    }

    private async Task<MoveAdUserOuResult> FailOuMoveAsync(
        MoveAdUserOuRequest request,
        string auditAction,
        string auditDescription,
        string messageKey,
        string errorDiagnosticJson,
        AdUserOuMoveContext? beforeContext,
        string? targetOuDistinguishedName,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteOuMoveFailureLogsSafelyAsync(
        request,
            auditAction,
            auditDescription,
            beforeContext,
            targetOuDistinguishedName,
            errorDiagnosticJson,
            cancellationToken);

        return new MoveAdUserOuResult(
            false,
            messageKey,
            request.UserId.ToString("D"),
            beforeContext?.SamAccountName,
            beforeContext?.UserPrincipalName,
            beforeContext?.DistinguishedName,
            beforeContext?.DistinguishedName,
            targetOuDistinguishedName,
            failureKind,
            messageParams);
    }

    private async Task WriteOuMoveSuccessLogsSafelyAsync(
        MoveAdUserOuRequest request,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdUserOuMoveContext beforeContext,
        AdUserOuMoveContext afterContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteOuMoveOperationLogsAsync(
                request,
                AdManagementOperationStatuses.Succeeded,
                connection,
                beforeContext,
                afterContext,
                errorDiagnosticJson: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} UserId={UserId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                OuMoveSuccessLoggingFailedMessage,
                afterContext.UserId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildOuMoveAuditRequest(
                    auditAction,
                    afterContext.UserId,
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} UserId={UserId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                OuMoveSuccessLoggingFailedMessage,
                afterContext.UserId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteOuMoveFailureLogsSafelyAsync(
        MoveAdUserOuRequest request,
        string auditAction,
        string auditDescription,
        AdUserOuMoveContext? beforeContext,
        string? targetOuDistinguishedName,
        string errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteOuMoveOperationLogsAsync(
                request,
                AdManagementOperationStatuses.Failed,
                connection: null,
                beforeContext,
                afterContext: beforeContext,
                errorDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} UserId={UserId} ActorUserId={ActorUserId}",
                OuMoveFailureLoggingFailedMessage,
                request.UserId,
                request.ActorUserId);
        }

        if (string.IsNullOrWhiteSpace(auditDescription))
        {
            return;
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildOuMoveAuditRequest(
                    auditAction,
                    beforeContext?.UserId ?? request.UserId.ToString("D"),
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} UserId={UserId} ActorUserId={ActorUserId}",
                OuMoveFailureLoggingFailedMessage,
                request.UserId,
                request.ActorUserId);
        }
    }

    private async Task WriteOuMoveOperationLogsAsync(
        MoveAdUserOuRequest request,
        string status,
        AdManagementConnectionParameters? connection,
        AdUserOuMoveContext? beforeContext,
        AdUserOuMoveContext? afterContext,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var targetOuDn = request.TargetOuDistinguishedName.Trim();
        var requestSummary = AdOperationLogSnapshotBuilder.BuildUserOuMoveRequestSummary(
            request.UserId,
            targetOuDn);

        string? beforeSnapshot = null;
        if (beforeContext is not null)
        {
            beforeSnapshot = AdOperationLogSnapshotBuilder.BuildUserOuMoveBeforeSnapshot(
                beforeContext.UserId,
                beforeContext.SamAccountName,
                beforeContext.UserPrincipalName,
                beforeContext.DistinguishedName,
                beforeContext.ParentOuDistinguishedName);
        }

        string? afterSnapshot = null;
        if (afterContext is not null)
        {
            afterSnapshot = AdOperationLogSnapshotBuilder.BuildUserOuMoveAfterSnapshot(
                afterContext.UserId,
                afterContext.SamAccountName,
                afterContext.UserPrincipalName,
                afterContext.DistinguishedName,
                afterContext.ParentOuDistinguishedName);
        }

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.UserOuMove,
                Status = status,
                TargetObjectType = AdManagementTargetUserTypes.AdUser,
                TargetObjectGuid = beforeContext?.UserId ?? request.UserId.ToString("D"),
                TargetDistinguishedName = beforeContext?.DistinguishedName,
                TargetSamAccountName = beforeContext?.SamAccountName,
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

    private static AuditLogWriteRequest BuildOuMoveAuditRequest(
        string auditAction,
        string entityId,
        string auditDescription,
        MoveAdUserOuRequest request) =>
        new()
        {
            Action = auditAction,
            EntityName = "AdUser",
            EntityId = entityId,
            Description = auditDescription,
            ActorUserId = request.ActorUserId,
            ActorUserName = request.ActorUserName,
            IpAddress = request.ActorIpAddress,
            UserAgent = request.ActorUserAgent,
        };

    private static string BuildOuMoveFailureDiagnostic(
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        string? normalizedReasonOverride = null) =>
        AdOperationErrorDiagnosticBuilder.BuildUserOuMoveFailureJson(
            step,
            targetObjectGuid,
            targetDistinguishedName,
            englishMessageOverride: englishMessageOverride,
            normalizedReasonOverride: normalizedReasonOverride);

    private static bool TryLoadUserOuMoveContext(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        out AdUserOuMoveContext context)
    {
        context = null!;
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid);
        var filter =
            $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))(objectGUID={guidFilter}))";

        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            "distinguishedName",
            "sAMAccountName",
            "userPrincipalName",
            "objectGUID")
        {
            SizeLimit = 2,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        var entry = response.Entries[0];
        if (!TryGetObjectGuid(entry, out var resolvedGuid))
        {
            return false;
        }

        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        context = new AdUserOuMoveContext(
            resolvedGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            AdLdapDnHelper.GetParentDistinguishedName(distinguishedName));

        return true;
    }

    private static string SanitizeOuMoveLdapError(LdapException exception)
    {
        var normalized = AdLdapErrorNormalizer.NormalizeMessageKey(exception.ErrorCode, exception.Message);
        return string.Equals(normalized, AdManagementApiMessageKeys.Users.UpdateFailed, StringComparison.Ordinal)
            ? AdManagementApiMessageKeys.Users.OuMoveFailed
            : normalized;
    }

    private sealed record AdUserOuMoveContext(
        string UserId,
        string DistinguishedName,
        string? SamAccountName,
        string? UserPrincipalName,
        string? ParentOuDistinguishedName);
}
