using System.DirectoryServices.Protocols;
using System.Text.Json;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdUserGroupMembershipService
{
    private const string GroupOperationFailedMessage = "Grup üyeliği işlemi başarısız oldu.";
    private const string GroupSearchFailedMessage = "AD grupları okunamadı.";
    private const string InvalidGroupDnMessage = "Geçersiz grup kimliği.";
    private const string GroupNotFoundMessage = "AD grubu bulunamadı.";
    private const string UserAlreadyInGroupMessage = "Kullanıcı bu gruba zaten üye.";
    private const string UserNotInGroupMessage = "Kullanıcı bu grupta değil.";
    private const string GroupMembershipAddedMessage = "Grup üyeliği eklendi.";
    private const string GroupMembershipRemovedMessage = "Grup üyeliği kaldırıldı.";
    private const int GroupSearchDefaultLimit = 50;
    private const int GroupSearchMaxLimit = 50;

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
                connectionResult.Message,
                request.UserId.ToString("D"),
                null,
                null,
                null,
                null,
                null,
                connectionResult.FailureKind);
        }

        var searchBase = ResolveDetailSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdUserGroupMembershipResult(
                false,
                AdManagementNotConfiguredMessage,
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
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadUserGroupContext(
                    ldapConnection,
                    searchBase,
                    request.UserId,
                    out var userContext))
            {
                return new AdUserGroupMembershipResult(
                    false,
                    UserNotFoundMessage,
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
        catch (LdapException)
        {
            return ConnectionFailedGroupMembership(request.UserId);
        }
        catch (Exception)
        {
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
                connectionResult.Message,
                null,
                connectionResult.FailureKind);
        }

        var groupsSearchBase = ResolveGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdGroupSearchResult(
                false,
                AdManagementNotConfiguredMessage,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
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
        catch (LdapException)
        {
            return GroupSearchConnectionFailed();
        }
        catch (Exception)
        {
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
                InvalidGroupDnMessage,
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
                connectionResult.Message,
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
                AdManagementNotConfiguredMessage,
                null,
                null,
                groupDn,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
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
                    UserNotFoundMessage,
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
                    GroupNotFoundMessage,
                    userContext,
                    null,
                    groupDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            var isMember = userContext.MemberOfDns.Contains(
                groupInfo.DistinguishedName,
                StringComparer.OrdinalIgnoreCase);

            if (add && isMember)
            {
                return await CompleteGroupOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD user added to group. User: {userContext.SamAccountName}. Group: {groupInfo.Name}.",
                    UserAlreadyInGroupMessage,
                    connectionResult.Context.Connection,
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
                    UserNotInGroupMessage,
                    connectionResult.Context.Connection,
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
                    GroupOperationFailedMessage,
                    userContext,
                    groupInfo,
                    groupDn,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            var successMessage = add ? GroupMembershipAddedMessage : GroupMembershipRemovedMessage;
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
                afterContext,
                groupInfo,
                cancellationToken);
        }
        catch (LdapException ex)
        {
            return await FailGroupOperationAsync(
                request,
                operationType,
                auditAction,
                add ? "AD user added to group failed." : "AD user removed from group failed.",
                SanitizeGroupLdapError(ex),
                null,
                null,
                groupDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
        catch (Exception)
        {
            return await FailGroupOperationAsync(
                request,
                operationType,
                auditAction,
                add ? "AD user added to group failed." : "AD user removed from group failed.",
                GroupOperationFailedMessage,
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
        AdUserGroupContext userContext,
        AdGroupDirectoryInfo groupInfo,
        CancellationToken cancellationToken)
    {
        await WriteGroupOperationLogsAsync(
            request,
            operationType,
            AdManagementOperationStatuses.Succeeded,
            connection,
            userContext,
            groupInfo,
            null,
            cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = auditAction,
                EntityName = "AdUserGroupMembership",
                EntityId = userContext.DistinguishedName,
                Description = auditDescription,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
            },
            cancellationToken);

        return new AdUserGroupOperationResult(
            true,
            message,
            userContext.UserId,
            groupInfo.DistinguishedName,
            groupInfo.Name);
    }

    private async Task<AdUserGroupOperationResult> FailGroupOperationAsync(
        GroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        AdUserGroupContext? userContext,
        AdGroupDirectoryInfo? groupInfo,
        string? groupDn,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken)
    {
        await WriteGroupOperationLogsAsync(
            request,
            operationType,
            AdManagementOperationStatuses.Failed,
            null,
            userContext,
            groupInfo,
            message,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(auditDescription))
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = auditAction,
                    EntityName = "AdUserGroupMembership",
                    EntityId = userContext?.DistinguishedName ?? request.UserId.ToString("D"),
                    Description = auditDescription,
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }

        return new AdUserGroupOperationResult(
            false,
            message,
            request.UserId.ToString("D"),
            groupInfo?.DistinguishedName ?? groupDn ?? request.GroupDistinguishedName,
            groupInfo?.Name,
            failureKind);
    }

    private async Task WriteGroupOperationLogsAsync(
        GroupMembershipChangeRequest request,
        string operationType,
        string status,
        AdManagementConnectionParameters? connection,
        AdUserGroupContext? userContext,
        AdGroupDirectoryInfo? groupInfo,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var requestSummary = JsonSerializer.Serialize(new
        {
            userId = request.UserId,
            groupDistinguishedName = groupInfo?.DistinguishedName ?? request.GroupDistinguishedName,
            groupName = groupInfo?.Name,
        });

        var snapshot = userContext is null
            ? null
            : JsonSerializer.Serialize(new
            {
                userId = userContext.UserId,
                samAccountName = userContext.SamAccountName,
                userPrincipalName = userContext.UserPrincipalName,
                distinguishedName = userContext.DistinguishedName,
                groupDistinguishedName = groupInfo?.DistinguishedName,
                groupName = groupInfo?.Name,
            });

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = operationType,
                Status = status,
                TargetObjectType = AdManagementTargetUserTypes.AdUser,
                TargetDistinguishedName = userContext?.DistinguishedName,
                TargetObjectGuid = userContext?.UserId ?? request.UserId.ToString("D"),
                TargetSamAccountName = userContext?.SamAccountName,
                ErrorMessage = errorMessage,
                RequestSummaryJson = requestSummary,
                AfterSnapshotJson = snapshot,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = connection is null ? null : ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

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

    private static bool TryLoadGroupByDn(
        LdapConnection ldapConnection,
        string groupDistinguishedName,
        out AdGroupDirectoryInfo groupInfo)
    {
        groupInfo = null!;
        if (string.IsNullOrWhiteSpace(groupDistinguishedName))
        {
            return false;
        }

        var searchRequest = new SearchRequest(
            groupDistinguishedName.Trim(),
            "(objectClass=group)",
            SearchScope.Base,
            "distinguishedName",
            "displayName",
            "cn",
            "name",
            "sAMAccountName",
            "description")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        SearchResponse response;
        try
        {
            response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        }
        catch (LdapException)
        {
            return false;
        }

        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        return TryMapGroupDirectoryInfo(response.Entries[0], out groupInfo);
    }

    private static bool TryMapGroupDirectoryInfo(
        SearchResultEntry entry,
        out AdGroupDirectoryInfo groupInfo)
    {
        groupInfo = null!;
        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var name = GetFirstString(entry, "cn")
            ?? GetFirstString(entry, "name")
            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;

        groupInfo = new AdGroupDirectoryInfo(
            distinguishedName,
            GetFirstString(entry, "displayName"),
            name,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "description"));

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
            ? GroupOperationFailedMessage
            : GroupOperationFailedMessage;

    private static AdUserGroupMembershipResult ConnectionFailedGroupMembership(Guid userId) =>
        new(
            false,
            DirectoryQueryFailedMessage,
            userId.ToString("D"),
            null,
            null,
            null,
            null,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdGroupSearchResult GroupSearchConnectionFailed() =>
        new(false, GroupSearchFailedMessage, null, AdDirectoryFailureKind.ConnectionFailed);

    private sealed record AdUserGroupContext(
        string UserId,
        string DistinguishedName,
        string? SamAccountName,
        string? UserPrincipalName,
        string? DisplayName,
        HashSet<string> MemberOfDns);

    private sealed record AdGroupDirectoryInfo(
        string DistinguishedName,
        string? DisplayName,
        string Name,
        string? SamAccountName,
        string? Description);

    private sealed record GroupMembershipChangeRequest(
        Guid UserId,
        string GroupDistinguishedName,
        Guid? ActorUserId,
        string? ActorUserName,
        string? ActorIpAddress,
        string? ActorUserAgent);
}
