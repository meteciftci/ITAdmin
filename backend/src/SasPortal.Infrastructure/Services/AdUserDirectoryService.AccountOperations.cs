using System.DirectoryServices.Protocols;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdUserAccountOperationService
{
    private const string AccountOperationFailedMessage = "Hesap işlemi başarısız oldu.";
    private const string AccountAlreadyEnabledMessage = "Kullanıcı hesabı zaten aktif.";
    private const string AccountAlreadyDisabledMessage = "Kullanıcı hesabı zaten pasif.";
    private const string AccountNotLockedMessage = "Kullanıcı hesabı kilitli değil.";
    private const string AccountEnabledMessage = "Kullanıcı hesabı aktife alındı.";
    private const string AccountDisabledMessage = "Kullanıcı hesabı pasife alındı.";
    private const string AccountUnlockedMessage = "Kullanıcı kilidi açıldı.";
    private const string AccountLoadUserStep = "LoadUser";
    private const string AccountModifyStep = "ModifyAccountControl";
    private const string AccountUnlockStep = "UnlockAccount";

    public Task<AdUserAccountOperationResult> EnableAsync(
        AdUserAccountOperationRequest request,
        CancellationToken cancellationToken = default) =>
        SetAccountDisabledAsync(request, disabled: false, cancellationToken);

    public Task<AdUserAccountOperationResult> DisableAsync(
        AdUserAccountOperationRequest request,
        CancellationToken cancellationToken = default) =>
        SetAccountDisabledAsync(request, disabled: true, cancellationToken);

    public Task<AdUserAccountOperationResult> UnlockAsync(
        AdUserAccountOperationRequest request,
        CancellationToken cancellationToken = default) =>
        UnlockAccountAsync(request, cancellationToken);

    private async Task<AdUserAccountOperationResult> SetAccountDisabledAsync(
        AdUserAccountOperationRequest request,
        bool disabled,
        CancellationToken cancellationToken)
    {
        var operationType = disabled
            ? AdManagementOperationTypes.UserDisable
            : AdManagementOperationTypes.UserEnable;
        var auditAction = disabled ? "Disable" : "Enable";
        var successMessage = disabled ? AccountDisabledMessage : AccountEnabledMessage;
        var alreadyMessage = disabled ? AccountAlreadyDisabledMessage : AccountAlreadyEnabledMessage;
        var auditDescriptionVerb = disabled ? "disabled" : "enabled";

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailAccountOperationAsync(
                request,
                operationType,
                auditAction,
                $"AD user account {auditDescriptionVerb} failed.",
                connectionResult.Message,
                connectionResult.Context?.Connection,
                beforeState: null,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var context = connectionResult.Context;
        var searchBase = ResolveDetailSearchBase(context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return await FailAccountOperationAsync(
                request,
                operationType,
                auditAction,
                $"AD user account {auditDescriptionVerb} failed.",
                AdManagementNotConfiguredMessage,
                context.Connection,
                beforeState: null,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        AdUserAccountState? loadedBeforeState = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            if (!TryLoadUserAccountState(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var beforeState))
            {
                return await FailAccountOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD user account {auditDescriptionVerb} failed.",
                    UserNotFoundMessage,
                    context.Connection,
                    beforeState: null,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            loadedBeforeState = beforeState;

            if (beforeState.IsEnabled != disabled)
            {
                return await CompleteAccountOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD user account {auditDescriptionVerb}. User: {beforeState.SamAccountName}.",
                    alreadyMessage,
                    context.Connection,
                    beforeState,
                    beforeState,
                    notificationEventKey: null,
                    cancellationToken);
            }

            var newUserAccountControl = AdLdapValueConverter.ApplyAccountDisabledFlag(
                beforeState.UserAccountControl,
                disabled);

            if (beforeState.UserAccountControl != newUserAccountControl)
            {
                ApplyUserAccountControl(ldapConnection, beforeState.DistinguishedName, newUserAccountControl);
            }

            if (!TryLoadUserAccountState(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var afterState))
            {
                return await FailAccountOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD user account {auditDescriptionVerb} failed.",
                    AccountOperationFailedMessage,
                    context.Connection,
                    loadedBeforeState,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            var notificationEventKey = disabled
                ? AdManagementNotificationEventKeys.UserDisabled
                : AdManagementNotificationEventKeys.UserEnabled;

            return await CompleteAccountOperationAsync(
                request,
                operationType,
                auditAction,
                $"AD user account {auditDescriptionVerb}. User: {afterState.SamAccountName}.",
                successMessage,
                context.Connection,
                beforeState,
                afterState,
                notificationEventKey,
                cancellationToken);
        }
        catch (LdapException ex)
        {
            return await FailAccountOperationAsync(
                request,
                operationType,
                auditAction,
                $"AD user account {auditDescriptionVerb} failed.",
                SanitizeLdapError(ex),
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken,
                ex);
        }
        catch (Exception)
        {
            return await FailAccountOperationAsync(
                request,
                operationType,
                auditAction,
                $"AD user account {auditDescriptionVerb} failed.",
                AccountOperationFailedMessage,
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private async Task<AdUserAccountOperationResult> UnlockAccountAsync(
        AdUserAccountOperationRequest request,
        CancellationToken cancellationToken)
    {
        const string operationType = AdManagementOperationTypes.UserUnlock;
        const string auditAction = "Unlock";

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailAccountOperationAsync(
                request,
                operationType,
                auditAction,
                "AD user account unlock failed.",
                connectionResult.Message,
                connectionResult.Context?.Connection,
                beforeState: null,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var context = connectionResult.Context;
        var searchBase = ResolveDetailSearchBase(context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return await FailAccountOperationAsync(
                request,
                operationType,
                auditAction,
                "AD user account unlock failed.",
                AdManagementNotConfiguredMessage,
                context.Connection,
                beforeState: null,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        AdUserAccountState? loadedBeforeState = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            if (!TryLoadUserAccountState(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var beforeState))
            {
                return await FailAccountOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    "AD user account unlock failed.",
                    UserNotFoundMessage,
                    context.Connection,
                    beforeState: null,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            loadedBeforeState = beforeState;

            if (!beforeState.IsLockedOut)
            {
                return await CompleteAccountOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD user account unlocked. User: {beforeState.SamAccountName}.",
                    AccountNotLockedMessage,
                    context.Connection,
                    beforeState,
                    beforeState,
                    notificationEventKey: null,
                    cancellationToken);
            }

            ApplyLockoutTime(ldapConnection, beforeState.DistinguishedName, 0);

            if (!TryLoadUserAccountState(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var afterState))
            {
                return await FailAccountOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    "AD user account unlock failed.",
                    AccountOperationFailedMessage,
                    context.Connection,
                    loadedBeforeState,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            return await CompleteAccountOperationAsync(
                request,
                operationType,
                auditAction,
                $"AD user account unlocked. User: {afterState.SamAccountName}.",
                AccountUnlockedMessage,
                context.Connection,
                beforeState,
                afterState,
                AdManagementNotificationEventKeys.UserUnlocked,
                cancellationToken);
        }
        catch (LdapException ex)
        {
            return await FailAccountOperationAsync(
                request,
                operationType,
                auditAction,
                "AD user account unlock failed.",
                SanitizeLdapError(ex),
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken,
                ex);
        }
        catch (Exception)
        {
            return await FailAccountOperationAsync(
                request,
                operationType,
                auditAction,
                "AD user account unlock failed.",
                AccountOperationFailedMessage,
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private static bool TryLoadUserAccountState(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        out AdUserAccountState state)
    {
        state = null!;
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
            "userAccountControl",
            "lockoutTime",
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

        var userAccountControl = GetFirstInt(entry, "userAccountControl");
        var lockoutTime = GetFirstLong(entry, "lockoutTime");

        state = new AdUserAccountState(
            resolvedGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            userAccountControl,
            AdLdapValueConverter.IsAccountEnabled(userAccountControl),
            AdLdapValueConverter.IsAccountLockedOut(lockoutTime),
            lockoutTime);

        return true;
    }

    private static void ApplyUserAccountControl(
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

    private static void ApplyLockoutTime(
        LdapConnection ldapConnection,
        string distinguishedName,
        long lockoutTime)
    {
        var modifyRequest = new ModifyRequest(
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            "lockoutTime",
            lockoutTime.ToString());

        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new LdapException((int)response.ResultCode);
        }
    }

    private async Task<AdUserAccountOperationResult> CompleteAccountOperationAsync(
        AdUserAccountOperationRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters connection,
        AdUserAccountState beforeState,
        AdUserAccountState afterState,
        string? notificationEventKey,
        CancellationToken cancellationToken)
    {
        AdManagementNotificationSummary? notificationSummary = null;
        if (!string.IsNullOrWhiteSpace(notificationEventKey))
        {
            notificationSummary = await TryEnqueueAccountOperationNotificationAsync(
                notificationEventKey,
                request,
                connection,
                afterState,
                cancellationToken);
        }

        await WriteAccountOperationLogsAsync(
            request,
            operationType,
            AdManagementOperationStatuses.Succeeded,
            connection,
            beforeState,
            afterState,
            null,
            notificationSummary,
            cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = auditAction,
                EntityName = "AdUser",
                EntityId = afterState.DistinguishedName,
                Description = auditDescription,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
            },
            cancellationToken);

        return new AdUserAccountOperationResult(
            true,
            message,
            afterState.UserId,
            afterState.SamAccountName,
            afterState.UserPrincipalName,
            afterState.DistinguishedName,
            afterState.IsEnabled,
            afterState.IsLockedOut);
    }

    private async Task<AdUserAccountOperationResult> FailAccountOperationAsync(
        AdUserAccountOperationRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters? connection,
        AdUserAccountState? beforeState,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        LdapException? ldapException = null)
    {
        var step = operationType == AdManagementOperationTypes.UserUnlock
            ? AccountUnlockStep
            : AccountModifyStep;
        if (beforeState is null && string.Equals(message, UserNotFoundMessage, StringComparison.Ordinal))
        {
            step = AccountLoadUserStep;
        }

        var diagnosticJson = ldapException is null
            ? BuildAccountFailureDiagnostic(
                operationType,
                step,
                request.UserId,
                beforeState?.DistinguishedName,
                ResolveAccountFailureReason(message),
                message)
            : AdOperationErrorDiagnosticBuilder.BuildAccountOperationFailureJson(
                operationType,
                step,
                request.UserId,
                beforeState?.DistinguishedName,
                ldapResultCode: ldapException.ErrorCode,
                ldapExceptionErrorCode: ldapException.ErrorCode,
                ldapDiagnosticMessage: ldapException.Message);

        await WriteAccountOperationLogsAsync(
            request,
            operationType,
            AdManagementOperationStatuses.Failed,
            connection,
            beforeState,
            afterState: beforeState,
            diagnosticJson,
            notificationSummary: null,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(auditDescription))
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = auditAction,
                    EntityName = "AdUser",
                    EntityId = request.UserId.ToString("D"),
                    Description = auditDescription,
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }

        return new AdUserAccountOperationResult(
            false,
            message,
            request.UserId.ToString("D"),
            FailureKind: failureKind);
    }

    private async Task WriteAccountOperationLogsAsync(
        AdUserAccountOperationRequest request,
        string operationType,
        string status,
        AdManagementConnectionParameters? connection,
        AdUserAccountState? beforeState,
        AdUserAccountState? afterState,
        string? errorDiagnosticJson,
        AdManagementNotificationSummary? notificationSummary,
        CancellationToken cancellationToken)
    {
        var requestedEnabled = operationType switch
        {
            AdManagementOperationTypes.UserEnable => true,
            AdManagementOperationTypes.UserDisable => false,
            _ => (bool?)null,
        };

        var requestSummary = AdOperationLogSnapshotBuilder.BuildAccountRequestSummary(
            operationType,
            request.UserId,
            requestedEnabled);

        var beforeSnapshot = beforeState is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildAccountBeforeSnapshot(
                operationType,
                beforeState.UserId,
                beforeState.SamAccountName,
                beforeState.UserPrincipalName,
                beforeState.DistinguishedName,
                beforeState.IsEnabled,
                beforeState.IsLockedOut,
                beforeState.UserAccountControl,
                beforeState.LockoutTime);

        var afterSnapshot = afterState is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildAccountAfterSnapshot(
                operationType,
                afterState.IsEnabled,
                afterState.IsLockedOut,
                afterState.UserAccountControl,
                afterState.LockoutTime);

        if (notificationSummary is not null && afterSnapshot is not null)
        {
            afterSnapshot = AppendNotificationSummary(afterSnapshot, notificationSummary);
        }

        var isSuccess = string.Equals(
            status,
            AdManagementOperationStatuses.Succeeded,
            StringComparison.Ordinal);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = operationType,
                Status = status,
                TargetObjectType = AdManagementTargetUserTypes.AdUser,
                TargetDistinguishedName = afterState?.DistinguishedName ?? beforeState?.DistinguishedName,
                TargetObjectGuid = afterState?.UserId ?? beforeState?.UserId ?? request.UserId.ToString("D"),
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

    private static string AppendNotificationSummary(
        string afterSnapshotJson,
        AdManagementNotificationSummary notificationSummary)
    {
        using var document = System.Text.Json.JsonDocument.Parse(afterSnapshotJson);
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            writer.WriteString(
                "notifications",
                FormatNotificationLogSummary(notificationSummary));
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string? ResolveAccountFailureReason(string message)
    {
        if (string.Equals(message, UserNotFoundMessage, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.NoSuchObject;
        }

        if (string.Equals(message, AdManagementNotConfiguredMessage, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.InvalidRequest;
        }

        if (string.Equals(message, AccountOperationFailedMessage, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.ConnectionFailed;
        }

        return null;
    }

    private static string BuildAccountFailureDiagnostic(
        string operationType,
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? normalizedReasonOverride,
        string userMessage) =>
        AdOperationErrorDiagnosticBuilder.BuildAccountOperationFailureJson(
            operationType,
            step,
            targetObjectGuid,
            targetDistinguishedName,
            englishMessageOverride: ResolveAccountFailureEnglishMessage(userMessage),
            normalizedReasonOverride: normalizedReasonOverride);

    private static string ResolveAccountFailureEnglishMessage(string userMessage) =>
        userMessage switch
        {
            var message when string.Equals(message, UserNotFoundMessage, StringComparison.Ordinal) =>
                "The AD user could not be found.",
            var message when string.Equals(message, AdManagementNotConfiguredMessage, StringComparison.Ordinal) =>
                "AD management is not configured.",
            var message when string.Equals(message, AccountOperationFailedMessage, StringComparison.Ordinal) =>
                "The AD account operation failed.",
            _ => "The AD account operation failed.",
        };

    private static string SanitizeLdapError(LdapException exception) =>
        string.IsNullOrWhiteSpace(exception.Message) || exception.Message.Contains("ldap", StringComparison.OrdinalIgnoreCase)
            ? AccountOperationFailedMessage
            : AccountOperationFailedMessage;

    private async Task<AdManagementNotificationSummary?> TryEnqueueAccountOperationNotificationAsync(
        string eventKey,
        AdUserAccountOperationRequest request,
        AdManagementConnectionParameters connection,
        AdUserAccountState afterState,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionResult = await ResolveConnectionAsync(cancellationToken);
            if (!connectionResult.IsSuccess || connectionResult.Context is null)
            {
                return null;
            }

            var mappings = await attributeMappingService.GetMappingsAsync(cancellationToken);
            var activeMappings = mappings.Where(static mapping => mapping.IsEnabled).ToList();
            var searchBase = ResolveDetailSearchBase(connectionResult.Context.Connection);
            if (string.IsNullOrWhiteSpace(searchBase))
            {
                return null;
            }

            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadUserNotificationContext(
                    ldapConnection,
                    searchBase,
                    Guid.Parse(afterState.UserId),
                    activeMappings,
                    out var userContext))
            {
                return null;
            }

            var contextWithActor = userContext with { ActorUserName = request.ActorUserName };

            return await notificationEnqueueService.EnqueueAccountOperationAsync(
                new AdManagementAccountOperationNotificationRequest(eventKey, contextWithActor),
                cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryLoadUserNotificationContext(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        IReadOnlyList<AdAttributeMappingItem> activeMappings,
        out AdManagementNotificationUserContext userContext)
    {
        userContext = null!;
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid);
        var filter =
            $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))(objectGUID={guidFilter}))";

        var ldapAttributes = AdLdapAttributeCatalog.BuildDetailLdapAttributeNames(activeMappings);
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            ldapAttributes)
        {
            SizeLimit = 2,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        if (!TryMapDetailItem(response.Entries[0], activeMappings, out var detail))
        {
            return false;
        }

        var attributeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mappedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapped in detail.MappedAttributes)
        {
            var value = mapped.Value?.FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item))?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            mappedValues[mapped.LogicalField] = value;
            if (!string.IsNullOrWhiteSpace(mapped.AdAttribute))
            {
                attributeValues[mapped.AdAttribute.Trim()] = value;
            }
        }

        if (!string.IsNullOrWhiteSpace(detail.Mail))
        {
            attributeValues["mail"] = detail.Mail;
        }

        userContext = new AdManagementNotificationUserContext(
            detail.Id,
            detail.SamAccountName,
            detail.UserPrincipalName,
            detail.DisplayName,
            detail.Mail,
            detail.Department,
            mappedValues,
            attributeValues,
            activeMappings,
            ActorUserName: null);

        return true;
    }

    private static string FormatNotificationLogSummary(AdManagementNotificationSummary summary) =>
        $"Notifications: {summary.QueuedCount} queued, {summary.SkippedCount} skipped.";

    private sealed record AdUserAccountState(
        string UserId,
        string DistinguishedName,
        string? SamAccountName,
        string? UserPrincipalName,
        int? UserAccountControl,
        bool IsEnabled,
        bool IsLockedOut,
        long? LockoutTime);
}
