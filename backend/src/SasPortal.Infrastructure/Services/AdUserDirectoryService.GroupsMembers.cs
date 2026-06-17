using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string GroupMemberSuccessLoggingFailedMessage =
        "AD group member operation succeeded but logging failed.";
    private const string GroupMemberFailureLoggingFailedMessage =
        "AD group member operation failed but logging failed.";

    private static readonly string[] MemberDetailAttributes =
    [
        "objectGUID",
        "objectClass",
        "displayName",
        "cn",
        "name",
        "sAMAccountName",
        "userPrincipalName",
        "dNSHostName",
        "description",
        "distinguishedName",
        "userAccountControl",
        "groupType",
    ];

    private static class GroupMemberSteps
    {
        public const string LoadGroup = "LoadGroup";
        public const string LoadMember = "LoadMember";
        public const string Preflight = "Preflight";
        public const string AddMember = "AddMember";
        public const string RemoveMember = "RemoveMember";
        public const string ReloadMembership = "ReloadMembership";
        public const string WriteOperationLog = "WriteOperationLog";
    }

    public Task<AdGroupMembersListResult> GetGroupMembersAsync(
        AdGroupMembersListQuery query,
        CancellationToken cancellationToken = default) =>
        LoadGroupMembersAsync(query, cancellationToken);

    public Task<AdGroupMemberCandidatesResult> SearchGroupMemberCandidatesAsync(
        AdGroupMemberCandidatesQuery query,
        CancellationToken cancellationToken = default) =>
        SearchGroupMemberCandidatesInternalAsync(query, cancellationToken);

    public Task<AdGroupMemberOperationResult> AddGroupMemberAsync(
        AddAdGroupMemberRequest request,
        CancellationToken cancellationToken = default) =>
        ModifyGroupMemberAsync(request, add: true, cancellationToken);

    public Task<AdGroupMemberOperationResult> RemoveGroupMemberAsync(
        RemoveAdGroupMemberRequest request,
        CancellationToken cancellationToken = default) =>
        ModifyGroupMemberAsync(
            new AddAdGroupMemberRequest(
                request.GroupId,
                request.MemberDistinguishedName,
                MemberType: null,
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent),
            add: false,
            cancellationToken);

    private async Task<AdGroupMembersListResult> LoadGroupMembersAsync(
        AdGroupMembersListQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = ClampMemberListPageSize(query.PageSize);
        var pageNumber = AdLdapValueConverter.NormalizePageNumber(query.PageNumber);

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdGroupMembersListResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageKey,
                connectionResult.MessageParams);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdGroupMembersListResult(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Common.NotConfigured),
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadGroupForUpdate(
                    ldapConnection,
                    groupsSearchBase,
                    query.GroupId,
                    out var groupDetail,
                    out _))
            {
                return new AdGroupMembersListResult(
                    false,
                    AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.NotFound),
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            var memberDns = ReadAllAttributeValues(
                ldapConnection,
                groupDetail!.DistinguishedName,
                "member")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var normalizedSearch = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
            var typeFilter = NormalizeMemberTypeFilter(query.Type);
            var useFastPath = normalizedSearch is null && typeFilter is null;

            IReadOnlyList<AdGroupMemberListItem> pageItems;
            int memberCount;
            bool hasNextPage;

            if (useFastPath)
            {
                memberCount = memberDns.Count;
                var skip = (pageNumber - 1) * pageSize;
                var pageDns = memberDns.Skip(skip).Take(pageSize + 1).ToList();
                hasNextPage = pageDns.Count > pageSize;
                if (hasNextPage)
                {
                    pageDns.RemoveAt(pageSize);
                }

                pageItems = ResolveMemberListItems(ldapConnection, pageDns);
            }
            else
            {
                var filteredItems = FilterMemberListItems(
                    ldapConnection,
                    memberDns,
                    normalizedSearch,
                    typeFilter);
                memberCount = filteredItems.Count;
                var skip = (pageNumber - 1) * pageSize;
                pageItems = filteredItems.Skip(skip).Take(pageSize + 1).ToList();
                hasNextPage = pageItems.Count > pageSize;
                if (hasNextPage)
                {
                    pageItems = pageItems.Take(pageSize).ToList();
                }
            }

            return new AdGroupMembersListResult(
                true,
                string.Empty,
                new AdGroupMembersPage(pageItems, pageNumber, pageSize, memberCount, hasNextPage));
        }
        catch (LdapException)
        {
            return GroupMembersListConnectionFailed();
        }
        catch (Exception)
        {
            return GroupMembersListConnectionFailed();
        }
    }

    private async Task<AdGroupMemberCandidatesResult> SearchGroupMemberCandidatesInternalAsync(
        AdGroupMemberCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        if (!AdLdapAttributeCatalog.IsSearchTermValid(query.Search))
        {
            return new AdGroupMemberCandidatesResult(true, string.Empty, []);
        }

        var pageSize = ClampMemberCandidatePageSize(query.PageSize);
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdGroupMemberCandidatesResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageKey,
                connectionResult.MessageParams);
        }

        var connection = connectionResult.Context.Connection;
        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdGroupMemberCandidatesResult(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Common.NotConfigured),
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadGroupForUpdate(
                    ldapConnection,
                    groupsSearchBase,
                    query.GroupId,
                    out var groupDetail,
                    out _))
            {
                return new AdGroupMemberCandidatesResult(
                    false,
                    AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.NotFound),
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            var directMemberDns = ReadAllAttributeValues(
                ldapConnection,
                groupDetail!.DistinguishedName,
                "member")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var requestedTypes = NormalizeCandidateTypes(query.Types);
            if (requestedTypes.Count == 0)
            {
                requestedTypes = ["user", "group", "computer"];
            }

            var items = new List<AdGroupMemberCandidateItem>(pageSize);
            var seenDns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidateType in requestedTypes)
            {
                if (items.Count >= pageSize)
                {
                    break;
                }

                var remaining = pageSize - items.Count;
                var batch = SearchMemberCandidatesByType(
                    ldapConnection,
                    connection,
                    candidateType,
                    query.Search!.Trim(),
                    groupDetail,
                    directMemberDns,
                    seenDns,
                    remaining);

                items.AddRange(batch);
            }

            return new AdGroupMemberCandidatesResult(true, string.Empty, items);
        }
        catch (LdapException)
        {
            return MemberCandidatesConnectionFailed();
        }
        catch (Exception)
        {
            return MemberCandidatesConnectionFailed();
        }
    }

    private async Task<AdGroupMemberOperationResult> ModifyGroupMemberAsync(
        AddAdGroupMemberRequest request,
        bool add,
        CancellationToken cancellationToken)
    {
        var operationType = add
            ? AdManagementOperationTypes.GroupMemberAdd
            : AdManagementOperationTypes.GroupMemberRemove;
        var auditAction = add ? "Add" : "Remove";
        var memberDn = request.MemberDistinguishedName?.Trim();

        if (string.IsNullOrWhiteSpace(memberDn))
        {
            return await FailGroupMemberOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD security group member add failed." : "AD security group member remove failed.",
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Common.InvalidRequest),
                AdGroupMemberOperationDiagnosticBuilder.BuildPreflightJson(
                    operationType,
                    GroupMemberSteps.Preflight,
                    AdUserUpdateNormalizedReasons.InvalidRequest,
                    "The member distinguished name is invalid.",
                    request.GroupId),
                null,
                null,
                null,
                AdDirectoryFailureKind.InvalidRequest,
                cancellationToken);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailGroupMemberOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD security group member add failed." : "AD security group member remove failed.",
                connectionResult.Message,
                AdGroupMemberOperationDiagnosticBuilder.BuildPreflightJson(
                    operationType,
                    GroupMemberSteps.Preflight,
                    AdUserUpdateNormalizedReasons.ConnectionFailed,
                    "The LDAP connection failed.",
                    request.GroupId,
                    memberDn),
                null,
                null,
                memberDn,
                connectionResult.FailureKind,
                cancellationToken);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return await FailGroupMemberOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD security group member add failed." : "AD security group member remove failed.",
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Common.NotConfigured),
                AdGroupMemberOperationDiagnosticBuilder.BuildPreflightJson(
                    operationType,
                    GroupMemberSteps.Preflight,
                    AdUserUpdateNormalizedReasons.InvalidRequest,
                    "AD management is not configured.",
                    request.GroupId,
                    memberDn),
                null,
                null,
                memberDn,
                AdDirectoryFailureKind.NotConfigured,
                cancellationToken);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadGroupForUpdate(
                    ldapConnection,
                    groupsSearchBase,
                    request.GroupId,
                    out var groupDetail,
                    out _))
            {
                return await FailGroupMemberOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD security group member add failed." : "AD security group member remove failed.",
                    AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.NotFound),
                    AdGroupMemberOperationDiagnosticBuilder.BuildNotFoundJson(
                        operationType,
                        GroupMemberSteps.LoadGroup,
                        request.GroupId),
                    null,
                    null,
                    memberDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            if (!TryResolveGroupMemberDetail(ldapConnection, memberDn, out var memberDetail))
            {
                return await FailGroupMemberOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD security group member add failed." : "AD security group member remove failed.",
                    AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.NotFound),
                    AdGroupMemberOperationDiagnosticBuilder.BuildPreflightJson(
                        operationType,
                        GroupMemberSteps.LoadMember,
                        AdUserUpdateNormalizedReasons.NoSuchObject,
                        "The member object could not be found.",
                        request.GroupId,
                        memberDn),
                    groupDetail,
                    null,
                    memberDn,
                    AdDirectoryFailureKind.NotFound,
                    cancellationToken);
            }

            var memberValidation = ValidateMemberForGroupOperation(
                connectionResult.Context.Connection,
                groupDetail!,
                memberDetail!,
                request.MemberType,
                add);

            if (!memberValidation.IsValid)
            {
                return await FailGroupMemberOperationAsync(
        request,
                    operationType,
                    auditAction,
                    add ? "AD security group member add failed." : "AD security group member remove failed.",
                    memberValidation.UserMessage,
                    AdGroupMemberOperationDiagnosticBuilder.BuildPreflightJson(
                        operationType,
                        GroupMemberSteps.Preflight,
                        memberValidation.NormalizedReason,
                        memberValidation.EnglishMessage,
                        request.GroupId,
                        memberDn),
                    groupDetail,
                    memberDetail,
                    memberDn,
                    memberValidation.FailureKind,
                    cancellationToken);
            }

            var directMemberDns = ReadAllAttributeValues(
                ldapConnection,
                groupDetail!.DistinguishedName,
                "member")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var isDirectMember = directMemberDns.Contains(memberDetail!.DistinguishedName);

            if (add && isDirectMember)
            {
                return await CompleteGroupMemberOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    "AD security group member added.",
                    AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.MemberAlreadyInGroup),
                    connectionResult.Context.Connection,
                    groupDetail,
                    memberDetail,
                    isDirectMember: true,
                    cancellationToken);
            }

            if (!add && !isDirectMember)
            {
                return await CompleteGroupMemberOperationAsync(
                    request,
                    operationType,
                    auditAction,
                    "AD security group member removed.",
                    AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.MemberNotInGroup),
                    connectionResult.Context.Connection,
                    groupDetail,
                    memberDetail,
                    isDirectMember: false,
                    cancellationToken);
            }

            var modifyOperation = add
                ? DirectoryAttributeOperation.Add
                : DirectoryAttributeOperation.Delete;
            var modifyRequest = new ModifyRequest(
                groupDetail.DistinguishedName,
                modifyOperation,
                "member",
                memberDetail.DistinguishedName);
            ldapConnection.SendRequest(modifyRequest);

            var afterIsDirectMember = add;
            if (!TryReloadDirectMembership(
                    ldapConnection,
                    groupDetail.DistinguishedName,
                    memberDetail.DistinguishedName,
                    out afterIsDirectMember))
            {
                afterIsDirectMember = add;
            }

            var successMessage = add ? AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.MemberAdded) : AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.MemberRemoved);
            var auditDescription = add
                ? "AD security group member added."
                : "AD security group member removed.";

            return await CompleteGroupMemberOperationAsync(
                request,
                operationType,
                auditAction,
                auditDescription,
                successMessage,
                connectionResult.Context.Connection,
                groupDetail,
                memberDetail,
                afterIsDirectMember,
                cancellationToken);
        }
        catch (LdapException ex)
        {
            return await FailGroupMemberOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD security group member add failed." : "AD security group member remove failed.",
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.MemberOperationFailed),
                AdGroupMemberOperationDiagnosticBuilder.BuildGenericFailureJson(
                    operationType,
                    add ? GroupMemberSteps.AddMember : GroupMemberSteps.RemoveMember,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The AD group member operation failed.",
                    request.GroupId,
                    memberDn,
                    ex.ErrorCode,
                    ex.ErrorCode,
                    ex.Message),
                null,
                null,
                memberDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
        catch (Exception)
        {
            return await FailGroupMemberOperationAsync(
        request,
                operationType,
                auditAction,
                add ? "AD security group member add failed." : "AD security group member remove failed.",
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.MemberOperationFailed),
                AdGroupMemberOperationDiagnosticBuilder.BuildGenericFailureJson(
                    operationType,
                    add ? GroupMemberSteps.AddMember : GroupMemberSteps.RemoveMember,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The AD group member operation failed.",
                    request.GroupId,
                    memberDn),
                null,
                null,
                memberDn,
                AdDirectoryFailureKind.ConnectionFailed,
                cancellationToken);
        }
    }

    private async Task<AdGroupMemberOperationResult> CompleteGroupMemberOperationAsync(
        AddAdGroupMemberRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        AdManagementConnectionParameters connection,
        AdGroupDetail groupDetail,
        AdGroupMemberSnapshotInfo memberDetail,
        bool isDirectMember,
        CancellationToken cancellationToken)
    {
        await WriteGroupMemberSuccessLogsSafelyAsync(
            request,
            operationType,
            auditAction,
            auditDescription,
            connection,
            groupDetail,
            memberDetail,
            isDirectMember,
            cancellationToken);

        return new AdGroupMemberOperationResult(
            true,
            message,
            groupDetail.Id,
            groupDetail.DistinguishedName,
            groupDetail.Name,
            memberDetail.DistinguishedName,
            memberDetail.DisplayName ?? memberDetail.Name);
    }

    private async Task<AdGroupMemberOperationResult> FailGroupMemberOperationAsync(
        AddAdGroupMemberRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        string message,
        string errorDiagnosticJson,
        AdGroupDetail? groupDetail,
        AdGroupMemberSnapshotInfo? memberDetail,
        string? memberDn,
        AdDirectoryFailureKind? failureKind,
        CancellationToken cancellationToken,
        string? messageKey = null,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteGroupMemberFailureLogsSafelyAsync(
        request,
            operationType,
            auditAction,
            auditDescription,
            groupDetail,
            memberDetail,
            errorDiagnosticJson,
            cancellationToken);

        return new AdGroupMemberOperationResult(
            false,
            message,
            groupDetail?.Id ?? request.GroupId.ToString("D"),
            groupDetail?.DistinguishedName,
            groupDetail?.Name,
            memberDetail?.DistinguishedName ?? memberDn,
            memberDetail?.DisplayName ?? memberDetail?.Name,
            failureKind);
    }

    private async Task WriteGroupMemberSuccessLogsSafelyAsync(
        AddAdGroupMemberRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        AdManagementConnectionParameters connection,
        AdGroupDetail groupDetail,
        AdGroupMemberSnapshotInfo memberDetail,
        bool isDirectMember,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupMemberOperationLogsAsync(
                request,
                operationType,
                AdManagementOperationStatuses.Succeeded,
                connection,
                groupDetail,
                memberDetail,
                isDirectMember,
                errorDiagnosticJson: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogGroupMemberLoggingFailure(ex, operationSucceeded: true, operationType, request, groupDetail);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildGroupMemberAuditRequest(
                    auditAction,
                    groupDetail.Id,
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogGroupMemberLoggingFailure(ex, operationSucceeded: true, operationType, request, groupDetail);
        }
    }

    private async Task WriteGroupMemberFailureLogsSafelyAsync(
        AddAdGroupMemberRequest request,
        string operationType,
        string auditAction,
        string auditDescription,
        AdGroupDetail? groupDetail,
        AdGroupMemberSnapshotInfo? memberDetail,
        string errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteGroupMemberOperationLogsAsync(
                request,
                operationType,
                AdManagementOperationStatuses.Failed,
                connection: null,
                groupDetail,
                memberDetail,
                memberDetail is null ? false : true,
                errorDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogGroupMemberLoggingFailure(ex, operationSucceeded: false, operationType, request, groupDetail);
        }

        if (string.IsNullOrWhiteSpace(auditDescription))
        {
            return;
        }

        try
        {
            await auditLogWriter.WriteAsync(
                BuildGroupMemberAuditRequest(
                    auditAction,
                    groupDetail?.Id ?? request.GroupId.ToString("D"),
                    auditDescription,
                    request),
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogGroupMemberLoggingFailure(ex, operationSucceeded: false, operationType, request, groupDetail);
        }
    }

    private static AuditLogWriteRequest BuildGroupMemberAuditRequest(
        string auditAction,
        string entityId,
        string auditDescription,
        AddAdGroupMemberRequest request) =>
        new()
        {
            Action = auditAction,
            EntityName = "AdGroup",
            EntityId = entityId,
            Description = auditDescription,
            ActorUserId = request.ActorUserId,
            ActorUserName = request.ActorUserName,
            IpAddress = request.ActorIpAddress,
            UserAgent = request.ActorUserAgent,
        };

    private void LogGroupMemberLoggingFailure(
        Exception exception,
        bool operationSucceeded,
        string operationType,
        AddAdGroupMemberRequest request,
        AdGroupDetail? groupDetail)
    {
        var logMessage = operationSucceeded
            ? GroupMemberSuccessLoggingFailedMessage
            : GroupMemberFailureLoggingFailedMessage;

        logger.LogError(
            exception,
            "{LogMessage} OperationType={OperationType} GroupId={GroupId} GroupName={GroupName} ActorUserId={ActorUserId}",
            logMessage,
            operationType,
            groupDetail?.Id ?? request.GroupId.ToString("D"),
            groupDetail?.Name,
            request.ActorUserId);
    }

    private async Task WriteGroupMemberOperationLogsAsync(
        AddAdGroupMemberRequest request,
        string operationType,
        string status,
        AdManagementConnectionParameters? connection,
        AdGroupDetail? groupDetail,
        AdGroupMemberSnapshotInfo? memberDetail,
        bool isDirectMember,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var requestSummary = groupDetail is null || memberDetail is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildGroupMemberOperationRequestSummary(
                operationType,
                groupDetail.Id,
                groupDetail.Name,
                groupDetail.SamAccountName,
                groupDetail.DistinguishedName,
                memberDetail.Type,
                memberDetail.Name,
                memberDetail.SamAccountName,
                memberDetail.DistinguishedName);

        string? beforeSnapshot = null;
        string? afterSnapshot = null;
        if (groupDetail is not null && memberDetail is not null)
        {
            var beforeDirect = operationType == AdManagementOperationTypes.GroupMemberAdd
                ? false
                : true;
            var afterDirect = operationType == AdManagementOperationTypes.GroupMemberAdd;

            if (!string.Equals(status, AdManagementOperationStatuses.Succeeded, StringComparison.Ordinal))
            {
                afterDirect = beforeDirect;
            }

            beforeSnapshot = AdOperationLogSnapshotBuilder.BuildGroupMemberOperationBeforeSnapshot(
                operationType,
                groupDetail,
                memberDetail,
                beforeDirect);
            afterSnapshot = string.Equals(status, AdManagementOperationStatuses.Succeeded, StringComparison.Ordinal)
                ? AdOperationLogSnapshotBuilder.BuildGroupMemberOperationAfterSnapshot(
                    operationType,
                    groupDetail,
                    memberDetail,
                    afterDirect)
                : null;
        }

        var isSuccess = string.Equals(status, AdManagementOperationStatuses.Succeeded, StringComparison.Ordinal);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = operationType,
                Status = status,
                TargetObjectType = AdManagementTargetGroupTypes.AdGroup,
                TargetDistinguishedName = groupDetail?.DistinguishedName,
                TargetObjectGuid = groupDetail?.Id ?? request.GroupId.ToString("D"),
                TargetSamAccountName = groupDetail?.SamAccountName,
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

    private static bool TryReloadDirectMembership(
        LdapConnection ldapConnection,
        string groupDistinguishedName,
        string memberDistinguishedName,
        out bool isDirectMember)
    {
        isDirectMember = false;
        try
        {
            var memberDns = ReadAllAttributeValues(ldapConnection, groupDistinguishedName, "member");
            isDirectMember = memberDns.Contains(memberDistinguishedName, StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch (LdapException)
        {
            return false;
        }
    }

    private static MemberValidationResult ValidateMemberForGroupOperation(
        AdManagementConnectionParameters connection,
        AdGroupDetail groupDetail,
        AdGroupMemberSnapshotInfo memberDetail,
        string? requestedMemberType,
        bool add)
    {
        if (AdLdapDnHelper.AreDistinguishedNamesEqual(
                groupDetail.DistinguishedName,
                memberDetail.DistinguishedName)
            || string.Equals(groupDetail.Id, memberDetail.Id, StringComparison.OrdinalIgnoreCase))
        {
            return MemberValidationResult.Invalid(
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.SelfMembership),
                AdUserUpdateNormalizedReasons.InvalidRequest,
                "A group cannot be added as a member of itself.",
                AdDirectoryFailureKind.InvalidRequest);
        }

        if (!string.IsNullOrWhiteSpace(requestedMemberType)
            && !string.Equals(
                NormalizeMemberTypeCode(requestedMemberType),
                NormalizeMemberTypeCode(memberDetail.Type),
                StringComparison.OrdinalIgnoreCase))
        {
            return MemberValidationResult.Invalid(
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.NotFound),
                AdUserUpdateNormalizedReasons.InvalidRequest,
                "The member type does not match the resolved AD object.",
                AdDirectoryFailureKind.InvalidRequest);
        }

        if (!IsAllowedMemberType(memberDetail.Type))
        {
            return MemberValidationResult.Invalid(
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.NotFound),
                AdUserUpdateNormalizedReasons.InvalidRequest,
                "Only users, security groups, and computers can be group members.",
                AdDirectoryFailureKind.InvalidRequest);
        }

        if (!IsMemberUnderAllowedSearchBase(connection, memberDetail))
        {
            return MemberValidationResult.Invalid(
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.NotFound),
                AdUserUpdateNormalizedReasons.NoSuchObject,
                "The member object is outside the allowed search base.",
                AdDirectoryFailureKind.NotFound);
        }

        if (string.Equals(memberDetail.Type, "Group", StringComparison.OrdinalIgnoreCase)
            && !memberDetail.IsSecurityGroup)
        {
            return MemberValidationResult.Invalid(
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.NotFound),
                AdUserUpdateNormalizedReasons.InvalidRequest,
                "Distribution groups cannot be added as members.",
                AdDirectoryFailureKind.InvalidRequest);
        }

        return MemberValidationResult.Valid();
    }

    private static bool IsMemberUnderAllowedSearchBase(
        AdManagementConnectionParameters connection,
        AdGroupMemberSnapshotInfo memberDetail)
    {
        var memberDn = memberDetail.DistinguishedName;
        return NormalizeMemberTypeCode(memberDetail.Type) switch
        {
            "user" => AdLdapDnHelper.IsEqualOrDescendantOf(
                memberDn,
                ResolveUsersRootOu(connection)),
            "group" => AdLdapDnHelper.IsEqualOrDescendantOf(
                memberDn,
                ResolveRequiredGroupsSearchBaseStatic(connection)),
            "computer" => AdLdapDnHelper.IsEqualOrDescendantOf(
                memberDn,
                ResolveComputersSearchBase(connection)),
            _ => false,
        };
    }

    private static string? ResolveUsersRootOu(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.UsersRootOu)
            ? connection.DefaultNamingContext ?? connection.BaseDn
            : connection.UsersRootOu;

    private static string? ResolveComputersSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.ComputersSearchBase)
            ? null
            : connection.ComputersSearchBase.Trim();

    private static string? ResolveRequiredGroupsSearchBaseStatic(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.GroupsSearchBase) ? null : connection.GroupsSearchBase.Trim();

    private static bool IsAllowedMemberType(string type)
    {
        var normalized = NormalizeMemberTypeCode(type);
        return normalized is "user" or "group" or "computer";
    }

    private static IReadOnlyList<AdGroupMemberListItem> ResolveMemberListItems(
        LdapConnection ldapConnection,
        IReadOnlyList<string> memberDns)
    {
        var items = new List<AdGroupMemberListItem>(memberDns.Count);
        foreach (var memberDn in memberDns)
        {
            if (TryResolveGroupMemberDetail(ldapConnection, memberDn, out var detail))
            {
                items.Add(MapToMemberListItem(detail!));
                continue;
            }

            items.Add(new AdGroupMemberListItem(
                null,
                "Unknown",
                null,
                AdLdapDnHelper.ParseCommonNameFromDistinguishedName(memberDn),
                null,
                null,
                null,
                null,
                null,
                memberDn,
                IsDirectMember: true));
        }

        return items;
    }

    private static List<AdGroupMemberListItem> FilterMemberListItems(
        LdapConnection ldapConnection,
        IReadOnlyList<string> memberDns,
        string? search,
        string? typeFilter)
    {
        var results = new List<AdGroupMemberListItem>();
        var normalizedSearch = search?.Trim();

        for (var index = 0; index < memberDns.Count; index += AdGroupDirectoryLimits.MemberResolveBatchSize)
        {
            var batch = memberDns.Skip(index).Take(AdGroupDirectoryLimits.MemberResolveBatchSize);
            foreach (var memberDn in batch)
            {
                if (!TryResolveGroupMemberDetail(ldapConnection, memberDn, out var detail))
                {
                    if (typeFilter is not null
                        && !string.Equals(typeFilter, "unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(normalizedSearch)
                        && !DnMatchesSearch(memberDn, normalizedSearch))
                    {
                        continue;
                    }

                    results.Add(new AdGroupMemberListItem(
                        null,
                        "Unknown",
                        null,
                        AdLdapDnHelper.ParseCommonNameFromDistinguishedName(memberDn),
                        null,
                        null,
                        null,
                        null,
                        null,
                        memberDn,
                        IsDirectMember: true));
                    continue;
                }

                if (!MatchesMemberSearch(detail!, normalizedSearch))
                {
                    continue;
                }

                if (typeFilter is not null
                    && !string.Equals(
                        NormalizeMemberTypeCode(detail!.Type),
                        typeFilter,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(MapToMemberListItem(detail!));
            }
        }

        return results;
    }

    private static bool DnMatchesSearch(string distinguishedName, string search) =>
        distinguishedName.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesMemberSearch(AdGroupMemberSnapshotInfo detail, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var values = new[]
        {
            detail.DisplayName,
            detail.Name,
            detail.Cn,
            detail.SamAccountName,
            detail.UserPrincipalName,
            detail.DNSHostName,
            detail.Description,
            detail.DistinguishedName,
        };

        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && value.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static AdGroupMemberListItem MapToMemberListItem(AdGroupMemberSnapshotInfo detail) =>
        new(
            detail.Id,
            detail.Type,
            detail.DisplayName,
            detail.Name,
            detail.Cn,
            detail.SamAccountName,
            detail.UserPrincipalName,
            detail.DNSHostName,
            detail.Description,
            detail.DistinguishedName,
            IsDirectMember: true);

    private static bool TryResolveGroupMemberDetail(
        LdapConnection ldapConnection,
        string distinguishedName,
        out AdGroupMemberSnapshotInfo? detail)
    {
        detail = null;
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        try
        {
            var searchRequest = new SearchRequest(
                distinguishedName.Trim(),
                "(objectClass=*)",
                SearchScope.Base,
                MemberDetailAttributes)
            {
                SizeLimit = 1,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
            {
                return false;
            }

            return TryMapGroupMemberSnapshotInfo(response.Entries[0], out detail);
        }
        catch (LdapException)
        {
            return false;
        }
    }

    private static bool TryMapGroupMemberSnapshotInfo(
        SearchResultEntry entry,
        out AdGroupMemberSnapshotInfo? detail)
    {
        detail = null;
        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var objectClasses = GetAllStrings(entry, "objectClass");
        var type = ResolveMemberType(objectClasses);
        var cn = GetFirstString(entry, "cn");
        var name = GetFirstString(entry, "name")
            ?? cn
            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;

        string? objectGuid = null;
        if (TryGetObjectGuid(entry, out var guid))
        {
            objectGuid = guid.ToString("D");
        }

        int? groupTypeRaw = null;
        if (string.Equals(type, "Group", StringComparison.OrdinalIgnoreCase))
        {
            groupTypeRaw = GetFirstInt(entry, "groupType");
        }

        var isSecurityGroup = !groupTypeRaw.HasValue
            || AdGroupTypeHelper.Parse(groupTypeRaw).SecurityEnabled;

        detail = new AdGroupMemberSnapshotInfo(
            objectGuid,
            type,
            GetFirstString(entry, "displayName"),
            name,
            cn,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            GetFirstString(entry, "dNSHostName"),
            GetFirstString(entry, "description"),
            distinguishedName)
        {
            IsSecurityGroup = isSecurityGroup,
            IsEnabled = ResolveIsEnabled(entry, type),
        };

        return true;
    }

    private static bool? ResolveIsEnabled(SearchResultEntry entry, string type)
    {
        if (!string.Equals(type, "User", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(type, "Computer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var userAccountControl = GetFirstInt(entry, "userAccountControl");
        if (!userAccountControl.HasValue)
        {
            return null;
        }

        const int accountDisableFlag = 0x0002;
        return (userAccountControl.Value & accountDisableFlag) == 0;
    }

    private IReadOnlyList<AdGroupMemberCandidateItem> SearchMemberCandidatesByType(
        LdapConnection ldapConnection,
        AdManagementConnectionParameters connection,
        string candidateType,
        string search,
        AdGroupDetail groupDetail,
        HashSet<string> directMemberDns,
        HashSet<string> seenDns,
        int maxItems)
    {
        var searchBase = ResolveCandidateSearchBase(connection, candidateType);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return [];
        }

        var filter = BuildCandidateFilter(candidateType, search);
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            MemberDetailAttributes)
        {
            SizeLimit = maxItems,
            TimeLimit = LdapOperationTimeout,
        };

        SearchResponse response;
        try
        {
            response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        }
        catch (LdapException)
        {
            return [];
        }

        if (response.ResultCode != ResultCode.Success)
        {
            return [];
        }

        var items = new List<AdGroupMemberCandidateItem>(maxItems);
        foreach (SearchResultEntry entry in response.Entries)
        {
            if (items.Count >= maxItems)
            {
                break;
            }

            if (!TryMapGroupMemberSnapshotInfo(entry, out var detail) || detail is null)
            {
                continue;
            }

            if (seenDns.Contains(detail.DistinguishedName)
                || directMemberDns.Contains(detail.DistinguishedName)
                || AdLdapDnHelper.AreDistinguishedNamesEqual(
                    detail.DistinguishedName,
                    groupDetail.DistinguishedName)
                || string.Equals(detail.Id, groupDetail.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(candidateType, "group", StringComparison.OrdinalIgnoreCase)
                && !detail.IsSecurityGroup)
            {
                continue;
            }

            if (!IsMemberUnderAllowedSearchBase(connection, detail))
            {
                continue;
            }

            seenDns.Add(detail.DistinguishedName);
            items.Add(new AdGroupMemberCandidateItem(
                detail.Id,
                detail.Type,
                detail.DisplayName,
                detail.Name,
                detail.Cn,
                detail.SamAccountName,
                detail.UserPrincipalName,
                detail.DNSHostName,
                detail.Description,
                detail.DistinguishedName,
                IsAlreadyDirectMember: false,
                detail.IsEnabled));
        }

        return items;
    }

    private static string? ResolveCandidateSearchBase(
        AdManagementConnectionParameters connection,
        string candidateType) =>
        NormalizeMemberTypeCode(candidateType) switch
        {
            "user" => ResolveUsersRootOu(connection),
            "group" => ResolveRequiredGroupsSearchBaseStatic(connection),
            "computer" => ResolveComputersSearchBase(connection),
            _ => null,
        };

    private static string BuildCandidateFilter(string candidateType, string search) =>
        NormalizeMemberTypeCode(candidateType) switch
        {
            "user" => AdLdapGroupFilterHelper.BuildUserMemberCandidateSearchFilter(search),
            "group" => AdLdapGroupFilterHelper.BuildSecurityGroupMemberCandidateSearchFilter(search),
            "computer" => AdLdapGroupFilterHelper.BuildComputerMemberCandidateSearchFilter(search),
            _ => "(objectClass=*)",
        };

    private static int ClampMemberListPageSize(int pageSize) =>
        pageSize <= 0
            ? AdGroupDirectoryLimits.MemberListDefaultPageSize
            : Math.Min(pageSize, AdGroupDirectoryLimits.MemberListMaxPageSize);

    private static int ClampMemberCandidatePageSize(int pageSize) =>
        pageSize <= 0
            ? AdGroupDirectoryLimits.MemberCandidateDefaultPageSize
            : Math.Min(pageSize, AdGroupDirectoryLimits.MemberCandidateMaxPageSize);

    private static string? NormalizeMemberTypeFilter(string? type)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return NormalizeMemberTypeCode(type) switch
        {
            "user" or "group" or "computer" or "unknown" => NormalizeMemberTypeCode(type),
            _ => null,
        };
    }

    private static List<string> NormalizeCandidateTypes(IReadOnlyList<string> types)
    {
        var normalized = new List<string>();
        foreach (var type in types)
        {
            foreach (var part in type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var code = NormalizeMemberTypeCode(part);
                if (code is "user" or "group" or "computer" && !normalized.Contains(code))
                {
                    normalized.Add(code);
                }
            }
        }

        return normalized;
    }

    private static string NormalizeMemberTypeCode(string type) =>
        type.Trim().ToLowerInvariant() switch
        {
            "user" => "user",
            "group" => "group",
            "computer" => "computer",
            "unknown" => "unknown",
            _ when type.Equals("User", StringComparison.OrdinalIgnoreCase) => "user",
            _ when type.Equals("Group", StringComparison.OrdinalIgnoreCase) => "group",
            _ when type.Equals("Computer", StringComparison.OrdinalIgnoreCase) => "computer",
            _ when type.Equals("Unknown", StringComparison.OrdinalIgnoreCase) => "unknown",
            _ => type.Trim().ToLowerInvariant(),
        };

    private static AdGroupMembersListResult GroupMembersListConnectionFailed() =>
        new(false, AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.QueryFailed), null, AdDirectoryFailureKind.ConnectionFailed);

    private static AdGroupMemberCandidatesResult MemberCandidatesConnectionFailed() =>
        new(false, AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.QueryFailed), null, AdDirectoryFailureKind.ConnectionFailed);

    private sealed class MemberValidationResult
    {
        public bool IsValid { get; init; }
        public string UserMessage { get; init; } = string.Empty;
        public string NormalizedReason { get; init; } = string.Empty;
        public string EnglishMessage { get; init; } = string.Empty;
        public AdDirectoryFailureKind FailureKind { get; init; }

        public static MemberValidationResult Valid() => new() { IsValid = true };

        public static MemberValidationResult Invalid(
            string userMessage,
            string normalizedReason,
            string englishMessage,
            AdDirectoryFailureKind failureKind) =>
            new()
            {
                IsValid = false,
                UserMessage = userMessage,
                NormalizedReason = normalizedReason,
                EnglishMessage = englishMessage,
                FailureKind = failureKind,
            };
    }
}
