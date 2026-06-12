using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdComputerUpdateService
{
    private const int ComputerDescriptionMaxLength = 1024;
    private const string ComputerUpdateFailedMessage = "Bilgisayar açıklaması güncellenemedi.";
    private const string ComputerUpdateSuccessMessage = "Bilgisayar açıklaması güncellendi.";
    private const string ComputerUpdateNoChangesMessage = "Bilgisayar açıklamasında değişiklik yok.";
    private const string ComputerDescriptionTooLongMessage = "Açıklama en fazla 1024 karakter olabilir.";
    private const string ComputerUpdateSuccessLoggingFailedMessage =
        "AD computer update operation succeeded but logging failed.";
    private const string ComputerUpdateFailureLoggingFailedMessage =
        "AD computer update operation failed but logging failed.";
    private const string ComputerUpdateValidateStep = "ValidateRequest";
    private const string ComputerUpdateLoadStep = "LoadComputer";
    private const string ComputerUpdateModifyStep = "ModifyDescription";
    private const string ComputerUpdateReloadStep = "ReloadComputer";

    private static readonly string[] ComputerUpdateAttributes =
    [
        "objectGUID",
        "distinguishedName",
        "sAMAccountName",
        "name",
        "cn",
        "description",
        "userAccountControl",
        "primaryGroupID",
        "isCriticalSystemObject",
    ];

    public Task<UpdateAdComputerResult> UpdateComputerAsync(
        UpdateAdComputerRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateComputerDescriptionAsync(request, cancellationToken);

    private async Task<UpdateAdComputerResult> UpdateComputerDescriptionAsync(
        UpdateAdComputerRequest request,
        CancellationToken cancellationToken)
    {
        const string auditAction = "Update";

        var normalizedDescription = NormalizeComputerDescription(request.Description);
        if (normalizedDescription.Length > ComputerDescriptionMaxLength)
        {
            return await FailComputerUpdateAsync(
                request,
                auditAction,
                "AD computer update failed.",
                ComputerDescriptionTooLongMessage,
                BuildComputerUpdateFailureDiagnostic(
                    ComputerUpdateValidateStep,
                    request.ComputerId,
                    null,
                    englishMessageOverride: "Computer description exceeds the maximum allowed length.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                beforeContext: null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailComputerUpdateAsync(
                request,
                auditAction,
                "AD computer update failed.",
                connectionResult.Message,
                BuildComputerUpdateFailureDiagnostic(
                    ComputerUpdateValidateStep,
                    request.ComputerId,
                    null,
                    englishMessageOverride: "The LDAP connection failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                beforeContext: null,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var context = connectionResult.Context;
        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return await FailComputerUpdateAsync(
                request,
                auditAction,
                "AD computer update failed.",
                AdManagementNotConfiguredMessage,
                BuildComputerUpdateFailureDiagnostic(
                    ComputerUpdateValidateStep,
                    request.ComputerId,
                    null,
                    englishMessageOverride: "AD management is not configured.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                beforeContext: null,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        AdComputerUpdateContext? loadedBeforeContext = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            if (!TryLoadComputerUpdateContext(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var beforeContext))
            {
                return await FailComputerUpdateAsync(
                    request,
                    auditAction,
                    "AD computer update failed.",
                    ComputerNotFoundMessage,
                    BuildComputerUpdateFailureDiagnostic(
                        ComputerUpdateLoadStep,
                        request.ComputerId,
                        null,
                        englishMessageOverride: "The AD computer could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    beforeContext: null,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            loadedBeforeContext = beforeContext;

            if (AdComputerAccountGuard.IsProtectedComputer(
                    beforeContext.PrimaryGroupId,
                    beforeContext.UserAccountControl,
                    beforeContext.IsCriticalSystemObject))
            {
                return await FailComputerUpdateAsync(
                    request,
                    auditAction,
                    "AD computer update failed.",
                    AdComputerAccountGuard.ProtectedComputerWriteOperationMessage,
                    BuildComputerUpdateFailureDiagnostic(
                        ComputerUpdateValidateStep,
                        request.ComputerId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "This computer account cannot be updated.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    beforeContext,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            var currentDescription = NormalizeComputerDescription(beforeContext.Description);
            if (string.Equals(currentDescription, normalizedDescription, StringComparison.Ordinal))
            {
                return await CompleteComputerUpdateAsync(
                    request,
                    auditAction,
                    $"AD computer update skipped (no changes): {beforeContext.Name ?? beforeContext.SamAccountName}.",
                    ComputerUpdateNoChangesMessage,
                    context.Connection,
                    ldapConnection,
                    computersSearchBase,
                    beforeContext,
                    beforeContext,
                    normalizedDescription,
                    requestSummaryJson: """{"changeStatus":"NoChangesDetected"}""",
                    cancellationToken);
            }

            ApplyComputerDescription(
                ldapConnection,
                beforeContext.DistinguishedName,
                normalizedDescription);

            if (!TryLoadComputerUpdateContext(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var afterContext))
            {
                return await FailComputerUpdateAsync(
                    request,
                    auditAction,
                    "AD computer update failed.",
                    ComputerUpdateFailedMessage,
                    BuildComputerUpdateFailureDiagnostic(
                        ComputerUpdateReloadStep,
                        request.ComputerId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "The AD computer update operation failed after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    loadedBeforeContext,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            return await CompleteComputerUpdateAsync(
                request,
                auditAction,
                $"AD computer description updated. Computer: {afterContext.Name ?? afterContext.SamAccountName}.",
                ComputerUpdateSuccessMessage,
                context.Connection,
                ldapConnection,
                computersSearchBase,
                beforeContext,
                afterContext,
                normalizedDescription,
                requestSummaryJson: null,
                cancellationToken);
        }
        catch (LdapException ex)
        {
            return await FailComputerUpdateAsync(
                request,
                auditAction,
                "AD computer update failed.",
                SanitizeComputerUpdateLdapError(ex),
                AdOperationErrorDiagnosticBuilder.BuildComputerUpdateFailureJson(
                    ComputerUpdateModifyStep,
                    request.ComputerId,
                    loadedBeforeContext?.DistinguishedName,
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                loadedBeforeContext,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
        catch (Exception)
        {
            return await FailComputerUpdateAsync(
                request,
                auditAction,
                "AD computer update failed.",
                ComputerUpdateFailedMessage,
                BuildComputerUpdateFailureDiagnostic(
                    ComputerUpdateModifyStep,
                    request.ComputerId,
                    loadedBeforeContext?.DistinguishedName,
                    englishMessageOverride: "The AD computer update operation failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                loadedBeforeContext,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private async Task<UpdateAdComputerResult> CompleteComputerUpdateAsync(
        UpdateAdComputerRequest request,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters connection,
        LdapConnection ldapConnection,
        string computersSearchBase,
        AdComputerUpdateContext beforeContext,
        AdComputerUpdateContext afterContext,
        string normalizedDescription,
        string? requestSummaryJson,
        CancellationToken cancellationToken)
    {
        AdComputerDetail? computerDetail = null;
        if (TryLoadComputerDetail(
                ldapConnection,
                computersSearchBase,
                Guid.Parse(afterContext.ComputerId),
                out var detail))
        {
            computerDetail = detail;
        }

        await WriteComputerUpdateSuccessLogsSafelyAsync(
            request,
            auditAction,
            auditDescription,
            connection,
            beforeContext,
            afterContext,
            normalizedDescription,
            requestSummaryJson,
            cancellationToken);

        return new UpdateAdComputerResult(true, message, computerDetail);
    }

    private async Task<UpdateAdComputerResult> FailComputerUpdateAsync(
        UpdateAdComputerRequest request,
        string auditAction,
        string auditDescription,
        string message,
        string errorDiagnosticJson,
        AdComputerUpdateContext? beforeContext,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken)
    {
        await WriteComputerUpdateFailureLogsSafelyAsync(
            request,
            auditAction,
            auditDescription,
            beforeContext,
            errorDiagnosticJson,
            cancellationToken);

        return new UpdateAdComputerResult(false, message, FailureKind: failureKind);
    }

    private static string NormalizeComputerDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        return description.Trim();
    }

    private static bool TryLoadComputerUpdateContext(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        out AdComputerUpdateContext context)
    {
        context = null!;
        var filter = AdLdapComputerFilterHelper.BuildComputerObjectGuidFilter(objectGuid);
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            ComputerUpdateAttributes)
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

        var cn = GetFirstString(entry, "cn");
        var name = GetFirstString(entry, "name")
            ?? cn
            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;

        context = new AdComputerUpdateContext(
            resolvedGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            name,
            GetFirstString(entry, "description"),
            GetFirstInt(entry, "userAccountControl"),
            GetFirstInt(entry, "primaryGroupID"),
            GetFirstBool(entry, "isCriticalSystemObject"));

        return true;
    }

    private static void ApplyComputerDescription(
        LdapConnection ldapConnection,
        string distinguishedName,
        string normalizedDescription)
    {
        if (string.IsNullOrEmpty(normalizedDescription))
        {
            var deleteRequest = new ModifyRequest(
                distinguishedName,
                DirectoryAttributeOperation.Delete,
                "description");
            var deleteResponse = (DirectoryResponse)ldapConnection.SendRequest(deleteRequest);
            if (deleteResponse.ResultCode != ResultCode.Success
                && deleteResponse.ResultCode != ResultCode.NoSuchAttribute)
            {
                throw new LdapException((int)deleteResponse.ResultCode);
            }

            return;
        }

        var modifyRequest = new ModifyRequest(
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            "description",
            normalizedDescription);
        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new LdapException((int)response.ResultCode);
        }
    }

    private async Task WriteComputerUpdateOperationLogsAsync(
        UpdateAdComputerRequest request,
        string status,
        AdManagementConnectionParameters? connection,
        AdComputerUpdateContext? beforeContext,
        AdComputerUpdateContext? afterContext,
        string? errorDiagnosticJson,
        string? requestSummaryJson,
        CancellationToken cancellationToken)
    {
        var normalizedDescription = NormalizeComputerDescription(request.Description);
        var requestSummary = requestSummaryJson
            ?? AdOperationLogSnapshotBuilder.BuildComputerUpdateRequestSummary(
                request.ComputerId,
                normalizedDescription);

        var beforeSnapshot = beforeContext is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerUpdateBeforeSnapshot(
                beforeContext.ComputerId,
                beforeContext.SamAccountName,
                beforeContext.Name,
                beforeContext.DistinguishedName,
                NormalizeComputerDescription(beforeContext.Description));

        var afterSnapshot = afterContext is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerUpdateAfterSnapshot(
                afterContext.ComputerId,
                afterContext.SamAccountName,
                afterContext.Name,
                afterContext.DistinguishedName,
                NormalizeComputerDescription(afterContext.Description));

        var isSuccess = string.Equals(
            status,
            AdManagementOperationStatuses.Succeeded,
            StringComparison.Ordinal);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.ComputerUpdate,
                Status = status,
                TargetObjectType = AdManagementTargetComputerTypes.AdComputer,
                TargetObjectGuid = afterContext?.ComputerId ?? beforeContext?.ComputerId ?? request.ComputerId.ToString("D"),
                TargetDistinguishedName = afterContext?.DistinguishedName ?? beforeContext?.DistinguishedName,
                TargetSamAccountName = afterContext?.SamAccountName ?? beforeContext?.SamAccountName,
                RequestSummaryJson = requestSummary,
                BeforeSnapshotJson = beforeSnapshot,
                AfterSnapshotJson = afterSnapshot,
                ErrorCode = isSuccess
                    ? null
                    : AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(errorDiagnosticJson),
                ErrorMessage = isSuccess ? null : errorDiagnosticJson,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = connection is null ? null : ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private async Task WriteComputerUpdateSuccessLogsSafelyAsync(
        UpdateAdComputerRequest request,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdComputerUpdateContext beforeContext,
        AdComputerUpdateContext afterContext,
        string normalizedDescription,
        string? requestSummaryJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerUpdateOperationLogsAsync(
                request,
                AdManagementOperationStatuses.Succeeded,
                connection,
                beforeContext,
                afterContext,
                errorDiagnosticJson: null,
                requestSummaryJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} ComputerId={ComputerId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                ComputerUpdateSuccessLoggingFailedMessage,
                afterContext.ComputerId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = auditAction,
                    EntityName = "AdComputer",
                    EntityId = afterContext.ComputerId,
                    Description = auditDescription,
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
                ComputerUpdateSuccessLoggingFailedMessage,
                afterContext.ComputerId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteComputerUpdateFailureLogsSafelyAsync(
        UpdateAdComputerRequest request,
        string auditAction,
        string? auditDescription,
        AdComputerUpdateContext? beforeContext,
        string diagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerUpdateOperationLogsAsync(
                request,
                AdManagementOperationStatuses.Failed,
                connection: null,
                beforeContext,
                afterContext: beforeContext,
                diagnosticJson,
                requestSummaryJson: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} ComputerId={ComputerId} ActorUserId={ActorUserId}",
                ComputerUpdateFailureLoggingFailedMessage,
                request.ComputerId,
                request.ActorUserId);
        }

        if (string.IsNullOrWhiteSpace(auditDescription))
        {
            return;
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = auditAction,
                    EntityName = "AdComputer",
                    EntityId = request.ComputerId.ToString("D"),
                    Description = auditDescription,
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
                "{LogMessage} ComputerId={ComputerId} ActorUserId={ActorUserId}",
                ComputerUpdateFailureLoggingFailedMessage,
                request.ComputerId,
                request.ActorUserId);
        }
    }

    private static string BuildComputerUpdateFailureDiagnostic(
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        string? normalizedReasonOverride = null) =>
        AdOperationErrorDiagnosticBuilder.BuildComputerUpdateFailureJson(
            step,
            targetObjectGuid,
            targetDistinguishedName,
            englishMessageOverride: englishMessageOverride,
            normalizedReasonOverride: normalizedReasonOverride);

    private static string SanitizeComputerUpdateLdapError(LdapException exception) =>
        string.IsNullOrWhiteSpace(exception.Message)
            || exception.Message.Contains("ldap", StringComparison.OrdinalIgnoreCase)
            ? ComputerUpdateFailedMessage
            : ComputerUpdateFailedMessage;

    private sealed record AdComputerUpdateContext(
        string ComputerId,
        string DistinguishedName,
        string? SamAccountName,
        string? Name,
        string? Description,
        int? UserAccountControl,
        int? PrimaryGroupId,
        bool? IsCriticalSystemObject);
}
