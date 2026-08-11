using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUsersDirectoryService : IAdUserAccountExpirationUpdateService
{
    private const string AccountExpirationSuccessLoggingFailedMessage =
        "AD user account expiration update succeeded but logging failed.";
    private const string AccountExpirationFailureLoggingFailedMessage =
        "AD user account expiration update failed but logging failed.";
    private const string AccountExpirationValidateStep = "ValidateRequest";
    private const string AccountExpirationLoadUserStep = "LoadUser";
    private const string AccountExpirationModifyStep = "ModifyAccountExpires";
    private const string AccountExpirationReloadUserStep = "ReloadUser";

    public Task<UpdateAdUserAccountExpirationResult> UpdateAccountExpirationAsync(
        UpdateAdUserAccountExpirationRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateUserAccountExpirationAsync(request, cancellationToken);

    private async Task<UpdateAdUserAccountExpirationResult> UpdateUserAccountExpirationAsync(
        UpdateAdUserAccountExpirationRequest request,
        CancellationToken cancellationToken)
    {
        const string auditAction = "Update";

        if (!request.NeverExpires && string.IsNullOrWhiteSpace(request.ExpiresAt))
        {
            return await FailAccountExpirationUpdateAsync(
        request,
                auditAction,
                "AD user account expiration update failed.",
                AdManagementApiMessageKeys.Users.AccountExpirationInvalidDate,
                BuildAccountExpirationFailureDiagnostic(
                    AccountExpirationValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "Account expiration date is required when neverExpires is false.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                AdDirectoryFailureKind.InvalidRequest,
                                cancellationToken,
                AdManagementApiMessageKeys.Users.AccountExpirationInvalidDate);
        }

        DateOnly parsedSelectedDate = default;
        if (!request.NeverExpires)
        {
            if (!AdLdapValueConverter.TryParseAccountExpirationDate(
                    request.ExpiresAt,
                    out parsedSelectedDate,
                    out var parseError))
            {
                return await FailAccountExpirationUpdateAsync(
        request,
                    auditAction,
                    "AD user account expiration update failed.",
                    AdManagementApiMessageKeys.Users.AccountExpirationInvalidDate,
                    BuildAccountExpirationFailureDiagnostic(
                        AccountExpirationValidateStep,
                        request.UserId,
                        null,
                        englishMessageOverride: parseError,
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    null,
                    AdDirectoryFailureKind.InvalidRequest,
                                        cancellationToken,
                AdManagementApiMessageKeys.Users.AccountExpirationInvalidDate);
            }
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailAccountExpirationUpdateAsync(
        request,
                auditAction,
                "AD user account expiration update failed.",
                connectionResult.MessageKey,
                BuildAccountExpirationFailureDiagnostic(
                    AccountExpirationValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "The LDAP connection failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                null,
                connectionResult.FailureKind,
                cancellationToken,
                connectionResult.MessageKey,
                connectionResult.MessageParams);
        }

        var connection = connectionResult.Context.Connection;
        if (AdLdapUserSearchBases.ResolveDistinctSearchBases(connection).Count == 0)
        {
            return await FailAccountExpirationUpdateAsync(
        request,
                auditAction,
                "AD user account expiration update failed.",
                AdManagementApiMessageKeys.Common.NotConfigured,
                BuildAccountExpirationFailureDiagnostic(
                    AccountExpirationValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "AD management is not configured.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                AdDirectoryFailureKind.NotConfigured,
                                cancellationToken,
                AdManagementApiMessageKeys.Common.NotConfigured);
        }

        AdUserAccountExpirationContext? loadedBeforeContext = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context, cancellationToken);
            if (!TryLoadUserAccountExpirationContext(
                    ldapConnection,
                    connection,
                    request.UserId,
                    out var beforeContext))
            {
                return await FailAccountExpirationUpdateAsync(
        request,
                    auditAction,
                    "AD user account expiration update failed.",
                    AdManagementApiMessageKeys.Users.NotFound,
                    BuildAccountExpirationFailureDiagnostic(
                        AccountExpirationLoadUserStep,
                        request.UserId,
                        null,
                        englishMessageOverride: "The AD user could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    null,
                    AdDirectoryFailureKind.NotFound,
                                        cancellationToken,
                AdManagementApiMessageKeys.Users.NotFound);
            }

            loadedBeforeContext = beforeContext;

            var targetNeverExpires = request.NeverExpires;
            var targetExpiresDate = targetNeverExpires
                ? null
                : parsedSelectedDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (beforeContext.NeverExpires == targetNeverExpires
                && string.Equals(
                    beforeContext.AccountExpiresDate,
                    targetExpiresDate,
                    StringComparison.Ordinal))
            {
                return await CompleteAccountExpirationUpdateAsync(
                    request,
                    auditAction,
                    $"AD user account expiration update skipped (no changes): {beforeContext.SamAccountName}.",
                    string.Empty,
                    connection,
                    beforeContext,
                    beforeContext,
                    cancellationToken);
            }

            var fileTime = targetNeverExpires
                ? AdLdapValueConverter.ToNeverExpiresFileTime()
                : AdAccountExpirationDateConverter.ToAccountExpiresFileTime(parsedSelectedDate);

            ApplyAccountExpires(ldapConnection, beforeContext.DistinguishedName, fileTime);

            if (!TryLoadUserAccountExpirationContext(
                    ldapConnection,
                    connection,
                    request.UserId,
                    out var afterContext))
            {
                return await FailAccountExpirationUpdateAsync(
        request,
                    auditAction,
                    "AD user account expiration update failed.",
                    AdManagementApiMessageKeys.Users.AccountExpirationUpdateFailed,
                    BuildAccountExpirationFailureDiagnostic(
                        AccountExpirationReloadUserStep,
                        request.UserId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride:
                            "The AD user could not be reloaded after account expiration update.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                    beforeContext,
                    AdDirectoryFailureKind.ConnectionFailed,
                                        cancellationToken,
                AdManagementApiMessageKeys.Users.AccountExpirationUpdateFailed);
            }

            return await CompleteAccountExpirationUpdateAsync(
                request,
                auditAction,
                $"AD user account expiration updated. User: {afterContext.SamAccountName}.",
                string.Empty,
                connection,
                beforeContext,
                afterContext,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException ex)
        {
            var (message, messageKey) = SanitizeAccountExpirationLdapError(ex);
            return await FailAccountExpirationUpdateAsync(
        request,
                auditAction,
                "AD user account expiration update failed.",
                message,
                BuildAccountExpirationFailureDiagnostic(
                    AccountExpirationModifyStep,
                    request.UserId,
                    loadedBeforeContext?.DistinguishedName,
                    englishMessageOverride: "The LDAP account expiration update failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed,
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                loadedBeforeContext,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken,
                messageKey);
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return await FailAccountExpirationUpdateAsync(
        request,
                auditAction,
                "AD user account expiration update failed.",
                AdManagementApiMessageKeys.Users.AccountExpirationUpdateFailed,
                BuildAccountExpirationFailureDiagnostic(
                    AccountExpirationModifyStep,
                    request.UserId,
                    loadedBeforeContext?.DistinguishedName,
                    englishMessageOverride: "The AD user account expiration update operation failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                loadedBeforeContext,
                AdDirectoryFailureKind.ConnectionFailed,
                                cancellationToken,
                AdManagementApiMessageKeys.Users.AccountExpirationUpdateFailed);
        }
    }

    private async Task<UpdateAdUserAccountExpirationResult> CompleteAccountExpirationUpdateAsync(
        UpdateAdUserAccountExpirationRequest request,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters connection,
        AdUserAccountExpirationContext beforeContext,
        AdUserAccountExpirationContext afterContext,
        CancellationToken cancellationToken)
    {
        await WriteAccountExpirationUpdateSuccessLogsSafelyAsync(
            request,
            auditAction,
            auditDescription,
            connection,
            beforeContext,
            afterContext,
            cancellationToken);

        return new UpdateAdUserAccountExpirationResult(
            true,
            message,
            afterContext.UserId,
            afterContext.SamAccountName,
            afterContext.AccountExpiresDate,
            afterContext.NeverExpires);
    }

    private async Task<UpdateAdUserAccountExpirationResult> FailAccountExpirationUpdateAsync(
        UpdateAdUserAccountExpirationRequest request,
        string auditAction,
        string auditDescription,
        string message,
        string errorDiagnosticJson,
        AdUserAccountExpirationContext? beforeContext,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        string? messageKey = null,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteAccountExpirationUpdateFailureLogsSafelyAsync(
        request,
            auditAction,
            auditDescription,
            beforeContext,
            errorDiagnosticJson,
            cancellationToken);

        return new UpdateAdUserAccountExpirationResult(
            false,
            messageKey ?? message,
            request.UserId.ToString("D"),
            beforeContext?.SamAccountName,
            beforeContext?.AccountExpiresDate,
            beforeContext?.NeverExpires ?? request.NeverExpires,
            failureKind,
            messageParams);
    }

    private async Task WriteAccountExpirationUpdateSuccessLogsSafelyAsync(
        UpdateAdUserAccountExpirationRequest request,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdUserAccountExpirationContext beforeContext,
        AdUserAccountExpirationContext afterContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteAccountExpirationUpdateOperationLogsAsync(
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
                AccountExpirationSuccessLoggingFailedMessage,
                afterContext.UserId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildAccountExpirationUpdateAuditRequest(
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
                AccountExpirationSuccessLoggingFailedMessage,
                afterContext.UserId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteAccountExpirationUpdateFailureLogsSafelyAsync(
        UpdateAdUserAccountExpirationRequest request,
        string auditAction,
        string auditDescription,
        AdUserAccountExpirationContext? beforeContext,
        string errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteAccountExpirationUpdateOperationLogsAsync(
                request,
                AdManagementOperationStatuses.Failed,
                connection: null,
                beforeContext,
                afterContext: null,
                errorDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} UserId={UserId} ActorUserId={ActorUserId}",
                AccountExpirationFailureLoggingFailedMessage,
                request.UserId,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildAccountExpirationUpdateAuditRequest(
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
                AccountExpirationFailureLoggingFailedMessage,
                request.UserId,
                request.ActorUserId);
        }
    }

    private async Task WriteAccountExpirationUpdateOperationLogsAsync(
        UpdateAdUserAccountExpirationRequest request,
        string status,
        AdManagementConnectionParameters? connection,
        AdUserAccountExpirationContext? beforeContext,
        AdUserAccountExpirationContext? afterContext,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var expiresAtSummary = request.NeverExpires
            ? null
            : request.ExpiresAt?.Trim();

        var requestSummary = AdOperationLogSnapshotBuilder.BuildUserAccountExpirationUpdateRequestSummary(
            request.UserId,
            request.NeverExpires,
            expiresAtSummary);

        var beforeSnapshot = beforeContext is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildUserAccountExpirationUpdateBeforeSnapshot(
                beforeContext.UserId,
                beforeContext.SamAccountName,
                beforeContext.UserPrincipalName,
                beforeContext.DistinguishedName,
                beforeContext.NeverExpires,
                beforeContext.AccountExpiresDate);

        var afterSnapshot = afterContext is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildUserAccountExpirationUpdateAfterSnapshot(
                afterContext.UserId,
                afterContext.SamAccountName,
                afterContext.UserPrincipalName,
                afterContext.DistinguishedName,
                afterContext.NeverExpires,
                afterContext.AccountExpiresDate);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.UserAccountExpirationUpdate,
                Status = status,
                TargetObjectType = AdManagementTargetUserTypes.AdUser,
                TargetObjectGuid = afterContext?.UserId ?? beforeContext?.UserId ?? request.UserId.ToString("D"),
                TargetDistinguishedName = afterContext?.DistinguishedName ?? beforeContext?.DistinguishedName,
                TargetSamAccountName = afterContext?.SamAccountName ?? beforeContext?.SamAccountName,
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

    private static AuditLogWriteRequest BuildAccountExpirationUpdateAuditRequest(
        string auditAction,
        string entityId,
        string auditDescription,
        UpdateAdUserAccountExpirationRequest request) =>
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

    private static string BuildAccountExpirationFailureDiagnostic(
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        string? normalizedReasonOverride = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        AdOperationErrorDiagnosticBuilder.BuildUserAccountExpirationUpdateFailureJson(
            step,
            targetObjectGuid,
            targetDistinguishedName,
            englishMessageOverride: englishMessageOverride,
            normalizedReasonOverride: normalizedReasonOverride,
            ldapResultCode: ldapResultCode,
            ldapExceptionErrorCode: ldapExceptionErrorCode,
            ldapDiagnosticMessage: ldapDiagnosticMessage);

    private static void ApplyAccountExpires(
        LdapConnection ldapConnection,
        string distinguishedName,
        long accountExpiresFileTime)
    {
        var modifyRequest = new ModifyRequest(
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            "accountExpires",
            accountExpiresFileTime.ToString());

        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new LdapException((int)response.ResultCode);
        }
    }

    private static (string Message, string MessageKey) SanitizeAccountExpirationLdapError(LdapException exception)
    {
        var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(exception.ErrorCode, exception.Message);
        if (string.Equals(messageKey, AdManagementApiMessageKeys.Ldap.UpdateUserFailed, StringComparison.Ordinal))
        {
            messageKey = AdManagementApiMessageKeys.Users.AccountExpirationUpdateFailed;
        }

        return (messageKey, messageKey);
    }

    private static bool TryLoadUserAccountExpirationContext(
        LdapConnection ldapConnection,
        AdManagementConnectionParameters connection,
        Guid objectGuid,
        out AdUserAccountExpirationContext context)
    {
        context = null!;
        if (!TryFindUserEntryByObjectGuid(
                ldapConnection,
                connection,
                objectGuid,
                ["distinguishedName", "sAMAccountName", "userPrincipalName", "accountExpires", "objectGUID"],
                out var entry))
        {
            return false;
        }

        if (!TryGetObjectGuid(entry, out var userGuid))
        {
            return false;
        }

        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var accountExpiresRaw = GetFirstLong(entry, "accountExpires");
        var neverExpires = AdAccountExpirationDateConverter.IsNeverExpires(accountExpiresRaw);
        var accountExpiresDate = AdAccountExpirationDateConverter.ToDisplayDateString(accountExpiresRaw);

        context = new AdUserAccountExpirationContext(
            userGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            neverExpires,
            accountExpiresDate);

        return true;
    }

    private sealed record AdUserAccountExpirationContext(
        string UserId,
        string DistinguishedName,
        string? SamAccountName,
        string? UserPrincipalName,
        bool NeverExpires,
        string? AccountExpiresDate);
}
