using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUsersDirectoryService : IAdUserGroupMembershipService
{
    private const int GroupSearchDefaultLimit = 50;
    private const int GroupSearchMaxLimit = 50;
    private const int GroupMembershipServerLogDnMaxLength = 250;
    private const string GroupMembershipSuccessLoggingFailedMessage =
        "AD group membership operation succeeded but logging failed.";
    private const string GroupMembershipFailureLoggingFailedMessage =
        "AD group membership operation failed but logging failed.";
    private const string GroupMembershipValidateStep = "ValidateRequest";
    private const string GroupMembershipLoadUserStep = "LoadUser";
    private const string GroupMembershipLoadGroupStep = "LoadGroup";
    private const string GroupMembershipModifyStep = "ModifyGroupMembership";

    public Task<AdUserGroupMembershipResult> GetUserGroupsAsync(
        AdUserGroupMembershipRequest request,
        CancellationToken cancellationToken = default) =>
        LoadUserGroupsAsync(request, cancellationToken);

    public Task<AdGroupSearchResult> SearchGroupsAsync(
        AdGroupSearchRequest request,
        CancellationToken cancellationToken = default) =>
        SearchDirectoryGroupsAsync(request, cancellationToken);

    public Task<AdUserGroupOperationResult> AddUserToGroupAsync(
        AddAdUserToGroupRequest request,
        CancellationToken cancellationToken = default) =>
        ModifyGroupMembershipAsync(
            new GroupMembershipChangeRequest(
                request.UserId,
                request.GroupDistinguishedName,
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent),
            add: true,
            AdManagementOperationTypes.UserGroupAdd,
            "Add",
            cancellationToken);

    public Task<AdUserGroupOperationResult> RemoveUserFromGroupAsync(
        RemoveAdUserFromGroupRequest request,
        CancellationToken cancellationToken = default) =>
        ModifyGroupMembershipAsync(
            new GroupMembershipChangeRequest(
                request.UserId,
                request.GroupDistinguishedName,
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent),
            add: false,
            AdManagementOperationTypes.UserGroupRemove,
            "Remove",
            cancellationToken);

    private async Task<AdUserGroupMembershipResult> LoadUserGroupsAsync(
        AdUserGroupMembershipRequest request,
        CancellationToken cancellationToken)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdUserGroupMembershipResult(
                false,
                connectionResult.MessageKey,
                request.UserId.ToString("D"),
                null,
                null,
                null,
                null,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var searchBase = ResolveDetailSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdUserGroupMembershipResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                request.UserId.ToString("D"),
                null,
                null,
                null,
                null,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context, cancellationToken);
            if (!TryLoadUserGroupContext(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var userContext))
            {
                return new AdUserGroupMembershipResult(
                    false,
                    AdManagementApiMessageKeys.Users.NotFound,
                    request.UserId.ToString("D"),
                    null,
                    null,
                    null,
                    null,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            var groups = BuildDirectGroupMembershipItems(ldapConnection, userContext.MemberOfDns);
            return new AdUserGroupMembershipResult(
                true,
                string.Empty,
                userContext.UserId,
                userContext.DisplayName,
                userContext.SamAccountName,
                userContext.UserPrincipalName,
                userContext.DistinguishedName,
                groups);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException)
        {
            return ConnectionFailedGroupMembership(request.UserId);
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return ConnectionFailedGroupMembership(request.UserId);
        }
    }

    private async Task<AdGroupSearchResult> SearchDirectoryGroupsAsync(
        AdGroupSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!AdLdapAttributeCatalog.IsSearchTermValid(request.Query))
        {
            return new AdGroupSearchResult(true, string.Empty, []);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdGroupSearchResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var groupsSearchBase = ResolveGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdGroupSearchResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context, cancellationToken);
            var escaped = AdLdapFilterHelper.EscapeFilterValue(request.Query!.Trim());
            var filter =
                $"(&(objectCategory=group)(objectClass=group)(|(displayName=*{escaped}*)(cn=*{escaped}*)(name=*{escaped}*)(sAMAccountName=*{escaped}*)))";
            var searchRequest = new SearchRequest(
                groupsSearchBase,
                filter,
                SearchScope.Subtree,
                "distinguishedName",
                "displayName",
                "cn",
                "name",
                "sAMAccountName",
                "description")
            {
                SizeLimit = GroupSearchMaxLimit,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return GroupSearchConnectionFailed();
            }

            var items = new List<AdGroupSearchItem>(GroupSearchDefaultLimit);
            foreach (SearchResultEntry entry in response.Entries)
            {
                if (!TryMapGroupSearchItem(entry, out var item))
                {
                    continue;
                }

                items.Add(item);
                if (items.Count >= GroupSearchDefaultLimit)
                {
                    break;
                }
            }

            var sorted = items
                .OrderBy(static item => item, GroupSearchItemComparer.Instance)
                .ToList();

            return new AdGroupSearchResult(true, string.Empty, sorted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException)
        {
            return GroupSearchConnectionFailed();
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return GroupSearchConnectionFailed();
        }
    }

    private async Task<AdUserGroupOperationResult> ModifyGroupMembershipAsync(
        GroupMembershipChangeRequest request,
        bool add,
        string operationType,
        string auditAction,
        CancellationToken cancellationToken)
    {
        var groupDn = request.GroupDistinguishedName?.Trim();
        if (string.IsNullOrWhiteSpace(groupDn))
        {
            return await FailGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD user added to group failed." : "AD user removed from group failed.",
                AdManagementApiMessageKeys.Groups.GroupDnRequired,
                BuildGroupFailureDiagnostic(
                    operationType,
                    GroupMembershipValidateStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "The group distinguished name is invalid.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                null,
                null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD user added to group failed." : "AD user removed from group failed.",
                connectionResult.MessageKey,
                BuildGroupFailureDiagnostic(
                    operationType,
                    GroupMembershipValidateStep,
                    request.UserId,
                    groupDn,
                    englishMessageOverride: "The LDAP connection failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                null,
                null,
                groupDn,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var searchBase = ResolveDetailSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return await FailGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD user added to group failed." : "AD user removed from group failed.",
                AdManagementApiMessageKeys.Common.NotConfigured,
                BuildGroupFailureDiagnostic(
                    operationType,
                    GroupMembershipValidateStep,
                    request.UserId,
                    groupDn,
                    englishMessageOverride: "AD management is not configured.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                null,
                null,
                groupDn,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context, cancellationToken);
            if (!TryLoadUserGroupContext(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var userContext))
            {
                return await FailGroupOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD user added to group failed." : "AD user removed from group failed.",
                    AdManagementApiMessageKeys.Users.NotFound,
                    BuildGroupFailureDiagnostic(
                        operationType,
                        GroupMembershipLoadUserStep,
                        request.UserId,
                        null,
                        englishMessageOverride: "The AD user could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    null,
                    null,
                    groupDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            if (!TryLoadGroupByDn(ldapConnection, groupDn, out var groupInfo))
            {
                return await FailGroupOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD user added to group failed." : "AD user removed from group failed.",
                    AdManagementApiMessageKeys.Groups.NotFound,
                    BuildGroupFailureDiagnostic(
                        operationType,
                        GroupMembershipLoadGroupStep,
                        request.UserId,
                        userContext.DistinguishedName,
                        englishMessageOverride: "The AD group could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    userContext,
                    null,
                    groupDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            var isMember = IsDirectGroupMember(userContext, groupInfo);
            var beforeContext = userContext;

            if (add && isMember)
            {
                return await CompleteGroupOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD user added to group. User: {userContext.SamAccountName}. Group: {groupInfo.Name}.",
                    AdManagementApiMessageKeys.Users.AlreadyInGroup,
                    connectionResult.Context.Connection,
                    beforeContext,
                    userContext,
                    groupInfo,
                    cancellationToken);
            }

            if (!add && !isMember)
            {
                return await CompleteGroupOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD user removed from group. User: {userContext.SamAccountName}. Group: {groupInfo.Name}.",
                    AdManagementApiMessageKeys.Users.NotInGroup,
                    connectionResult.Context.Connection,
                    beforeContext,
                    userContext,
                    groupInfo,
                    cancellationToken);
            }

            var modifyOperation = add
                ? DirectoryAttributeOperation.Add
                : DirectoryAttributeOperation.Delete;
            var modifyRequest = new ModifyRequest(
                groupInfo.DistinguishedName,
                modifyOperation,
                "member",
                userContext.DistinguishedName);
            ldapConnection.SendRequest(modifyRequest);

            if (!TryLoadUserGroupContext(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var afterContext))
            {
                return await FailGroupOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD user added to group failed." : "AD user removed from group failed.",
                    AdManagementApiMessageKeys.Users.GroupOperationFailed,
                    BuildGroupFailureDiagnostic(
                        operationType,
                        GroupMembershipModifyStep,
                        request.UserId,
                        userContext.DistinguishedName,
                        englishMessageOverride: "The AD group membership operation failed after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    beforeContext,
                    groupInfo,
                    groupDn,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            var successMessage = add ? AdManagementApiMessageKeys.Users.GroupMembershipAdded : AdManagementApiMessageKeys.Users.GroupMembershipRemoved;
            var auditDescription = add
                ? $"AD user added to group. User: {userContext.SamAccountName}. Group: {groupInfo.Name}."
                : $"AD user removed from group. User: {userContext.SamAccountName}. Group: {groupInfo.Name}.";

            return await CompleteGroupOperationAsync(
                request,
                operationType,
                auditAction,
                auditDescription,
                successMessage,
                connectionResult.Context.Connection,
                beforeContext,
                afterContext,
                groupInfo,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException ex)
        {
            return await FailGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD user added to group failed." : "AD user removed from group failed.",
                SanitizeGroupLdapError(ex),
                BuildGroupFailureDiagnostic(
                    operationType,
                    GroupMembershipModifyStep,
                    request.UserId,
                    null,
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                null,
                null,
                groupDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return await FailGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD user added to group failed." : "AD user removed from group failed.",
                AdManagementApiMessageKeys.Users.GroupOperationFailed,
                BuildGroupFailureDiagnostic(
                    operationType,
                    GroupMembershipModifyStep,
                    request.UserId,
                    null,
                    englishMessageOverride: "The AD group membership operation failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                null,
                null,
                groupDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private async Task<AdUserGroupOperationResult> CompleteGroupOperationAsync(
        GroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters connection,
        AdUserGroupContext beforeContext,
        AdUserGroupContext afterContext,
        AdGroupDirectoryInfo groupInfo,
        CancellationToken cancellationToken)
    {
        await WriteGroupSuccessLogsSafelyAsync(
            request,
            operationType,
            auditAction,
            auditDescription,
            connection,
            beforeContext,
            afterContext,
            groupInfo,
            cancellationToken);

        return new AdUserGroupOperationResult(
            true,
            message,
            afterContext.UserId,
            groupInfo.DistinguishedName,
            groupInfo.Name);
    }

    private async Task<AdUserGroupOperationResult> FailGroupOperationAsync(
        GroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        string errorDiagnosticJson,
        AdUserGroupContext? beforeContext,
        AdGroupDirectoryInfo? groupInfo,
        string? groupDn,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        string? messageKey = null,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteGroupFailureLogsSafelyAsync(
        request,
            operationType,
            auditAction,
            auditDescription,
            beforeContext,
            groupInfo,
            errorDiagnosticJson,
            cancellationToken);

        return new AdUserGroupOperationResult(
            false,
            message,
            request.UserId.ToString("D"),
            groupInfo?.DistinguishedName ?? groupDn ?? request.GroupDistinguishedName,
            groupInfo?.Name,
            failureKind);
    }

    private async Task WriteGroupSuccessLogsSafelyAsync(
        GroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdUserGroupContext beforeContext,
        AdUserGroupContext afterContext,
        AdGroupDirectoryInfo groupInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupOperationLogsAsync(
                request,
                operationType,
                AdManagementOperationStatuses.Succeeded,
                connection,
                beforeContext,
                afterContext,
                groupInfo,
                errorDiagnosticJson: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogGroupMembershipLoggingFailure(
                ex,
                operationSucceeded: true,
                operationType,
                request,
                afterContext,
                groupInfo);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildGroupMembershipAuditRequest(
                    auditAction,
                    afterContext.UserId,
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogGroupMembershipLoggingFailure(
                ex,
                operationSucceeded: true,
                operationType,
                request,
                afterContext,
                groupInfo);
        }
    }

    private async Task WriteGroupFailureLogsSafelyAsync(
        GroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        AdUserGroupContext? beforeContext,
        AdGroupDirectoryInfo? groupInfo,
        string errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupOperationLogsAsync(
                request,
                operationType,
                AdManagementOperationStatuses.Failed,
                connection: null,
                beforeContext,
                afterContext: beforeContext,
                groupInfo,
                errorDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogGroupMembershipLoggingFailure(
                ex,
                operationSucceeded: false,
                operationType,
                request,
                beforeContext,
                groupInfo);
        }

        if (string.IsNullOrWhiteSpace(auditDescription))
        {
            return;
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildGroupMembershipAuditRequest(
                    auditAction,
                    beforeContext?.UserId ?? request.UserId.ToString("D"),
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogGroupMembershipLoggingFailure(
                ex,
                operationSucceeded: false,
                operationType,
                request,
                beforeContext,
                groupInfo);
        }
    }

    private static AuditLogWriteRequest BuildGroupMembershipAuditRequest(
        string auditAction,
        string entityId,
        string auditDescription,
        GroupMembershipChangeRequest request) =>
        new()
        {
            Action = auditAction,
            EntityName = "AdUserGroupMembership",
            EntityId = entityId,
            Description = auditDescription,
            ActorUserId = request.ActorUserId,
            ActorUserName = request.ActorUserName,
            IpAddress = request.ActorIpAddress,
            UserAgent = request.ActorUserAgent,
        };

    private void LogGroupMembershipLoggingFailure(
        Exception exception,
        bool operationSucceeded,
        string operationType,
        GroupMembershipChangeRequest request,
        AdUserGroupContext? userContext,
        AdGroupDirectoryInfo? groupInfo)
    {
        var logMessage = operationSucceeded
            ? GroupMembershipSuccessLoggingFailedMessage
            : GroupMembershipFailureLoggingFailedMessage;

        logger.LogError(
            exception,
            "{LogMessage} OperationType={OperationType} UserId={UserId} GroupName={GroupName} GroupDistinguishedName={GroupDistinguishedName} ActorUserId={ActorUserId}",
            logMessage,
            operationType,
            userContext?.UserId ?? request.UserId.ToString("D"),
            groupInfo?.Name,
            TruncateForServerLog(groupInfo?.DistinguishedName ?? request.GroupDistinguishedName),
            request.ActorUserId);
    }

    private static string? TruncateForServerLog(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= GroupMembershipServerLogDnMaxLength
                ? value.Trim()
                : value.Trim()[..GroupMembershipServerLogDnMaxLength];

    private async Task WriteGroupOperationLogsAsync(
        GroupMembershipChangeRequest request,
        string operationType,
        string status,
        AdManagementConnectionParameters? connection,
        AdUserGroupContext? beforeContext,
        AdUserGroupContext? afterContext,
        AdGroupDirectoryInfo? groupInfo,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var groupDistinguishedName = groupInfo?.DistinguishedName ?? request.GroupDistinguishedName;
        var requestSummary = AdOperationLogSnapshotBuilder.BuildGroupMembershipRequestSummary(
            operationType,
            request.UserId,
            groupDistinguishedName,
            groupInfo?.Name);

        var beforeSnapshot = beforeContext is null || groupInfo is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildGroupMembershipBeforeSnapshot(
                operationType,
                beforeContext.UserId,
                beforeContext.SamAccountName,
                beforeContext.UserPrincipalName,
                beforeContext.DistinguishedName,
                groupInfo.Name,
                groupInfo.DistinguishedName,
                IsDirectGroupMember(beforeContext, groupInfo));

        var afterSnapshot = afterContext is null || groupInfo is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildGroupMembershipAfterSnapshot(
                operationType,
                afterContext.UserId,
                afterContext.SamAccountName,
                afterContext.UserPrincipalName,
                afterContext.DistinguishedName,
                groupInfo.Name,
                groupInfo.DistinguishedName,
                IsDirectGroupMember(afterContext, groupInfo));

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
                TargetDistinguishedName = afterContext?.DistinguishedName ?? beforeContext?.DistinguishedName,
                TargetObjectGuid = afterContext?.UserId ?? beforeContext?.UserId ?? request.UserId.ToString("D"),
                TargetSamAccountName = afterContext?.SamAccountName ?? beforeContext?.SamAccountName,
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

    private static bool IsDirectGroupMember(AdUserGroupContext userContext, AdGroupDirectoryInfo groupInfo) =>
        userContext.MemberOfDns.Contains(groupInfo.DistinguishedName, StringComparer.OrdinalIgnoreCase);

    private static string BuildGroupFailureDiagnostic(
        string operationType,
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        string? normalizedReasonOverride = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        AdOperationErrorDiagnosticBuilder.BuildGroupMembershipFailureJson(
            operationType,
            step,
            targetObjectGuid,
            targetDistinguishedName,
            englishMessageOverride,
            ldapResultCode,
            ldapExceptionErrorCode,
            ldapDiagnosticMessage,
            normalizedReasonOverride);

    private static IReadOnlyList<AdUserGroupMembershipItem> BuildDirectGroupMembershipItems(
        LdapConnection ldapConnection,
        IReadOnlyCollection<string> memberOfDns)
    {
        var items = new List<AdUserGroupMembershipItem>(memberOfDns.Count);
        foreach (var groupDn in memberOfDns)
        {
            if (TryLoadGroupByDn(ldapConnection, groupDn, out var groupInfo))
            {
                items.Add(new AdUserGroupMembershipItem(
                    groupInfo.DistinguishedName,
                    groupInfo.DisplayName,
                    groupInfo.Name,
                    groupInfo.SamAccountName,
                    groupInfo.Description,
                    IsDirect: true));
                continue;
            }

            var fallbackName = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(groupDn) ?? groupDn;
            items.Add(new AdUserGroupMembershipItem(
                groupDn,
                null,
                fallbackName,
                null,
                null,
                IsDirect: true));
        }

        return items
            .OrderBy(static item => item, GroupMembershipItemComparer.Instance)
            .ToList();
    }

    private static bool TryLoadUserGroupContext(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        out AdUserGroupContext context)
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
            "displayName",
            "memberOf",
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

        var memberOf = GetAllStrings(entry, "memberOf");
        context = new AdUserGroupContext(
            resolvedGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            GetFirstString(entry, "displayName"),
            memberOf.ToHashSet(StringComparer.OrdinalIgnoreCase));

        return true;
    }

    private static bool TryMapGroupSearchItem(SearchResultEntry entry, out AdGroupSearchItem item)
    {
        item = null!;
        if (!TryMapGroupDirectoryInfo(entry, out var groupInfo))
        {
            return false;
        }

        item = new AdGroupSearchItem(
            groupInfo.DistinguishedName,
            groupInfo.DisplayName,
            groupInfo.Name,
            groupInfo.SamAccountName,
            groupInfo.Description);

        return true;
    }

    private static string ResolveGroupSortKey(string? displayName, string? samAccountName, string name)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(samAccountName))
        {
            return samAccountName.Trim();
        }

        return name;
    }

    private sealed class GroupMembershipItemComparer : IComparer<AdUserGroupMembershipItem>
    {
        public static GroupMembershipItemComparer Instance { get; } = new();

        public int Compare(AdUserGroupMembershipItem? left, AdUserGroupMembershipItem? right)
        {
            if (left is null || right is null)
            {
                return 0;
            }

            var comparison = string.Compare(
                ResolveGroupSortKey(left.DisplayName, left.SamAccountName, left.Name),
                ResolveGroupSortKey(right.DisplayName, right.SamAccountName, right.Name),
                StringComparison.OrdinalIgnoreCase);

            return comparison != 0
                ? comparison
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class GroupSearchItemComparer : IComparer<AdGroupSearchItem>
    {
        public static GroupSearchItemComparer Instance { get; } = new();

        public int Compare(AdGroupSearchItem? left, AdGroupSearchItem? right)
        {
            if (left is null || right is null)
            {
                return 0;
            }

            var comparison = string.Compare(
                ResolveGroupSortKey(left.DisplayName, left.SamAccountName, left.Name),
                ResolveGroupSortKey(right.DisplayName, right.SamAccountName, right.Name),
                StringComparison.OrdinalIgnoreCase);

            return comparison != 0
                ? comparison
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string? ResolveGroupsSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.GroupsSearchBase)
            ? ResolveDetailSearchBase(connection)
            : connection.GroupsSearchBase;

    private static string SanitizeGroupLdapError(LdapException exception) =>
        string.IsNullOrWhiteSpace(exception.Message)
            || exception.Message.Contains("ldap", StringComparison.OrdinalIgnoreCase)
            ? AdManagementApiMessageKeys.Users.GroupOperationFailed
            : AdManagementApiMessageKeys.Users.GroupOperationFailed;

    private static AdUserGroupMembershipResult ConnectionFailedGroupMembership(Guid userId) =>
        new(
            false,
            AdManagementApiMessageKeys.Users.QueryFailed,
            userId.ToString("D"),
            null,
            null,
            null,
            null,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdGroupSearchResult GroupSearchConnectionFailed() =>
        new(false, AdManagementApiMessageKeys.Groups.QueryFailed, null, AdDirectoryFailureKind.ConnectionFailed);

    private sealed record AdUserGroupContext(
        string UserId,
        string DistinguishedName,
        string? SamAccountName,
        string? UserPrincipalName,
        string? DisplayName,
        HashSet<string> MemberOfDns);

    private sealed record GroupMembershipChangeRequest(
        Guid UserId,
        string GroupDistinguishedName,
        Guid? ActorUserId,
        string? ActorUserName,
        string? ActorIpAddress,
        string? ActorUserAgent);
}
