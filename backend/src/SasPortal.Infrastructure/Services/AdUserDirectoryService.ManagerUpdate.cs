using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdUserManagerUpdateService
{
    private const string ManagerUpdateFailedMessage = "Manager güncellenemedi.";
    private const string ManagerUpdateSuccessMessage = "Manager güncellendi.";
    private const string ManagerNotFoundMessage = "Seçilen manager kullanıcısı bulunamadı.";
    private const string ManagerSelfSelectionMessage = "Kullanıcı kendisinin manager'ı olamaz.";
    private const string ManagerInvalidRequestMessage = "Geçersiz manager güncelleme isteği.";
    private const string ManagerUpdateSuccessLoggingFailedMessage =
        "AD user manager update succeeded but logging failed.";
    private const string ManagerUpdateFailureLoggingFailedMessage =
        "AD user manager update failed but logging failed.";
    private const string ManagerValidateStep = "ValidateRequest";
    private const string ManagerLoadUserStep = "LoadUser";
    private const string ManagerLoadManagerStep = "LoadManager";
    private const string ManagerModifyStep = "ModifyManager";
    private const string ManagerReloadUserStep = "ReloadUser";

    public Task<UpdateAdUserManagerResult> UpdateManagerAsync(
        UpdateAdUserManagerRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateUserManagerAsync(request, cancellationToken);

    private async Task<UpdateAdUserManagerResult> UpdateUserManagerAsync(
        UpdateAdUserManagerRequest request,
        CancellationToken cancellationToken)
    {
        const string auditAction = "Update";

        if (!request.ClearManager && request.ManagerUserId is null)
        {
            return await FailManagerUpdateAsync(
                request,
                auditAction,
                "AD user manager update failed.",
                ManagerInvalidRequestMessage,
                BuildManagerFailureDiagnostic(
                    ManagerValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "Manager user id is required when clearManager is false.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        if (request.ManagerUserId == request.UserId)
        {
            return await FailManagerUpdateAsync(
                request,
                auditAction,
                "AD user manager update failed.",
                ManagerSelfSelectionMessage,
                BuildManagerFailureDiagnostic(
                    ManagerValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "A user cannot be their own manager.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailManagerUpdateAsync(
                request,
                auditAction,
                "AD user manager update failed.",
                connectionResult.Message,
                BuildManagerFailureDiagnostic(
                    ManagerValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "The LDAP connection failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                null,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var connection = connectionResult.Context.Connection;
        if (AdLdapUserSearchBases.ResolveDistinctSearchBases(connection).Count == 0)
        {
            return await FailManagerUpdateAsync(
                request,
                auditAction,
                "AD user manager update failed.",
                AdManagementNotConfiguredMessage,
                BuildManagerFailureDiagnostic(
                    ManagerValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "AD management is not configured.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        AdUserManagerOperationContext? loadedBeforeContext = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadUserManagerContext(
                    ldapConnection,
                    connection,
                    request.UserId,
                    out var beforeContext))
            {
                return await FailManagerUpdateAsync(
                    request,
                    auditAction,
                    "AD user manager update failed.",
                    UserNotFoundMessage,
                    BuildManagerFailureDiagnostic(
                        ManagerLoadUserStep,
                        request.UserId,
                        null,
                        englishMessageOverride: "The AD user could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    null,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            loadedBeforeContext = beforeContext;

            AdUserManagerSnapshotInfo? targetManager = null;
            if (!request.ClearManager)
            {
                if (!TryLoadManagerUserContext(
                        ldapConnection,
                        connection,
                        request.ManagerUserId!.Value,
                        out targetManager))
                {
                    return await FailManagerUpdateAsync(
                        request,
                        auditAction,
                        "AD user manager update failed.",
                        ManagerNotFoundMessage,
                        BuildManagerFailureDiagnostic(
                            ManagerLoadManagerStep,
                            request.UserId,
                            beforeContext.DistinguishedName,
                            englishMessageOverride: "The manager user could not be found.",
                            normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                        beforeContext,
                        AdDirectoryFailureKind.NotFound,
                        cancellationToken);
                }
            }

            var targetManagerDn = targetManager?.DistinguishedName;
            if (!request.ClearManager
                && AdLdapDnHelper.AreDistinguishedNamesEqual(
                    beforeContext.Manager?.DistinguishedName,
                    targetManagerDn))
            {
                return await CompleteManagerUpdateAsync(
                    request,
                    auditAction,
                    $"AD user manager update skipped (no changes): {beforeContext.SamAccountName}.",
                    ManagerUpdateSuccessMessage,
                    connection,
                    beforeContext,
                    beforeContext,
                    cancellationToken);
            }

            if (request.ClearManager)
            {
                ClearManagerAttribute(ldapConnection, beforeContext.DistinguishedName);
            }
            else
            {
                SetManagerAttribute(ldapConnection, beforeContext.DistinguishedName, targetManagerDn!);
            }

            if (!TryLoadUserManagerContext(
                    ldapConnection,
                    connection,
                    request.UserId,
                    out var afterContext))
            {
                return await FailManagerUpdateAsync(
                    request,
                    auditAction,
                    "AD user manager update failed.",
                    ManagerUpdateFailedMessage,
                    BuildManagerFailureDiagnostic(
                        ManagerReloadUserStep,
                        request.UserId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "The AD user could not be reloaded after manager update.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                    beforeContext,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            return await CompleteManagerUpdateAsync(
                request,
                auditAction,
                $"AD user manager updated. User: {afterContext.SamAccountName}.",
                ManagerUpdateSuccessMessage,
                connection,
                beforeContext,
                afterContext,
                cancellationToken);
        }
        catch (LdapException ex)
        {
            return await FailManagerUpdateAsync(
                request,
                auditAction,
                "AD user manager update failed.",
                SanitizeManagerLdapError(ex),
                BuildManagerFailureDiagnostic(
                    ManagerModifyStep,
                    request.UserId,
                    loadedBeforeContext?.DistinguishedName,
                    englishMessageOverride: "The LDAP manager update failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed,
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                loadedBeforeContext,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
        catch (Exception)
        {
            return await FailManagerUpdateAsync(
                request,
                auditAction,
                "AD user manager update failed.",
                ManagerUpdateFailedMessage,
                BuildManagerFailureDiagnostic(
                    ManagerModifyStep,
                    request.UserId,
                    loadedBeforeContext?.DistinguishedName,
                    englishMessageOverride: "The AD user manager update operation failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                loadedBeforeContext,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private async Task<UpdateAdUserManagerResult> CompleteManagerUpdateAsync(
        UpdateAdUserManagerRequest request,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters connection,
        AdUserManagerOperationContext beforeContext,
        AdUserManagerOperationContext afterContext,
        CancellationToken cancellationToken)
    {
        await WriteManagerUpdateSuccessLogsSafelyAsync(
            request,
            auditAction,
            auditDescription,
            connection,
            beforeContext,
            afterContext,
            cancellationToken);

        return new UpdateAdUserManagerResult(
            true,
            message,
            afterContext.UserId,
            afterContext.SamAccountName,
            afterContext.Manager?.DistinguishedName,
            afterContext.Manager?.DisplayName);
    }

    private async Task<UpdateAdUserManagerResult> FailManagerUpdateAsync(
        UpdateAdUserManagerRequest request,
        string auditAction,
        string auditDescription,
        string message,
        string errorDiagnosticJson,
        AdUserManagerOperationContext? beforeContext,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken)
    {
        await WriteManagerUpdateFailureLogsSafelyAsync(
            request,
            auditAction,
            auditDescription,
            beforeContext,
            errorDiagnosticJson,
            cancellationToken);

        return new UpdateAdUserManagerResult(
            false,
            message,
            request.UserId.ToString("D"),
            beforeContext?.SamAccountName,
            beforeContext?.Manager?.DistinguishedName,
            beforeContext?.Manager?.DisplayName,
            failureKind);
    }

    private async Task WriteManagerUpdateSuccessLogsSafelyAsync(
        UpdateAdUserManagerRequest request,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdUserManagerOperationContext beforeContext,
        AdUserManagerOperationContext afterContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteManagerUpdateOperationLogsAsync(
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
                ManagerUpdateSuccessLoggingFailedMessage,
                afterContext.UserId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildManagerUpdateAuditRequest(auditAction, afterContext.UserId, auditDescription, request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} UserId={UserId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                ManagerUpdateSuccessLoggingFailedMessage,
                afterContext.UserId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteManagerUpdateFailureLogsSafelyAsync(
        UpdateAdUserManagerRequest request,
        string auditAction,
        string auditDescription,
        AdUserManagerOperationContext? beforeContext,
        string errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteManagerUpdateOperationLogsAsync(
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
                ManagerUpdateFailureLoggingFailedMessage,
                request.UserId,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildManagerUpdateAuditRequest(
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
                ManagerUpdateFailureLoggingFailedMessage,
                request.UserId,
                request.ActorUserId);
        }
    }

    private async Task WriteManagerUpdateOperationLogsAsync(
        UpdateAdUserManagerRequest request,
        string status,
        AdManagementConnectionParameters? connection,
        AdUserManagerOperationContext? beforeContext,
        AdUserManagerOperationContext? afterContext,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var requestSummary = AdOperationLogSnapshotBuilder.BuildUserManagerUpdateRequestSummary(
            request.UserId,
            request.ManagerUserId,
            request.ClearManager);

        var beforeSnapshot = beforeContext is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildUserManagerUpdateBeforeSnapshot(
                beforeContext.UserId,
                beforeContext.SamAccountName,
                beforeContext.UserPrincipalName,
                beforeContext.DistinguishedName,
                beforeContext.Manager);

        var afterSnapshot = afterContext is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildUserManagerUpdateAfterSnapshot(
                afterContext.UserId,
                afterContext.SamAccountName,
                afterContext.UserPrincipalName,
                afterContext.DistinguishedName,
                afterContext.Manager);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.UserManagerUpdate,
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

    private static AuditLogWriteRequest BuildManagerUpdateAuditRequest(
        string auditAction,
        string entityId,
        string auditDescription,
        UpdateAdUserManagerRequest request) =>
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

    private static string BuildManagerFailureDiagnostic(
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        string? normalizedReasonOverride = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        AdOperationErrorDiagnosticBuilder.BuildUserManagerUpdateFailureJson(
            step,
            targetObjectGuid,
            targetDistinguishedName,
            englishMessageOverride: englishMessageOverride,
            normalizedReasonOverride: normalizedReasonOverride,
            ldapResultCode: ldapResultCode,
            ldapExceptionErrorCode: ldapExceptionErrorCode,
            ldapDiagnosticMessage: ldapDiagnosticMessage);

    private static void SetManagerAttribute(
        LdapConnection ldapConnection,
        string userDistinguishedName,
        string managerDistinguishedName)
    {
        var modifyRequest = new ModifyRequest(
            userDistinguishedName,
            DirectoryAttributeOperation.Replace,
            "manager",
            managerDistinguishedName);

        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new LdapException((int)response.ResultCode);
        }
    }

    private static void ClearManagerAttribute(LdapConnection ldapConnection, string userDistinguishedName)
    {
        var modifyRequest = new ModifyRequest(
            userDistinguishedName,
            DirectoryAttributeOperation.Delete,
            "manager");

        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        if (response.ResultCode != ResultCode.Success
            && response.ResultCode != ResultCode.NoSuchAttribute)
        {
            throw new LdapException((int)response.ResultCode);
        }
    }

    private static string SanitizeManagerLdapError(LdapException exception)
    {
        var normalized = AdLdapErrorNormalizer.Normalize(exception.ErrorCode, exception.Message);
        return string.Equals(normalized, AdLdapErrorNormalizer.UpdateUserFailedMessage, StringComparison.Ordinal)
            ? ManagerUpdateFailedMessage
            : normalized;
    }

    private static bool TryLoadUserManagerContext(
        LdapConnection ldapConnection,
        AdManagementConnectionParameters connection,
        Guid objectGuid,
        out AdUserManagerOperationContext context)
    {
        context = null!;
        if (!TryFindUserEntryByObjectGuid(
                ldapConnection,
                connection,
                objectGuid,
                ["distinguishedName", "sAMAccountName", "userPrincipalName", "manager", "objectGUID"],
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

        AdUserManagerSnapshotInfo? manager = null;
        var managerDn = GetFirstString(entry, "manager");
        if (!string.IsNullOrWhiteSpace(managerDn)
            && TryResolveManagerByDistinguishedName(
                ldapConnection,
                managerDn,
                out var managerId,
                out var managerSam,
                out var managerUpn,
                out var managerDisplayName))
        {
            manager = new AdUserManagerSnapshotInfo(
                managerId,
                managerSam,
                managerUpn,
                managerDisplayName,
                managerDn);
        }
        else if (!string.IsNullOrWhiteSpace(managerDn))
        {
            manager = new AdUserManagerSnapshotInfo(null, null, null, null, managerDn);
        }

        context = new AdUserManagerOperationContext(
            userGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            manager);

        return true;
    }

    private static bool TryLoadManagerUserContext(
        LdapConnection ldapConnection,
        AdManagementConnectionParameters connection,
        Guid managerUserId,
        out AdUserManagerSnapshotInfo manager)
    {
        manager = null!;
        if (!TryFindUserEntryByObjectGuid(
                ldapConnection,
                connection,
                managerUserId,
                ["distinguishedName", "sAMAccountName", "userPrincipalName", "displayName", "objectGUID"],
                out var entry))
        {
            return false;
        }

        if (!TryGetObjectGuid(entry, out var objectGuid))
        {
            return false;
        }

        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        manager = new AdUserManagerSnapshotInfo(
            objectGuid.ToString("D"),
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            GetFirstString(entry, "displayName"),
            distinguishedName);

        return true;
    }

    private sealed record AdUserManagerOperationContext(
        string UserId,
        string DistinguishedName,
        string? SamAccountName,
        string? UserPrincipalName,
        AdUserManagerSnapshotInfo? Manager);
}
