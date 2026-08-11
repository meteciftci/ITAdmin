using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdComputersDirectoryService : IAdComputerOuMoveService
{
    private const string ComputerOuMoveSuccessLoggingFailedMessage =
        "AD computer OU move operation succeeded but logging failed.";
    private const string ComputerOuMoveFailureLoggingFailedMessage =
        "AD computer OU move operation failed but logging failed.";
    private const string ComputerOuMoveValidateStep = "ValidateRequest";
    private const string ComputerOuMoveLoadStep = "LoadComputer";
    private const string ComputerOuMoveValidateTargetOuStep = "ValidateTargetOu";
    private const string ComputerOuMoveMoveStep = "MoveComputer";
    private const string ComputerOuMoveReloadStep = "ReloadComputer";

    private static readonly string[] ComputerOuMoveAttributes =
    [
        "objectGUID",
        "distinguishedName",
        "sAMAccountName",
        "name",
        "cn",
        "userAccountControl",
        "primaryGroupID",
        "isCriticalSystemObject",
    ];

    public Task<MoveAdComputerOuResult> MoveOuAsync(
        MoveAdComputerOuRequest request,
        CancellationToken cancellationToken = default) =>
        MoveComputerOuAsync(request, cancellationToken);

    private async Task<MoveAdComputerOuResult> MoveComputerOuAsync(
        MoveAdComputerOuRequest request,
        CancellationToken cancellationToken)
    {
        const string auditAction = "MoveOu";

        var targetOuDn = request.TargetOuDistinguishedName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetOuDn))
        {
            return await FailComputerOuMoveAsync(
        request,
                auditAction,
                "AD computer OU move failed.",
                AdManagementApiMessageKeys.Computers.TargetOuRequired,
                BuildComputerOuMoveFailureDiagnostic(
                    ComputerOuMoveValidateStep,
                    request.ComputerId,
                    null,
                    englishMessageOverride: "Target OU distinguished name is required.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                beforeContext: null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        if (!IsValidTargetOuDistinguishedName(targetOuDn))
        {
            return await FailComputerOuMoveAsync(
        request,
                auditAction,
                "AD computer OU move failed.",
                AdManagementApiMessageKeys.Ldap.InvalidDnSyntax,
                BuildComputerOuMoveFailureDiagnostic(
                    ComputerOuMoveValidateStep,
                    request.ComputerId,
                    null,
                    englishMessageOverride: "The target OU distinguished name is not valid.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                beforeContext: null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailComputerOuMoveAsync(
        request,
                auditAction,
                "AD computer OU move failed.",
                connectionResult.MessageKey,
                BuildComputerOuMoveFailureDiagnostic(
                    ComputerOuMoveValidateStep,
                    request.ComputerId,
                    null,
                    englishMessageOverride: "The LDAP connection failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                beforeContext: null,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var connection = connectionResult.Context.Connection;
        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return await FailComputerOuMoveAsync(
        request,
                auditAction,
                "AD computer OU move failed.",
                AdManagementApiMessageKeys.Common.NotConfigured,
                BuildComputerOuMoveFailureDiagnostic(
                    ComputerOuMoveValidateTargetOuStep,
                    request.ComputerId,
                    null,
                    englishMessageOverride: "AD management is not configured.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                beforeContext: null,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        if (!AdLdapDnHelper.IsEqualOrDescendantOf(targetOuDn, computersSearchBase))
        {
            return await FailComputerOuMoveAsync(
        request,
                auditAction,
                "AD computer OU move failed.",
                AdManagementApiMessageKeys.Computers.InvalidTargetOu,
                BuildComputerOuMoveFailureDiagnostic(
                    ComputerOuMoveValidateTargetOuStep,
                    request.ComputerId,
                    null,
                    englishMessageOverride:
                        "The target OU must be within the configured computers search base.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                beforeContext: null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        AdComputerOuMoveContext? loadedBeforeContext = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context, cancellationToken);
            if (!TryLoadComputerOuMoveContext(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var beforeContext))
            {
                return await FailComputerOuMoveAsync(
        request,
                    auditAction,
                    "AD computer OU move failed.",
                    AdManagementApiMessageKeys.Computers.NotFound,
                    BuildComputerOuMoveFailureDiagnostic(
                        ComputerOuMoveLoadStep,
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
                return await FailComputerOuMoveAsync(
        request,
                    auditAction,
                    "AD computer OU move failed.",
                    AdComputerAccountGuard.ProtectedComputerWriteOperationMessage,
                    BuildComputerOuMoveFailureDiagnostic(
                        ComputerOuMoveValidateStep,
                        request.ComputerId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "This computer account cannot be moved.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    beforeContext,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            if (!TryLoadOrganizationalUnit(ldapConnection, targetOuDn))
            {
                return await FailComputerOuMoveAsync(
        request,
                    auditAction,
                    "AD computer OU move failed.",
                    AdManagementApiMessageKeys.Ldap.NoSuchObject,
                    BuildComputerOuMoveFailureDiagnostic(
                        ComputerOuMoveValidateTargetOuStep,
                        request.ComputerId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "The target organizational unit could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    beforeContext,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            if (AdLdapDnHelper.AreDistinguishedNamesEqual(beforeContext.ParentOuDistinguishedName, targetOuDn))
            {
                return await CompleteComputerOuMoveAsync(
                    request,
                    auditAction,
                    $"AD computer OU move skipped (no changes): {beforeContext.Name ?? beforeContext.SamAccountName}.",
                    AdManagementApiMessageKeys.Computers.AlreadyInTargetOu,
                    connection,
                    ldapConnection,
                    computersSearchBase,
                    beforeContext,
                    beforeContext with { ParentOuDistinguishedName = targetOuDn },
                    cancellationToken);
            }

            var currentRdn = AdLdapDnHelper.GetRelativeDistinguishedName(beforeContext.DistinguishedName);
            if (string.IsNullOrWhiteSpace(currentRdn))
            {
                return await FailComputerOuMoveAsync(
        request,
                    auditAction,
                    "AD computer OU move failed.",
                    AdManagementApiMessageKeys.Computers.OuMoveFailed,
                    BuildComputerOuMoveFailureDiagnostic(
                        ComputerOuMoveMoveStep,
                        request.ComputerId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "The computer distinguished name is not valid for Active Directory.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax),
                    beforeContext,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            var modifyDnRequest = new ModifyDNRequest(
                beforeContext.DistinguishedName,
                targetOuDn,
                currentRdn);
            ldapConnection.SendRequest(modifyDnRequest);

            if (!TryLoadComputerOuMoveContext(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var afterContext))
            {
                return await FailComputerOuMoveAsync(
        request,
                    auditAction,
                    "AD computer OU move failed.",
                    AdManagementApiMessageKeys.Computers.OuMoveFailed,
                    BuildComputerOuMoveFailureDiagnostic(
                        ComputerOuMoveReloadStep,
                        request.ComputerId,
                        beforeContext.DistinguishedName,
                        englishMessageOverride: "The AD computer OU move operation failed after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    beforeContext,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            if (!AdLdapDnHelper.AreDistinguishedNamesEqual(afterContext.ParentOuDistinguishedName, targetOuDn))
            {
                return await FailComputerOuMoveAsync(
        request,
                    auditAction,
                    "AD computer OU move failed.",
                    AdManagementApiMessageKeys.Computers.OuMoveFailed,
                    BuildComputerOuMoveFailureDiagnostic(
                        ComputerOuMoveReloadStep,
                        request.ComputerId,
                        afterContext.DistinguishedName,
                        englishMessageOverride: "The AD computer OU move could not be verified after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    beforeContext,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            return await CompleteComputerOuMoveAsync(
                request,
                auditAction,
                $"AD computer moved to OU. Computer: {afterContext.Name ?? afterContext.SamAccountName}.",
                AdManagementApiMessageKeys.Computers.OuMoveSuccess,
                connection,
                ldapConnection,
                computersSearchBase,
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
            return await FailComputerOuMoveAsync(
        request,
                auditAction,
                "AD computer OU move failed.",
                SanitizeComputerOuMoveLdapError(ex),
                AdOperationErrorDiagnosticBuilder.BuildComputerOuMoveFailureJson(
                    ComputerOuMoveMoveStep,
                    request.ComputerId,
                    loadedBeforeContext?.DistinguishedName,
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                loadedBeforeContext,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return await FailComputerOuMoveAsync(
        request,
                auditAction,
                "AD computer OU move failed.",
                AdManagementApiMessageKeys.Computers.OuMoveFailed,
                BuildComputerOuMoveFailureDiagnostic(
                    ComputerOuMoveMoveStep,
                    request.ComputerId,
                    loadedBeforeContext?.DistinguishedName,
                    englishMessageOverride: "The AD computer OU move operation failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                loadedBeforeContext,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private async Task<MoveAdComputerOuResult> CompleteComputerOuMoveAsync(
        MoveAdComputerOuRequest request,
        string auditAction,
        string auditDescription,
        string messageKey,
        AdManagementConnectionParameters connection,
        LdapConnection ldapConnection,
        string computersSearchBase,
        AdComputerOuMoveContext beforeContext,
        AdComputerOuMoveContext afterContext,
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

        await WriteComputerOuMoveSuccessLogsSafelyAsync(
            request,
            auditAction,
            auditDescription,
            connection,
            beforeContext,
            afterContext,
            cancellationToken);

        return new MoveAdComputerOuResult(true, messageKey, computerDetail);
    }

    private async Task<MoveAdComputerOuResult> FailComputerOuMoveAsync(
        MoveAdComputerOuRequest request,
        string auditAction,
        string auditDescription,
        string messageKey,
        string errorDiagnosticJson,
        AdComputerOuMoveContext? beforeContext,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteComputerOuMoveFailureLogsSafelyAsync(
        request,
            auditAction,
            auditDescription,
            beforeContext,
            errorDiagnosticJson,
            cancellationToken);

        return new MoveAdComputerOuResult(
            false,
            messageKey,
            FailureKind: failureKind,
            MessageParams: messageParams);
    }

    private static bool IsValidTargetOuDistinguishedName(string distinguishedName) =>
        !string.IsNullOrWhiteSpace(distinguishedName)
        && distinguishedName.Contains('=', StringComparison.Ordinal);

    private static bool TryLoadComputerOuMoveContext(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        out AdComputerOuMoveContext context)
    {
        context = null!;
        var filter = AdLdapComputerFilterHelper.BuildComputerObjectGuidFilter(objectGuid);
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            ComputerOuMoveAttributes)
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

        context = new AdComputerOuMoveContext(
            resolvedGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            name,
            GetFirstInt(entry, "userAccountControl"),
            GetFirstInt(entry, "primaryGroupID"),
            GetFirstBool(entry, "isCriticalSystemObject"),
            AdLdapDnHelper.GetParentDistinguishedName(distinguishedName));

        return true;
    }

    private async Task WriteComputerOuMoveOperationLogsAsync(
        MoveAdComputerOuRequest request,
        string status,
        AdManagementConnectionParameters? connection,
        AdComputerOuMoveContext? beforeContext,
        AdComputerOuMoveContext? afterContext,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var targetOuDn = request.TargetOuDistinguishedName.Trim();
        var requestSummary = AdOperationLogSnapshotBuilder.BuildComputerOuMoveRequestSummary(
            request.ComputerId,
            targetOuDn);

        var beforeSnapshot = beforeContext is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerOuMoveBeforeSnapshot(
                beforeContext.ComputerId,
                beforeContext.SamAccountName,
                beforeContext.Name,
                beforeContext.DistinguishedName,
                beforeContext.ParentOuDistinguishedName);

        var afterSnapshot = afterContext is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerOuMoveAfterSnapshot(
                afterContext.ComputerId,
                afterContext.SamAccountName,
                afterContext.Name,
                afterContext.DistinguishedName,
                afterContext.ParentOuDistinguishedName);

        var isSuccess = string.Equals(
            status,
            AdManagementOperationStatuses.Succeeded,
            StringComparison.Ordinal);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.ComputerMoveOu,
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

    private async Task WriteComputerOuMoveSuccessLogsSafelyAsync(
        MoveAdComputerOuRequest request,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdComputerOuMoveContext beforeContext,
        AdComputerOuMoveContext afterContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerOuMoveOperationLogsAsync(
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
                "{LogMessage} ComputerId={ComputerId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                ComputerOuMoveSuccessLoggingFailedMessage,
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
                ComputerOuMoveSuccessLoggingFailedMessage,
                afterContext.ComputerId,
                afterContext.SamAccountName,
                request.ActorUserId);
        }
    }

    private async Task WriteComputerOuMoveFailureLogsSafelyAsync(
        MoveAdComputerOuRequest request,
        string auditAction,
        string auditDescription,
        AdComputerOuMoveContext? beforeContext,
        string errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerOuMoveOperationLogsAsync(
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
                "{LogMessage} ComputerId={ComputerId} ActorUserId={ActorUserId}",
                ComputerOuMoveFailureLoggingFailedMessage,
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
                ComputerOuMoveFailureLoggingFailedMessage,
                request.ComputerId,
                request.ActorUserId);
        }
    }

    private static string BuildComputerOuMoveFailureDiagnostic(
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        string? normalizedReasonOverride = null) =>
        AdOperationErrorDiagnosticBuilder.BuildComputerOuMoveFailureJson(
            step,
            targetObjectGuid,
            targetDistinguishedName,
            englishMessageOverride: englishMessageOverride,
            normalizedReasonOverride: normalizedReasonOverride);

    private static string SanitizeComputerOuMoveLdapError(LdapException exception) =>
        string.IsNullOrWhiteSpace(exception.Message)
            || exception.Message.Contains("ldap", StringComparison.OrdinalIgnoreCase)
            ? AdManagementApiMessageKeys.Computers.OuMoveFailed
            : AdManagementApiMessageKeys.Computers.OuMoveFailed;

    private sealed record AdComputerOuMoveContext(
        string ComputerId,
        string DistinguishedName,
        string? SamAccountName,
        string? Name,
        int? UserAccountControl,
        int? PrimaryGroupId,
        bool? IsCriticalSystemObject,
        string? ParentOuDistinguishedName);
}
