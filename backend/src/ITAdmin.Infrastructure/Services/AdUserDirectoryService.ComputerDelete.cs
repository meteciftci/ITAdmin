using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdComputersDirectoryService : IAdComputerDeleteService
{
    private const string ComputerDeleteSuccessLoggingFailedMessage =
        "AD computer delete operation succeeded but logging failed.";
    private const string ComputerDeleteFailureLoggingFailedMessage =
        "AD computer delete operation failed but logging failed.";

    private static class AdComputerDeleteSteps
    {
        public const string LoadComputer = "LoadComputer";
        public const string Preflight = "Preflight";
        public const string DeleteComputer = "DeleteComputer";
        public const string VerifyDeleted = "VerifyDeleted";
    }

    public async Task<DeleteAdComputerResult> DeleteComputerAsync(
        DeleteAdComputerRequest request,
        CancellationToken cancellationToken = default)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailComputerDeleteAsync(
        request,
                connectionResult.MessageKey,
                connectionResult.Context?.Connection,
                beforeState: null,
                connectionResult.FailureKind,
                AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                    AdManagementOperationTypes.ComputerDelete,
                    AdComputerDeleteSteps.LoadComputer,
                    request.ComputerId,
                    targetDistinguishedName: null,
                    englishMessageOverride: connectionResult.MessageKey),
                cancellationToken);
        }

        var context = connectionResult.Context;
        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return await FailComputerDeleteAsync(
        request,
                AdManagementApiMessageKeys.Common.NotConfigured,
                context.Connection,
                beforeState: null,
                AdDirectoryFailureKind.NotConfigured,
                AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                    AdManagementOperationTypes.ComputerDelete,
                    AdComputerDeleteSteps.LoadComputer,
                    request.ComputerId,
                    targetDistinguishedName: null,
                    englishMessageOverride: AdManagementApiMessageKeys.Common.NotConfigured,
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                cancellationToken);
        }

        AdComputerAccountState? loadedBeforeState = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(context);

            if (!TryLoadComputerAccountState(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var beforeState))
            {
                return await FailComputerDeleteAsync(
        request,
                    AdManagementApiMessageKeys.Computers.NotFound,
                    context.Connection,
                    beforeState: null,
                    AdDirectoryFailureKind.NotFound,
                    AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                        AdManagementOperationTypes.ComputerDelete,
                        AdComputerDeleteSteps.LoadComputer,
                        request.ComputerId,
                        targetDistinguishedName: null,
                        englishMessageOverride: AdManagementApiMessageKeys.Computers.NotFound,
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    cancellationToken);
            }

            loadedBeforeState = beforeState;

            if (AdComputerAccountGuard.IsProtectedComputer(
                    beforeState.PrimaryGroupId,
                    beforeState.UserAccountControl,
                    beforeState.IsCriticalSystemObject))
            {
                return await FailComputerDeleteAsync(
        request,
                    AdComputerAccountGuard.ProtectedComputerDeleteMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                        AdManagementOperationTypes.ComputerDelete,
                        AdComputerDeleteSteps.Preflight,
                        request.ComputerId,
                        beforeState.DistinguishedName,
                        englishMessageOverride: AdComputerAccountGuard.ProtectedComputerDeleteMessage,
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    cancellationToken);
            }

            try
            {
                ExecuteDeleteComputer(ldapConnection, beforeState.DistinguishedName);
            }
            catch (DeleteComputerLdapException ex)
            {
                return await FailComputerDeleteAsync(
        request,
                    ex.UserMessage,
                    context.Connection,
                    beforeState,
                    ex.FailureKind,
                    AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                        AdManagementOperationTypes.ComputerDelete,
                        AdComputerDeleteSteps.DeleteComputer,
                        request.ComputerId,
                        beforeState.DistinguishedName,
                        englishMessageOverride: ex.EnglishMessage,
                        ldapResultCode: ex.LdapResultCode,
                        ldapExceptionErrorCode: ex.LdapExceptionErrorCode,
                        ldapDiagnosticMessage: ex.LdapDiagnosticMessage,
                        normalizedReasonOverride: ex.NormalizedReason),
                    cancellationToken);
            }

            if (TryLoadComputerAccountState(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out _))
            {
                return await FailComputerDeleteAsync(
        request,
                    AdManagementApiMessageKeys.Computers.DeleteFailed,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.ConnectionFailed,
                    AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                        AdManagementOperationTypes.ComputerDelete,
                        AdComputerDeleteSteps.VerifyDeleted,
                        request.ComputerId,
                        beforeState.DistinguishedName,
                        englishMessageOverride: "The AD computer account still exists after delete.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.DeleteFailed),
                    cancellationToken);
            }

            await WriteComputerDeleteSuccessLogsAsync(
                request,
                context.Connection,
                beforeState,
                cancellationToken);

            return new DeleteAdComputerResult(
                true,
                AdManagementApiMessageKeys.Computers.DeleteSuccess,
                beforeState.ComputerId,
                beforeState.Name ?? beforeState.SamAccountName,
                beforeState.DistinguishedName);
        }
        catch (LdapException ex)
        {
            return await FailComputerDeleteAsync(
        request,
                SanitizeComputerDeleteLdapError(ex),
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                    AdManagementOperationTypes.ComputerDelete,
                    AdComputerDeleteSteps.DeleteComputer,
                    request.ComputerId,
                    loadedBeforeState?.DistinguishedName,
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD computer delete unexpected failure. ComputerId={ComputerId} ActorUserId={ActorUserId}",
                request.ComputerId,
                request.ActorUserId);

            return await FailComputerDeleteAsync(
        request,
                AdManagementApiMessageKeys.Computers.DeleteFailed,
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                    AdManagementOperationTypes.ComputerDelete,
                    AdComputerDeleteSteps.DeleteComputer,
                    request.ComputerId,
                    loadedBeforeState?.DistinguishedName,
                    englishMessageOverride: "The AD computer account could not be deleted."),
                cancellationToken);
        }
    }

    private static void ExecuteDeleteComputer(LdapConnection ldapConnection, string distinguishedName)
    {
        var deleteRequest = new DeleteRequest(distinguishedName);
        var response = (DirectoryResponse)ldapConnection.SendRequest(deleteRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            var userMessage = AdLdapErrorNormalizer.NormalizeMessageKey((int)response.ResultCode, response.ErrorMessage);
            throw new DeleteComputerLdapException(
                userMessage,
                MapComputerDeleteFailureKind(response.ResultCode),
                ResolveComputerDeleteNormalizedReason(response.ResultCode),
                ResolveComputerDeleteEnglishMessage(response.ResultCode),
                (int)response.ResultCode,
                (int)response.ResultCode,
                response.ErrorMessage);
        }
    }

    private static AdDirectoryFailureKind MapComputerDeleteFailureKind(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdDirectoryFailureKind.NotFound,
            ResultCode.InvalidDNSyntax or ResultCode.NamingViolation =>
                AdDirectoryFailureKind.InvalidRequest,
            ResultCode.InsufficientAccessRights => AdDirectoryFailureKind.InvalidRequest,
            _ => AdDirectoryFailureKind.ConnectionFailed,
        };

    private static string ResolveComputerDeleteNormalizedReason(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdUserUpdateNormalizedReasons.NoSuchObject,
            ResultCode.InvalidDNSyntax or ResultCode.NamingViolation =>
                AdUserUpdateNormalizedReasons.InvalidDnSyntax,
            ResultCode.InsufficientAccessRights => AdUserUpdateNormalizedReasons.InsufficientAccessRights,
            ResultCode.Unavailable or ResultCode.TimeLimitExceeded or ResultCode.Busy =>
                AdUserUpdateNormalizedReasons.ConnectionFailed,
            _ => AdUserUpdateNormalizedReasons.DeleteFailed,
        };

    private static string ResolveComputerDeleteEnglishMessage(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => "The AD computer account could not be found.",
            ResultCode.InsufficientAccessRights =>
                "The AD service account does not have permission to delete this computer account.",
            _ => "The AD computer account could not be deleted.",
        };

    private async Task<DeleteAdComputerResult> FailComputerDeleteAsync(
        DeleteAdComputerRequest request,
        string message,
        AdManagementConnectionParameters? connection,
        AdComputerAccountState? beforeState,
        AdDirectoryFailureKind? failureKind,
        string operationDiagnosticJson,
        CancellationToken cancellationToken,
        string? messageKey = null,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        try
        {
            await WriteComputerDeleteFailureLogsAsync(
        request,
                connection,
                beforeState,
                operationDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} ComputerId={ComputerId} ActorUserId={ActorUserId}",
                ComputerDeleteFailureLoggingFailedMessage,
                request.ComputerId,
                request.ActorUserId);
        }

        return new DeleteAdComputerResult(
            false,
            message,
            null,
            null,
            null,
            failureKind);
    }

    private async Task WriteComputerDeleteSuccessLogsAsync(
        DeleteAdComputerRequest request,
        AdManagementConnectionParameters connection,
        AdComputerAccountState beforeState,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerDeleteOperationLogAsync(
                request,
                connection,
                AdManagementOperationStatuses.Succeeded,
                beforeState,
                errorDiagnosticJson: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} ComputerId={ComputerId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                ComputerDeleteSuccessLoggingFailedMessage,
                beforeState.ComputerId,
                beforeState.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Delete",
                    EntityName = "AdComputer",
                    EntityId = beforeState.ComputerId,
                    Description =
                        $"AD computer account deleted. Computer: {beforeState.Name ?? beforeState.SamAccountName}.",
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
                "{LogMessage} ComputerId={ComputerId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                ComputerDeleteSuccessLoggingFailedMessage,
                beforeState.ComputerId,
                beforeState.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteComputerDeleteFailureLogsAsync(
        DeleteAdComputerRequest request,
        AdManagementConnectionParameters? connection,
        AdComputerAccountState? beforeState,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await WriteComputerDeleteOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Failed,
            beforeState,
            operationDiagnosticJson,
            cancellationToken);
    }

    private async Task WriteComputerDeleteOperationLogAsync(
        DeleteAdComputerRequest request,
        AdManagementConnectionParameters? connection,
        string status,
        AdComputerAccountState? beforeState,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var beforeSnapshot = beforeState is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerDeleteBeforeSnapshot(
                beforeState.ComputerId,
                beforeState.SamAccountName,
                beforeState.Name,
                beforeState.DistinguishedName,
                beforeState.IsEnabled,
                beforeState.UserAccountControl,
                beforeState.PrimaryGroupId);

        var afterSnapshot = string.Equals(
            status,
            AdManagementOperationStatuses.Succeeded,
            StringComparison.Ordinal)
            && beforeState is not null
            ? AdOperationLogSnapshotBuilder.BuildComputerDeleteAfterSnapshot(
                beforeState.ComputerId,
                beforeState.SamAccountName,
                beforeState.Name,
                beforeState.DistinguishedName)
            : null;

        var isSuccess = string.Equals(
            status,
            AdManagementOperationStatuses.Succeeded,
            StringComparison.Ordinal);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.ComputerDelete,
                Status = status,
                TargetObjectType = AdManagementTargetComputerTypes.AdComputer,
                TargetDistinguishedName = beforeState?.DistinguishedName,
                TargetObjectGuid = beforeState?.ComputerId ?? request.ComputerId.ToString("D"),
                TargetSamAccountName = beforeState?.SamAccountName,
                ErrorCode = isSuccess
                    ? null
                    : AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(errorDiagnosticJson),
                ErrorMessage = isSuccess ? null : errorDiagnosticJson,
                RequestSummaryJson = AdOperationLogSnapshotBuilder.BuildComputerDeleteRequestSummary(
                    request.ComputerId),
                BeforeSnapshotJson = beforeSnapshot,
                AfterSnapshotJson = afterSnapshot,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = connection is null ? null : ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private static string SanitizeComputerDeleteLdapError(LdapException exception) =>
        string.IsNullOrWhiteSpace(exception.Message)
            ? AdManagementApiMessageKeys.Computers.DeleteFailed
            : AdManagementApiMessageKeys.Computers.DeleteFailed;

    private sealed class DeleteComputerLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind,
        string normalizedReason,
        string englishMessage,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) : Exception(userMessage)
    {
        public string UserMessage { get; } = userMessage;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
        public string NormalizedReason { get; } = normalizedReason;
        public string EnglishMessage { get; } = englishMessage;
        public int? LdapResultCode { get; } = ldapResultCode;
        public int? LdapExceptionErrorCode { get; } = ldapExceptionErrorCode;
        public string? LdapDiagnosticMessage { get; } = ldapDiagnosticMessage;
    }
}
