using System.DirectoryServices.Protocols;
using System.Text.Json;
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
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

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
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

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
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
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
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

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
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

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
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
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
            AdLdapValueConverter.IsAccountLockedOut(lockoutTime));

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
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken)
    {
        await WriteAccountOperationLogsAsync(
            request,
            operationType,
            AdManagementOperationStatuses.Failed,
            connection,
            null,
            null,
            message,
            null,
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
        string? errorMessage,
        AdManagementNotificationSummary? notificationSummary,
        CancellationToken cancellationToken)
    {
        var beforeSnapshot = beforeState is null
            ? null
            : JsonSerializer.Serialize(new
            {
                userId = beforeState.UserId,
                samAccountName = beforeState.SamAccountName,
                userPrincipalName = beforeState.UserPrincipalName,
                distinguishedName = beforeState.DistinguishedName,
                isEnabled = beforeState.IsEnabled,
                isLockedOut = beforeState.IsLockedOut,
                userAccountControl = beforeState.UserAccountControl,
            });

        var afterSnapshot = afterState is null
            ? null
            : JsonSerializer.Serialize(new
            {
                userId = afterState.UserId,
                samAccountName = afterState.SamAccountName,
                userPrincipalName = afterState.UserPrincipalName,
                distinguishedName = afterState.DistinguishedName,
                isEnabled = afterState.IsEnabled,
                isLockedOut = afterState.IsLockedOut,
                userAccountControl = afterState.UserAccountControl,
                notifications = notificationSummary is null
                    ? null
                    : FormatNotificationLogSummary(notificationSummary),
            });

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = operationType,
                Status = status,
                TargetObjectType = AdManagementTargetUserTypes.AdUser,
                TargetDistinguishedName = afterState?.DistinguishedName ?? beforeState?.DistinguishedName,
                TargetObjectGuid = afterState?.UserId ?? beforeState?.UserId ?? request.UserId.ToString("D"),
                TargetSamAccountName = afterState?.SamAccountName ?? beforeState?.SamAccountName,
                ErrorMessage = errorMessage,
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
        bool IsLockedOut);
}
