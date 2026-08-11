using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdComputersDirectoryService : IAdComputerAccountOperationService
{
    private const string ComputerLoadStep = "LoadComputer";
    private const string ComputerModifyStep = "ModifyAccountControl";
    private const string ComputerOperationSuccessLoggingFailedMessage =
        "AD computer account operation succeeded but logging failed.";
    private const string ComputerOperationFailureLoggingFailedMessage =
        "AD computer account operation failed but logging failed.";

    private static readonly string[] ComputerAccountOperationAttributes =
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

    public Task<AdComputerAccountOperationResult> EnableComputerAsync(
        AdComputerAccountOperationRequest request,
        CancellationToken cancellationToken = default) =>
        SetComputerAccountDisabledAsync(request, disabled: false, cancellationToken);

    public Task<AdComputerAccountOperationResult> DisableComputerAsync(
        AdComputerAccountOperationRequest request,
        CancellationToken cancellationToken = default) =>
        SetComputerAccountDisabledAsync(request, disabled: true, cancellationToken);

    private async Task<AdComputerAccountOperationResult> SetComputerAccountDisabledAsync(
        AdComputerAccountOperationRequest request,
        bool disabled,
        CancellationToken cancellationToken)
    {
        var operationType = disabled
            ? AdManagementOperationTypes.ComputerDisable
            : AdManagementOperationTypes.ComputerEnable;
        var auditAction = disabled ? "Disable" : "Enable";
        var successMessage = disabled ? AdManagementApiMessageKeys.Computers.AccountDisabled : AdManagementApiMessageKeys.Computers.AccountEnabled;
        var alreadyMessage = disabled ? AdManagementApiMessageKeys.Computers.AccountDisabled : AdManagementApiMessageKeys.Computers.AccountEnabled;
        var auditDescriptionVerb = disabled ? "disabled" : "enabled";

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailComputerAccountOperationAsync(
        request,
                operationType,
                auditAction,
                $"AD computer account {auditDescriptionVerb} failed.",
                connectionResult.MessageKey,
                connectionResult.Context?.Connection,
                beforeState: null,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var context = connectionResult.Context;
        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return await FailComputerAccountOperationAsync(
        request,
                operationType,
                auditAction,
                $"AD computer account {auditDescriptionVerb} failed.",
                AdManagementApiMessageKeys.Common.NotConfigured,
                context.Connection,
                beforeState: null,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        AdComputerAccountState? loadedBeforeState = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(context, cancellationToken);
            if (!TryLoadComputerAccountState(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var beforeState))
            {
                return await FailComputerAccountOperationAsync(
        request,
                    operationType,
                    auditAction,
                    $"AD computer account {auditDescriptionVerb} failed.",
                    AdManagementApiMessageKeys.Computers.NotFound,
                    context.Connection,
                    beforeState: null,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            loadedBeforeState = beforeState;

            if (beforeState.UserAccountControl is null)
            {
                return await FailComputerAccountOperationAsync(
        request,
                    operationType,
                    auditAction,
                    $"AD computer account {auditDescriptionVerb} failed.",
                    AdManagementApiMessageKeys.Computers.AccountOperationFailed,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            if (AdComputerAccountGuard.IsProtectedComputer(
                    beforeState.PrimaryGroupId,
                    beforeState.UserAccountControl,
                    beforeState.IsCriticalSystemObject))
            {
                return await FailComputerAccountOperationAsync(
        request,
                    operationType,
                    auditAction,
                    $"AD computer account {auditDescriptionVerb} failed.",
                    AdComputerAccountGuard.ProtectedComputerMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            if (beforeState.IsEnabled != disabled)
            {
                return await CompleteComputerAccountOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD computer account {auditDescriptionVerb}. Computer: {beforeState.Name ?? beforeState.SamAccountName}.",
                    alreadyMessage,
                    context.Connection,
                    ldapConnection,
                    computersSearchBase,
                    beforeState,
                    beforeState,
                    cancellationToken);
            }

            var newUserAccountControl = AdLdapValueConverter.ApplyAccountDisabledFlag(
                beforeState.UserAccountControl,
                disabled);

            if (beforeState.UserAccountControl != newUserAccountControl)
            {
                ApplyComputerUserAccountControl(
                    ldapConnection,
                    beforeState.DistinguishedName,
                    newUserAccountControl);
            }

            if (!TryLoadComputerAccountState(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var afterState))
            {
                return await FailComputerAccountOperationAsync(
        request,
                    operationType,
                    auditAction,
                    $"AD computer account {auditDescriptionVerb} failed.",
                    AdManagementApiMessageKeys.Computers.AccountOperationFailed,
                    context.Connection,
                    loadedBeforeState,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            return await CompleteComputerAccountOperationAsync(
                request,
                operationType,
                auditAction,
                $"AD computer account {auditDescriptionVerb}. Computer: {afterState.Name ?? afterState.SamAccountName}.",
                successMessage,
                context.Connection,
                ldapConnection,
                computersSearchBase,
                beforeState,
                afterState,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException ex)
        {
            return await FailComputerAccountOperationAsync(
        request,
                operationType,
                auditAction,
                $"AD computer account {auditDescriptionVerb} failed.",
                SanitizeComputerLdapError(ex),
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken,
                ex);
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return await FailComputerAccountOperationAsync(
        request,
                operationType,
                auditAction,
                $"AD computer account {auditDescriptionVerb} failed.",
                AdManagementApiMessageKeys.Computers.AccountOperationFailed,
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private static bool TryLoadComputerAccountState(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        out AdComputerAccountState state)
    {
        state = null!;
        var filter = AdLdapComputerFilterHelper.BuildComputerObjectGuidFilter(objectGuid);
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            ComputerAccountOperationAttributes)
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

        var userAccountControl = GetFirstInt(entry, "userAccountControl");
        var cn = GetFirstString(entry, "cn");
        var name = GetFirstString(entry, "name")
            ?? cn
            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;

        state = new AdComputerAccountState(
            resolvedGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            name,
            userAccountControl,
            AdLdapValueConverter.IsAccountEnabled(userAccountControl),
            GetFirstInt(entry, "primaryGroupID"),
            GetFirstBool(entry, "isCriticalSystemObject"));

        return true;
    }

    private static void ApplyComputerUserAccountControl(
        LdapConnection ldapConnection,
        string distinguishedName,
        int userAccountControl)
    {
        var modifyRequest = new ModifyRequest(
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            "userAccountControl",
            userAccountControl.ToString());

        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new LdapException((int)response.ResultCode);
        }
    }

    private async Task<AdComputerAccountOperationResult> CompleteComputerAccountOperationAsync(
        AdComputerAccountOperationRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters connection,
        LdapConnection ldapConnection,
        string computersSearchBase,
        AdComputerAccountState beforeState,
        AdComputerAccountState afterState,
        CancellationToken cancellationToken)
    {
        AdComputerDetail? computerDetail = null;
        if (TryLoadComputerDetail(
                ldapConnection,
                computersSearchBase,
                Guid.Parse(afterState.ComputerId),
                out var detail))
        {
            computerDetail = detail;
        }

        await WriteComputerAccountSuccessLogsSafelyAsync(
            request,
            operationType,
            auditAction,
            auditDescription,
            connection,
            beforeState,
            afterState,
            cancellationToken);

        return new AdComputerAccountOperationResult(
            true,
            message,
            computerDetail);
    }

    private async Task<AdComputerAccountOperationResult> FailComputerAccountOperationAsync(
        AdComputerAccountOperationRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters? connection,
        AdComputerAccountState? beforeState,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        LdapException? ldapException = null,
        string? messageKey = null,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        var step = beforeState is null && string.Equals(message, AdManagementApiMessageKeys.Computers.NotFound, StringComparison.Ordinal)
            ? ComputerLoadStep
            : ComputerModifyStep;

        var diagnosticJson = ldapException is null
            ? AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                operationType,
                step,
                request.ComputerId,
                beforeState?.DistinguishedName,
                englishMessageOverride: ResolveComputerFailureEnglishMessage(message),
                normalizedReasonOverride: ResolveComputerFailureReason(message))
            : AdOperationErrorDiagnosticBuilder.BuildComputerAccountOperationFailureJson(
                operationType,
                step,
                request.ComputerId,
                beforeState?.DistinguishedName,
                ldapResultCode: ldapException.ErrorCode,
                ldapExceptionErrorCode: ldapException.ErrorCode,
                ldapDiagnosticMessage: ldapException.Message);

        await WriteComputerAccountFailureLogsSafelyAsync(
        request,
            operationType,
            auditAction,
            auditDescription,
            connection,
            beforeState,
            diagnosticJson,
            cancellationToken);

        return new AdComputerAccountOperationResult(
            false,
            message,
            FailureKind: failureKind,
            MessageParams: messageParams);
    }

    private bool TryLoadComputerDetail(
        LdapConnection ldapConnection,
        string computersSearchBase,
        Guid id,
        out AdComputerDetail detail)
    {
        detail = null!;
        var filter = AdLdapComputerFilterHelper.BuildComputerObjectGuidFilter(id);
        var searchRequest = new SearchRequest(
            computersSearchBase,
            filter,
            SearchScope.Subtree,
            ComputerDetailAttributes)
        {
            SizeLimit = 2,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        if (!TryMapComputerDetail(ldapConnection, response.Entries[0], out detail))
        {
            return false;
        }

        detail = TryEnrichComputerDetailWithResolvedManagedBy(ldapConnection, detail);
        return true;
    }

    private async Task WriteComputerAccountOperationLogsAsync(
        AdComputerAccountOperationRequest request,
        string operationType,
        string status,
        AdManagementConnectionParameters? connection,
        AdComputerAccountState? beforeState,
        AdComputerAccountState? afterState,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var requestedEnabled = operationType switch
        {
            AdManagementOperationTypes.ComputerEnable => true,
            AdManagementOperationTypes.ComputerDisable => false,
            _ => (bool?)null,
        };

        var requestSummary = AdOperationLogSnapshotBuilder.BuildComputerAccountRequestSummary(
            operationType,
            request.ComputerId,
            requestedEnabled);

        var beforeSnapshot = beforeState is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerAccountBeforeSnapshot(
                operationType,
                beforeState.ComputerId,
                beforeState.SamAccountName,
                beforeState.Name,
                beforeState.DistinguishedName,
                beforeState.IsEnabled,
                beforeState.UserAccountControl,
                beforeState.PrimaryGroupId);

        var afterSnapshot = afterState is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerAccountAfterSnapshot(
                operationType,
                afterState.ComputerId,
                afterState.SamAccountName,
                afterState.Name,
                afterState.DistinguishedName,
                afterState.IsEnabled,
                afterState.UserAccountControl,
                afterState.PrimaryGroupId);

        var isSuccess = string.Equals(
            status,
            AdManagementOperationStatuses.Succeeded,
            StringComparison.Ordinal);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = operationType,
                Status = status,
                TargetObjectType = AdManagementTargetComputerTypes.AdComputer,
                TargetDistinguishedName = afterState?.DistinguishedName ?? beforeState?.DistinguishedName,
                TargetObjectGuid = afterState?.ComputerId ?? beforeState?.ComputerId ?? request.ComputerId.ToString("D"),
                TargetSamAccountName = afterState?.SamAccountName ?? beforeState?.SamAccountName,
                ErrorCode = isSuccess
                    ? null
                    : AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(errorDiagnosticJson),
                ErrorMessage = isSuccess ? null : errorDiagnosticJson,
                RequestSummaryJson = requestSummary,
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

    private async Task WriteComputerAccountSuccessLogsSafelyAsync(
        AdComputerAccountOperationRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdComputerAccountState beforeState,
        AdComputerAccountState afterState,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerAccountOperationLogsAsync(
                request,
                operationType,
                AdManagementOperationStatuses.Succeeded,
                connection,
                beforeState,
                afterState,
                errorDiagnosticJson: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogComputerAccountOperationLoggingFailure(
                ex,
                operationSucceeded: true,
                operationType,
                request,
                afterState);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = auditAction,
                    EntityName = "AdComputer",
                    EntityId = afterState.ComputerId,
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
            LogComputerAccountOperationLoggingFailure(
                ex,
                operationSucceeded: true,
                operationType,
                request,
                afterState);
        }
    }

    private async Task WriteComputerAccountFailureLogsSafelyAsync(
        AdComputerAccountOperationRequest request,
        string operationType,
        string auditAction,
        string? auditDescription,
        AdManagementConnectionParameters? connection,
        AdComputerAccountState? beforeState,
        string diagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerAccountOperationLogsAsync(
                request,
                operationType,
                AdManagementOperationStatuses.Failed,
                connection,
                beforeState,
                afterState: beforeState,
                diagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogComputerAccountOperationLoggingFailure(
                ex,
                operationSucceeded: false,
                operationType,
                request,
                beforeState);
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
            LogComputerAccountOperationLoggingFailure(
                ex,
                operationSucceeded: false,
                operationType,
                request,
                beforeState);
        }
    }

    private void LogComputerAccountOperationLoggingFailure(
        Exception exception,
        bool operationSucceeded,
        string operationType,
        AdComputerAccountOperationRequest request,
        AdComputerAccountState? accountState)
    {
        var logMessage = operationSucceeded
            ? ComputerOperationSuccessLoggingFailedMessage
            : ComputerOperationFailureLoggingFailedMessage;

        logger.LogError(
            exception,
            "{LogMessage} OperationType={OperationType} ComputerId={ComputerId} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
            logMessage,
            operationType,
            accountState?.ComputerId ?? request.ComputerId.ToString("D"),
            accountState?.SamAccountName,
            request.ActorUserId);
    }

    private static string? ResolveComputerFailureReason(string message)
    {
        if (string.Equals(message, AdManagementApiMessageKeys.Computers.NotFound, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.NoSuchObject;
        }

        if (string.Equals(message, AdManagementApiMessageKeys.Common.NotConfigured, StringComparison.Ordinal)
            || string.Equals(message, AdComputerAccountGuard.ProtectedComputerMessage, StringComparison.Ordinal)
            || string.Equals(message, AdManagementApiMessageKeys.Computers.AccountOperationFailed, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.InvalidRequest;
        }

        if (string.Equals(message, AdManagementApiMessageKeys.Computers.AccountOperationFailed, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.ConnectionFailed;
        }

        return null;
    }

    private static string ResolveComputerFailureEnglishMessage(string userMessage) =>
        userMessage switch
        {
            var message when string.Equals(message, AdManagementApiMessageKeys.Computers.NotFound, StringComparison.Ordinal) =>
                "The AD computer could not be found.",
            var message when string.Equals(message, AdManagementApiMessageKeys.Common.NotConfigured, StringComparison.Ordinal) =>
                "AD management is not configured.",
            var message when string.Equals(message, AdComputerAccountGuard.ProtectedComputerMessage, StringComparison.Ordinal) =>
                "This computer account cannot be enabled or disabled.",
            var message when string.Equals(message, AdManagementApiMessageKeys.Computers.AccountOperationFailed, StringComparison.Ordinal) =>
                "The computer account userAccountControl value could not be read.",
            var message when string.Equals(message, AdManagementApiMessageKeys.Computers.AccountOperationFailed, StringComparison.Ordinal) =>
                "The AD computer account operation failed.",
            _ => "The AD computer account operation failed.",
        };

    private static string SanitizeComputerLdapError(LdapException exception) =>
        string.IsNullOrWhiteSpace(exception.Message) || exception.Message.Contains("ldap", StringComparison.OrdinalIgnoreCase)
            ? AdManagementApiMessageKeys.Computers.AccountOperationFailed
            : AdManagementApiMessageKeys.Computers.AccountOperationFailed;

    private static bool? GetFirstBool(SearchResultEntry entry, string attributeName)
    {
        if (!entry.Attributes.Contains(attributeName))
        {
            return null;
        }

        var attribute = entry.Attributes[attributeName];
        if (attribute is null || attribute.Count == 0)
        {
            return null;
        }

        var raw = attribute[0];
        return raw switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            _ => null,
        };
    }

    private sealed record AdComputerAccountState(
        string ComputerId,
        string DistinguishedName,
        string? SamAccountName,
        string? Name,
        int? UserAccountControl,
        bool IsEnabled,
        int? PrimaryGroupId,
        bool? IsCriticalSystemObject);
}
