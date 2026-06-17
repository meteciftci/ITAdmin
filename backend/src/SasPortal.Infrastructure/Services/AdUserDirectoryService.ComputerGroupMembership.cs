using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdComputerGroupMembershipService
{
    private const int ComputerGroupSearchDefaultLimit = 50;
    private const int ComputerGroupSearchMaxLimit = 50;
    private const int ComputerGroupMembershipServerLogDnMaxLength = 250;
    private const string ComputerGroupMembershipSuccessLoggingFailedMessage =
        "AD computer group membership operation succeeded but logging failed.";
    private const string ComputerGroupMembershipFailureLoggingFailedMessage =
        "AD computer group membership operation failed but logging failed.";
    private const string ComputerGroupMembershipValidateStep = "ValidateRequest";
    private const string ComputerGroupMembershipLoadComputerStep = "LoadComputer";
    private const string ComputerGroupMembershipLoadGroupStep = "LoadGroup";
    private const string ComputerGroupMembershipModifyStep = "ModifyGroupMembership";

    private static readonly string[] ComputerGroupContextAttributes =
    [
        "objectGUID",
        "distinguishedName",
        "sAMAccountName",
        "name",
        "cn",
        "dNSHostName",
        "memberOf",
        "primaryGroupID",
        "userAccountControl",
        "isCriticalSystemObject",
    ];

    public Task<AdComputerGroupMembershipResult> GetComputerGroupsAsync(
        AdComputerGroupMembershipRequest request,
        CancellationToken cancellationToken = default) =>
        LoadComputerGroupsAsync(request, cancellationToken);

    public Task<AdComputerGroupSearchResult> SearchGroupCandidatesAsync(
        AdComputerGroupSearchRequest request,
        CancellationToken cancellationToken = default) =>
        SearchComputerGroupCandidatesAsync(request, cancellationToken);

    public Task<AdComputerGroupOperationResult> AddComputerToGroupAsync(
        AddAdComputerToGroupRequest request,
        CancellationToken cancellationToken = default) =>
        ModifyComputerGroupMembershipAsync(
            new ComputerGroupMembershipChangeRequest(
                request.ComputerId,
                request.GroupDistinguishedName,
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent),
            add: true,
            AdManagementOperationTypes.ComputerGroupAdd,
            "Add",
            cancellationToken);

    public Task<AdComputerGroupOperationResult> RemoveComputerFromGroupAsync(
        RemoveAdComputerFromGroupRequest request,
        CancellationToken cancellationToken = default) =>
        ModifyComputerGroupMembershipAsync(
            new ComputerGroupMembershipChangeRequest(
                request.ComputerId,
                request.GroupDistinguishedName,
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent),
            add: false,
            AdManagementOperationTypes.ComputerGroupRemove,
            "Remove",
            cancellationToken);

    private async Task<AdComputerGroupMembershipResult> LoadComputerGroupsAsync(
        AdComputerGroupMembershipRequest request,
        CancellationToken cancellationToken)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return ConnectionFailedComputerGroupMembership(request.ComputerId, connectionResult.MessageKey, connectionResult.FailureKind, connectionResult.MessageParams);
        }

        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return ConnectionFailedComputerGroupMembership(
                request.ComputerId,
                AdManagementApiMessageKeys.Common.NotConfigured,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadComputerGroupContext(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var computerContext))
            {
                return new AdComputerGroupMembershipResult(
                    false,
                    AdManagementApiMessageKeys.Computers.NotFound,
                    request.ComputerId.ToString("D"),
                    null,
                    null,
                    null,
                    null,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            var groups = BuildComputerDirectGroupMembershipItems(ldapConnection, computerContext.MemberOfDns);
            return new AdComputerGroupMembershipResult(
                true,
                string.Empty,
                computerContext.ComputerId,
                computerContext.Name,
                computerContext.SamAccountName,
                computerContext.DnsHostName,
                computerContext.DistinguishedName,
                groups);
        }
        catch (LdapException)
        {
            return ConnectionFailedComputerGroupMembership(
                request.ComputerId,
                AdManagementApiMessageKeys.Groups.QueryFailed,
                AdDirectoryFailureKind.ConnectionFailed);
        }
        catch (Exception)
        {
            return ConnectionFailedComputerGroupMembership(
                request.ComputerId,
                AdManagementApiMessageKeys.Groups.QueryFailed,
                AdDirectoryFailureKind.ConnectionFailed);
        }
    }

    private async Task<AdComputerGroupSearchResult> SearchComputerGroupCandidatesAsync(
        AdComputerGroupSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!AdLdapAttributeCatalog.IsSearchTermValid(request.Query))
        {
            return new AdComputerGroupSearchResult(true, string.Empty, []);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdComputerGroupSearchResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdComputerGroupSearchResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return new AdComputerGroupSearchResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            HashSet<string> existingMemberDns = new(StringComparer.OrdinalIgnoreCase);
            if (TryLoadComputerGroupContext(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var computerContext))
            {
                existingMemberDns = computerContext.MemberOfDns;
            }

            var filter = AdLdapGroupFilterHelper.BuildSecurityGroupSearchFilter(request.Query!.Trim());
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
                SizeLimit = ComputerGroupSearchMaxLimit,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return ComputerGroupSearchConnectionFailed();
            }

            var items = new List<AdComputerGroupCandidateItem>(ComputerGroupSearchDefaultLimit);
            foreach (SearchResultEntry entry in response.Entries)
            {
                if (!TryMapComputerGroupCandidateItem(entry, out var item))
                {
                    continue;
                }

                if (existingMemberDns.Contains(item.DistinguishedName))
                {
                    continue;
                }

                items.Add(item);
                if (items.Count >= ComputerGroupSearchDefaultLimit)
                {
                    break;
                }
            }

            var sorted = items
                .OrderBy(static item => item, ComputerGroupCandidateItemComparer.Instance)
                .ToList();

            return new AdComputerGroupSearchResult(true, string.Empty, sorted);
        }
        catch (LdapException)
        {
            return ComputerGroupSearchConnectionFailed();
        }
        catch (Exception)
        {
            return ComputerGroupSearchConnectionFailed();
        }
    }

    private async Task<AdComputerGroupOperationResult> ModifyComputerGroupMembershipAsync(
        ComputerGroupMembershipChangeRequest request,
        bool add,
        string operationType,
        string auditAction,
        CancellationToken cancellationToken)
    {
        var groupDn = request.GroupDistinguishedName?.Trim();
        if (string.IsNullOrWhiteSpace(groupDn))
        {
            return await FailComputerGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                AdManagementApiMessageKeys.Groups.GroupDnRequired,
                BuildComputerGroupFailureDiagnostic(
                    operationType,
                    ComputerGroupMembershipValidateStep,
                    request.ComputerId,
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
            return await FailComputerGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                connectionResult.MessageKey,
                BuildComputerGroupFailureDiagnostic(
                    operationType,
                    ComputerGroupMembershipValidateStep,
                    request.ComputerId,
                    groupDn,
                    englishMessageOverride: "The LDAP connection failed.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                null,
                null,
                groupDn,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return await FailComputerGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                AdManagementApiMessageKeys.Common.NotConfigured,
                BuildComputerGroupFailureDiagnostic(
                    operationType,
                    ComputerGroupMembershipValidateStep,
                    request.ComputerId,
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
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadComputerGroupContext(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var computerContext))
            {
                return await FailComputerGroupOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                    AdManagementApiMessageKeys.Computers.NotFound,
                    BuildComputerGroupFailureDiagnostic(
                        operationType,
                        ComputerGroupMembershipLoadComputerStep,
                        request.ComputerId,
                        null,
                        englishMessageOverride: "The AD computer could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    null,
                    null,
                    groupDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            if (AdComputerAccountGuard.IsProtectedComputer(
                    computerContext.PrimaryGroupId,
                    computerContext.UserAccountControl,
                    computerContext.IsCriticalSystemObject))
            {
                return await FailComputerGroupOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                    AdComputerAccountGuard.ProtectedComputerGroupMembershipMessage,
                    BuildComputerGroupFailureDiagnostic(
                        operationType,
                        ComputerGroupMembershipLoadComputerStep,
                        request.ComputerId,
                        computerContext.DistinguishedName,
                        englishMessageOverride: "Group membership cannot be changed for this computer account.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    computerContext,
                    null,
                    groupDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    cancellationToken);
            }

            if (!TryLoadSecurityGroupByDn(ldapConnection, groupDn, out var groupInfo))
            {
                return await FailComputerGroupOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                    AdManagementApiMessageKeys.Groups.NotFound,
                    BuildComputerGroupFailureDiagnostic(
                        operationType,
                        ComputerGroupMembershipLoadGroupStep,
                        request.ComputerId,
                        computerContext.DistinguishedName,
                        englishMessageOverride: "The AD group could not be found or is not a security group.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    computerContext,
                    null,
                    groupDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            var isMember = IsDirectComputerGroupMember(computerContext, groupInfo);
            var beforeContext = computerContext;

            if (add && isMember)
            {
                return await CompleteComputerGroupOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD computer added to group. Computer: {computerContext.SamAccountName}. Group: {groupInfo.Name}.",
                    AdManagementApiMessageKeys.Computers.AlreadyInGroup,
                    connectionResult.Context.Connection,
                    beforeContext,
                    computerContext,
                    groupInfo,
                    cancellationToken);
            }

            if (!add && !isMember)
            {
                return await CompleteComputerGroupOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    $"AD computer removed from group. Computer: {computerContext.SamAccountName}. Group: {groupInfo.Name}.",
                    AdManagementApiMessageKeys.Computers.NotInGroup,
                    connectionResult.Context.Connection,
                    beforeContext,
                    computerContext,
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
                computerContext.DistinguishedName);
            ldapConnection.SendRequest(modifyRequest);

            if (!TryLoadComputerGroupContext(
                    ldapConnection,
                    computersSearchBase,
                    request.ComputerId,
                    out var afterContext))
            {
                return await FailComputerGroupOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                    AdManagementApiMessageKeys.Computers.GroupOperationFailed,
                    BuildComputerGroupFailureDiagnostic(
                        operationType,
                        ComputerGroupMembershipModifyStep,
                        request.ComputerId,
                        computerContext.DistinguishedName,
                        englishMessageOverride: "The AD group membership operation failed after modify.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed),
                    beforeContext,
                    groupInfo,
                    groupDn,
                    AdDirectoryFailureKind.ConnectionFailed,
                    cancellationToken);
            }

            var successMessage = add
                ? AdManagementApiMessageKeys.Computers.GroupMembershipAdded
                : AdManagementApiMessageKeys.Computers.GroupMembershipRemoved;
            var auditDescription = add
                ? $"AD computer added to group. Computer: {computerContext.SamAccountName}. Group: {groupInfo.Name}."
                : $"AD computer removed from group. Computer: {computerContext.SamAccountName}. Group: {groupInfo.Name}.";

            return await CompleteComputerGroupOperationAsync(
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
        catch (LdapException ex)
        {
            return await FailComputerGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                SanitizeComputerGroupLdapError(ex),
                BuildComputerGroupFailureDiagnostic(
                    operationType,
                    ComputerGroupMembershipModifyStep,
                    request.ComputerId,
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
        catch (Exception)
        {
            return await FailComputerGroupOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD computer added to group failed." : "AD computer removed from group failed.",
                AdManagementApiMessageKeys.Computers.GroupOperationFailed,
                BuildComputerGroupFailureDiagnostic(
                    operationType,
                    ComputerGroupMembershipModifyStep,
                    request.ComputerId,
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

    private async Task<AdComputerGroupOperationResult> CompleteComputerGroupOperationAsync(
        ComputerGroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters connection,
        AdComputerGroupContext beforeContext,
        AdComputerGroupContext afterContext,
        AdComputerSecurityGroupInfo groupInfo,
        CancellationToken cancellationToken)
    {
        await WriteComputerGroupSuccessLogsSafelyAsync(
            request,
            operationType,
            auditAction,
            auditDescription,
            connection,
            beforeContext,
            afterContext,
            groupInfo,
            cancellationToken);

        return new AdComputerGroupOperationResult(
            true,
            message,
            afterContext.ComputerId,
            afterContext.Name,
            afterContext.SamAccountName,
            groupInfo.DistinguishedName,
            groupInfo.Name,
            groupInfo.DisplayName,
            groupInfo.SamAccountName);
    }

    private async Task<AdComputerGroupOperationResult> FailComputerGroupOperationAsync(
        ComputerGroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        string errorDiagnosticJson,
        AdComputerGroupContext? beforeContext,
        AdComputerSecurityGroupInfo? groupInfo,
        string? groupDn,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        string? messageKey = null,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteComputerGroupFailureLogsSafelyAsync(
        request,
            operationType,
            auditAction,
            auditDescription,
            beforeContext,
            groupInfo,
            errorDiagnosticJson,
            cancellationToken);

        return new AdComputerGroupOperationResult(
            false,
            message,
            beforeContext?.ComputerId ?? request.ComputerId.ToString("D"),
            beforeContext?.Name,
            beforeContext?.SamAccountName,
            groupInfo?.DistinguishedName ?? groupDn ?? request.GroupDistinguishedName,
            groupInfo?.Name,
            groupInfo?.DisplayName,
            groupInfo?.SamAccountName,
            failureKind);
    }

    private async Task WriteComputerGroupSuccessLogsSafelyAsync(
        ComputerGroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdComputerGroupContext beforeContext,
        AdComputerGroupContext afterContext,
        AdComputerSecurityGroupInfo groupInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerGroupOperationLogsAsync(
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
            LogComputerGroupMembershipLoggingFailure(
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
                BuildComputerGroupMembershipAuditRequest(
                    auditAction,
                    afterContext.ComputerId,
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogComputerGroupMembershipLoggingFailure(
                ex,
                operationSucceeded: true,
                operationType,
                request,
                afterContext,
                groupInfo);
        }
    }

    private async Task WriteComputerGroupFailureLogsSafelyAsync(
        ComputerGroupMembershipChangeRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        AdComputerGroupContext? beforeContext,
        AdComputerSecurityGroupInfo? groupInfo,
        string errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteComputerGroupOperationLogsAsync(
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
            LogComputerGroupMembershipLoggingFailure(
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
                BuildComputerGroupMembershipAuditRequest(
                    auditAction,
                    beforeContext?.ComputerId ?? request.ComputerId.ToString("D"),
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogComputerGroupMembershipLoggingFailure(
                ex,
                operationSucceeded: false,
                operationType,
                request,
                beforeContext,
                groupInfo);
        }
    }

    private static AuditLogWriteRequest BuildComputerGroupMembershipAuditRequest(
        string auditAction,
        string entityId,
        string auditDescription,
        ComputerGroupMembershipChangeRequest request) =>
        new()
        {
            Action = auditAction,
            EntityName = "AdComputerGroupMembership",
            EntityId = entityId,
            Description = auditDescription,
            ActorUserId = request.ActorUserId,
            ActorUserName = request.ActorUserName,
            IpAddress = request.ActorIpAddress,
            UserAgent = request.ActorUserAgent,
        };

    private void LogComputerGroupMembershipLoggingFailure(
        Exception exception,
        bool operationSucceeded,
        string operationType,
        ComputerGroupMembershipChangeRequest request,
        AdComputerGroupContext? computerContext,
        AdComputerSecurityGroupInfo? groupInfo)
    {
        var logMessage = operationSucceeded
            ? ComputerGroupMembershipSuccessLoggingFailedMessage
            : ComputerGroupMembershipFailureLoggingFailedMessage;

        logger.LogError(
            exception,
            "{LogMessage} OperationType={OperationType} ComputerId={ComputerId} GroupName={GroupName} GroupDistinguishedName={GroupDistinguishedName} ActorUserId={ActorUserId}",
            logMessage,
            operationType,
            computerContext?.ComputerId ?? request.ComputerId.ToString("D"),
            groupInfo?.Name,
            TruncateComputerGroupForServerLog(groupInfo?.DistinguishedName ?? request.GroupDistinguishedName),
            request.ActorUserId);
    }

    private static string? TruncateComputerGroupForServerLog(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= ComputerGroupMembershipServerLogDnMaxLength
                ? value.Trim()
                : value.Trim()[..ComputerGroupMembershipServerLogDnMaxLength];

    private async Task WriteComputerGroupOperationLogsAsync(
        ComputerGroupMembershipChangeRequest request,
        string operationType,
        string status,
        AdManagementConnectionParameters? connection,
        AdComputerGroupContext? beforeContext,
        AdComputerGroupContext? afterContext,
        AdComputerSecurityGroupInfo? groupInfo,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var groupDistinguishedName = groupInfo?.DistinguishedName ?? request.GroupDistinguishedName;
        var requestSummary = AdOperationLogSnapshotBuilder.BuildComputerGroupMembershipRequestSummary(
            operationType,
            request.ComputerId,
            groupDistinguishedName,
            groupInfo?.Name);

        var beforeSnapshot = beforeContext is null || groupInfo is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerGroupMembershipBeforeSnapshot(
                operationType,
                beforeContext.ComputerId,
                beforeContext.SamAccountName,
                beforeContext.Name,
                beforeContext.DistinguishedName,
                groupInfo.Id,
                groupInfo.DisplayName,
                groupInfo.Name,
                groupInfo.SamAccountName,
                groupInfo.DistinguishedName,
                IsDirectComputerGroupMember(beforeContext, groupInfo));

        var afterSnapshot = afterContext is null || groupInfo is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildComputerGroupMembershipAfterSnapshot(
                operationType,
                afterContext.ComputerId,
                afterContext.SamAccountName,
                afterContext.Name,
                afterContext.DistinguishedName,
                groupInfo.Id,
                groupInfo.DisplayName,
                groupInfo.Name,
                groupInfo.SamAccountName,
                groupInfo.DistinguishedName,
                IsDirectComputerGroupMember(afterContext, groupInfo));

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
                TargetDistinguishedName = afterContext?.DistinguishedName ?? beforeContext?.DistinguishedName,
                TargetObjectGuid = afterContext?.ComputerId ?? beforeContext?.ComputerId ?? request.ComputerId.ToString("D"),
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

    private static bool IsDirectComputerGroupMember(
        AdComputerGroupContext computerContext,
        AdComputerSecurityGroupInfo groupInfo) =>
        computerContext.MemberOfDns.Contains(groupInfo.DistinguishedName, StringComparer.OrdinalIgnoreCase);

    private static string BuildComputerGroupFailureDiagnostic(
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

    private static IReadOnlyList<AdComputerGroupMembershipItem> BuildComputerDirectGroupMembershipItems(
        LdapConnection ldapConnection,
        IReadOnlyCollection<string> memberOfDns)
    {
        var items = new List<AdComputerGroupMembershipItem>(memberOfDns.Count);
        foreach (var groupDn in memberOfDns)
        {
            if (TryLoadSecurityGroupByDn(ldapConnection, groupDn, out var groupInfo))
            {
                items.Add(new AdComputerGroupMembershipItem(
                    groupInfo.Id,
                    groupInfo.DistinguishedName,
                    groupInfo.DisplayName,
                    groupInfo.Name,
                    groupInfo.SamAccountName,
                    groupInfo.Description,
                    IsDirect: true));
                continue;
            }

            if (TryLoadGroupByDn(ldapConnection, groupDn, out var fallbackGroup))
            {
                items.Add(new AdComputerGroupMembershipItem(
                    string.Empty,
                    fallbackGroup.DistinguishedName,
                    fallbackGroup.DisplayName,
                    fallbackGroup.Name,
                    fallbackGroup.SamAccountName,
                    fallbackGroup.Description,
                    IsDirect: true));
                continue;
            }

            var fallbackName = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(groupDn) ?? groupDn;
            items.Add(new AdComputerGroupMembershipItem(
                string.Empty,
                groupDn,
                null,
                fallbackName,
                null,
                null,
                IsDirect: true));
        }

        return items
            .OrderBy(static item => item, ComputerGroupMembershipItemComparer.Instance)
            .ToList();
    }

    private static bool TryLoadComputerGroupContext(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        out AdComputerGroupContext context)
    {
        context = null!;
        var filter = AdLdapComputerFilterHelper.BuildComputerObjectGuidFilter(objectGuid);
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            ComputerGroupContextAttributes)
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
        var name = GetFirstString(entry, "name")
            ?? GetFirstString(entry, "cn")
            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;

        context = new AdComputerGroupContext(
            resolvedGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            name,
            GetFirstString(entry, "dNSHostName"),
            GetFirstInt(entry, "primaryGroupID"),
            GetFirstInt(entry, "userAccountControl"),
            GetFirstBool(entry, "isCriticalSystemObject"),
            memberOf.ToHashSet(StringComparer.OrdinalIgnoreCase));

        return true;
    }

    private static bool TryLoadSecurityGroupByDn(
        LdapConnection ldapConnection,
        string groupDistinguishedName,
        out AdComputerSecurityGroupInfo groupInfo)
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
            "objectGUID",
            "distinguishedName",
            "displayName",
            "cn",
            "name",
            "sAMAccountName",
            "description",
            "groupType")
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

        var entry = response.Entries[0];
        if (!TryGetObjectGuid(entry, out var resolvedGuid))
        {
            return false;
        }

        var groupTypeRaw = GetFirstInt(entry, "groupType");
        if (!AdGroupTypeHelper.Parse(groupTypeRaw).SecurityEnabled)
        {
            return false;
        }

        if (!TryMapGroupDirectoryInfo(entry, out var directoryInfo))
        {
            return false;
        }

        groupInfo = new AdComputerSecurityGroupInfo(
            resolvedGuid.ToString("D"),
            directoryInfo.DistinguishedName,
            directoryInfo.DisplayName,
            directoryInfo.Name,
            directoryInfo.SamAccountName,
            directoryInfo.Description);

        return true;
    }

    private static bool TryMapComputerGroupCandidateItem(
        SearchResultEntry entry,
        out AdComputerGroupCandidateItem item)
    {
        item = null!;
        if (!TryMapGroupDirectoryInfo(entry, out var groupInfo))
        {
            return false;
        }

        item = new AdComputerGroupCandidateItem(
            groupInfo.DistinguishedName,
            groupInfo.DisplayName,
            groupInfo.Name,
            groupInfo.SamAccountName,
            groupInfo.Description);

        return true;
    }

    private static string ResolveComputerGroupSortKey(
        string? displayName,
        string? samAccountName,
        string name)
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

    private static AdComputerGroupMembershipResult ConnectionFailedComputerGroupMembership(
        Guid computerId,
        string messageKey,
        AdDirectoryFailureKind? failureKind,
        IReadOnlyDictionary<string, object>? messageParams = null) =>
        new(
            false,
            messageKey,
            computerId.ToString("D"),
            null,
            null,
            null,
            null,
            null,
            failureKind,
            messageParams);

    private static AdComputerGroupSearchResult ComputerGroupSearchConnectionFailed() =>
        new(false, AdManagementApiMessageKeys.Groups.QueryFailed, null, AdDirectoryFailureKind.ConnectionFailed);

    private static string SanitizeComputerGroupLdapError(LdapException exception) =>
        string.IsNullOrWhiteSpace(exception.Message)
            || exception.Message.Contains("ldap", StringComparison.OrdinalIgnoreCase)
            ? AdManagementApiMessageKeys.Computers.GroupOperationFailed
            : AdManagementApiMessageKeys.Computers.GroupOperationFailed;

    private sealed class ComputerGroupMembershipItemComparer : IComparer<AdComputerGroupMembershipItem>
    {
        public static ComputerGroupMembershipItemComparer Instance { get; } = new();

        public int Compare(AdComputerGroupMembershipItem? left, AdComputerGroupMembershipItem? right)
        {
            if (left is null || right is null)
            {
                return 0;
            }

            var comparison = string.Compare(
                ResolveComputerGroupSortKey(left.DisplayName, left.SamAccountName, left.Name),
                ResolveComputerGroupSortKey(right.DisplayName, right.SamAccountName, right.Name),
                StringComparison.OrdinalIgnoreCase);

            return comparison != 0
                ? comparison
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class ComputerGroupCandidateItemComparer : IComparer<AdComputerGroupCandidateItem>
    {
        public static ComputerGroupCandidateItemComparer Instance { get; } = new();

        public int Compare(AdComputerGroupCandidateItem? left, AdComputerGroupCandidateItem? right)
        {
            if (left is null || right is null)
            {
                return 0;
            }

            var comparison = string.Compare(
                ResolveComputerGroupSortKey(left.DisplayName, left.SamAccountName, left.Name),
                ResolveComputerGroupSortKey(right.DisplayName, right.SamAccountName, right.Name),
                StringComparison.OrdinalIgnoreCase);

            return comparison != 0
                ? comparison
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record AdComputerGroupContext(
        string ComputerId,
        string DistinguishedName,
        string? SamAccountName,
        string Name,
        string? DnsHostName,
        int? PrimaryGroupId,
        int? UserAccountControl,
        bool? IsCriticalSystemObject,
        HashSet<string> MemberOfDns);

    private sealed record AdComputerSecurityGroupInfo(
        string Id,
        string DistinguishedName,
        string? DisplayName,
        string Name,
        string? SamAccountName,
        string? Description);

    private sealed record ComputerGroupMembershipChangeRequest(
        Guid ComputerId,
        string GroupDistinguishedName,
        Guid? ActorUserId,
        string? ActorUserName,
        string? ActorIpAddress,
        string? ActorUserAgent);
}
