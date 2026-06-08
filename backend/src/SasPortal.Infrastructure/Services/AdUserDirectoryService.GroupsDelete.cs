using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string DeleteGroupSuccessLoggingFailedMessage =
        "AD group delete operation succeeded but logging failed.";
    private const string DeleteGroupFailureLoggingFailedMessage =
        "AD group delete operation failed but logging failed.";

    private static class AdGroupDeleteSteps
    {
        public const string LoadGroup = "LoadGroup";
        public const string Preflight = "Preflight";
        public const string DeleteGroup = "DeleteGroup";
        public const string WriteOperationLog = "WriteOperationLog";
    }

    public async Task<DeleteAdGroupResult> DeleteGroupAsync(
        DeleteAdGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new DeleteAdGroupResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new DeleteAdGroupResult(
                false,
                AdManagementNotConfiguredMessage,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        var context = connectionResult.Context;

        try
        {
            using var ldapConnection = CreateBoundConnection(context);

            if (!TryLoadGroupForUpdate(
                    ldapConnection,
                    groupsSearchBase,
                    request.GroupId,
                    out var groupDetail,
                    out _))
            {
                return await FailGroupDeleteAsync(
                    request,
                    context.Connection,
                    AdLdapErrorNormalizer.GroupNotFoundMessage,
                    AdDirectoryFailureKind.NotFound,
                    AdGroupDeleteOperationDiagnosticBuilder.BuildNotFoundJson(
                        AdGroupDeleteSteps.LoadGroup,
                        request.GroupId),
                    beforeDetail: null,
                    targetDistinguishedName: null,
                    cancellationToken);
            }

            var distinguishedName = groupDetail!.DistinguishedName;
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                return await FailGroupDeleteAsync(
                    request,
                    context.Connection,
                    AdLdapErrorNormalizer.GroupNotFoundMessage,
                    AdDirectoryFailureKind.NotFound,
                    AdGroupDeleteOperationDiagnosticBuilder.BuildNotFoundJson(
                        AdGroupDeleteSteps.LoadGroup,
                        request.GroupId),
                    beforeDetail: groupDetail,
                    targetDistinguishedName: null,
                    cancellationToken);
            }

            if (!groupDetail.SecurityEnabled)
            {
                return await FailGroupDeleteAsync(
                    request,
                    context.Connection,
                    AdLdapErrorNormalizer.DeleteGroupFailedMessage,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdGroupDeleteOperationDiagnosticBuilder.BuildPreflightJson(
                        AdGroupDeleteSteps.Preflight,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "Only AD security groups can be deleted through this operation.",
                        request.GroupId,
                        distinguishedName),
                    beforeDetail: groupDetail,
                    targetDistinguishedName: distinguishedName,
                    cancellationToken);
            }

            try
            {
                ExecuteDeleteGroup(ldapConnection, distinguishedName);
            }
            catch (DeleteGroupLdapException ex)
            {
                return await FailGroupDeleteAsync(
                    request,
                    context.Connection,
                    ex.UserMessage,
                    ex.FailureKind,
                    AdGroupDeleteOperationDiagnosticBuilder.BuildGenericFailureJson(
                        AdGroupDeleteSteps.DeleteGroup,
                        ex.NormalizedReason,
                        ex.EnglishMessage,
                        request.GroupId,
                        distinguishedName,
                        ex.LdapResultCode,
                        ex.LdapExceptionErrorCode,
                        ex.LdapDiagnosticMessage),
                    beforeDetail: groupDetail,
                    targetDistinguishedName: distinguishedName,
                    cancellationToken);
            }

            await WriteGroupDeleteSuccessLogsAsync(
                request,
                context.Connection,
                groupDetail,
                distinguishedName,
                cancellationToken);

            return new DeleteAdGroupResult(
                true,
                string.Empty,
                groupDetail.Id);
        }
        catch (LdapException ex)
        {
            return await FailGroupDeleteAsync(
                request,
                context.Connection,
                AdLdapErrorNormalizer.Normalize(ex.ErrorCode, ex.Message),
                AdDirectoryFailureKind.ConnectionFailed,
                AdGroupDeleteOperationDiagnosticBuilder.BuildGenericFailureJson(
                    AdGroupDeleteSteps.DeleteGroup,
                    AdUserUpdateNormalizedReasons.ConnectionFailed,
                    "The AD security group could not be deleted.",
                    request.GroupId,
                    null,
                    ex.ErrorCode,
                    ex.ErrorCode,
                    ex.Message),
                beforeDetail: null,
                targetDistinguishedName: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD group delete unexpected failure. GroupId={GroupId} ActorUserId={ActorUserId}",
                request.GroupId,
                request.ActorUserId);

            return await FailGroupDeleteAsync(
                request,
                context.Connection,
                AdLdapErrorNormalizer.DeleteGroupFailedMessage,
                AdDirectoryFailureKind.ConnectionFailed,
                AdGroupDeleteOperationDiagnosticBuilder.BuildGenericFailureJson(
                    AdGroupDeleteSteps.DeleteGroup,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The AD security group could not be deleted.",
                    request.GroupId,
                    null),
                beforeDetail: null,
                targetDistinguishedName: null,
                cancellationToken);
        }
    }

    private static void ExecuteDeleteGroup(LdapConnection ldapConnection, string distinguishedName)
    {
        var deleteRequest = new DeleteRequest(distinguishedName);
        var response = (DirectoryResponse)ldapConnection.SendRequest(deleteRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            var userMessage = AdLdapErrorNormalizer.Normalize((int)response.ResultCode, response.ErrorMessage);
            throw new DeleteGroupLdapException(
                userMessage,
                MapGroupFailureKind(response.ResultCode),
                ResolveDeleteNormalizedReason(response.ResultCode),
                ResolveDeleteEnglishMessage(response.ResultCode),
                (int)response.ResultCode,
                (int)response.ResultCode,
                response.ErrorMessage);
        }
    }

    private static string ResolveDeleteNormalizedReason(ResultCode resultCode) =>
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

    private static string ResolveDeleteEnglishMessage(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => "The AD security group could not be found.",
            ResultCode.InsufficientAccessRights =>
                "The AD service account does not have permission to delete this group.",
            _ => "The AD security group could not be deleted.",
        };

    private async Task<DeleteAdGroupResult> FailGroupDeleteAsync(
        DeleteAdGroupRequest request,
        AdManagementConnectionParameters connection,
        string message,
        AdDirectoryFailureKind failureKind,
        string operationDiagnosticJson,
        AdGroupDetail? beforeDetail,
        string? targetDistinguishedName,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupDeleteFailureLogsAsync(
                request,
                connection,
                beforeDetail,
                targetDistinguishedName,
                operationDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} ActorUserId={ActorUserId}",
                DeleteGroupFailureLoggingFailedMessage,
                request.GroupId,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Delete",
                    EntityName = "AdGroup",
                    EntityId = request.GroupId.ToString("D"),
                    Description = "AD security group delete failed.",
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
                DeleteGroupFailureLoggingFailedMessage,
                request.GroupId,
                request.ActorUserId);
        }

        return new DeleteAdGroupResult(false, message, null, failureKind);
    }

    private async Task WriteGroupDeleteSuccessLogsAsync(
        DeleteAdGroupRequest request,
        AdManagementConnectionParameters connection,
        AdGroupDetail groupDetail,
        string targetDistinguishedName,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupDeleteOperationLogAsync(
                request,
                connection,
                AdManagementOperationStatuses.Succeeded,
                groupDetail,
                targetDistinguishedName,
                errorMessage: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} GroupId={GroupId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                DeleteGroupSuccessLoggingFailedMessage,
                groupDetail.Id,
                groupDetail.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Delete",
                    EntityName = "AdGroup",
                    EntityId = groupDetail.Id,
                    Description = "AD security group deleted.",
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
                DeleteGroupSuccessLoggingFailedMessage,
                groupDetail.Id,
                groupDetail.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteGroupDeleteFailureLogsAsync(
        DeleteAdGroupRequest request,
        AdManagementConnectionParameters connection,
        AdGroupDetail? beforeDetail,
        string? targetDistinguishedName,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await WriteGroupDeleteOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Failed,
            beforeDetail,
            targetDistinguishedName,
            errorMessage: operationDiagnosticJson,
            cancellationToken);
    }

    private async Task WriteGroupDeleteOperationLogAsync(
        DeleteAdGroupRequest request,
        AdManagementConnectionParameters connection,
        string status,
        AdGroupDetail? beforeDetail,
        string? targetDistinguishedName,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.GroupDelete,
                Status = status,
                TargetObjectType = AdManagementTargetGroupTypes.AdGroup,
                TargetDistinguishedName = targetDistinguishedName ?? beforeDetail?.DistinguishedName,
                TargetObjectGuid = beforeDetail?.Id ?? request.GroupId.ToString("D"),
                TargetSamAccountName = beforeDetail?.SamAccountName,
                RequestSummaryJson = AdGroupDeleteSnapshotBuilder.BuildRequestSummary(request, beforeDetail),
                BeforeSnapshotJson = AdGroupDeleteSnapshotBuilder.Build(beforeDetail),
                AfterSnapshotJson = null,
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

    private sealed class DeleteGroupLdapException(
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
