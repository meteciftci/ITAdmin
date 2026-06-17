using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string CreateGroupSuccessLoggingFailedMessage =
        "AD group create operation succeeded but logging failed.";
    private const string CreateGroupFailureLoggingFailedMessage =
        "AD group create operation failed but logging failed.";
    private const int GroupOuSearchDefaultPageSize = 50;
    private const int GroupOuSearchMaxPageSize = 100;

    public Task<AdOrganizationalUnitSearchResult> SearchGroupOrganizationalUnitsAsync(
        AdOrganizationalUnitSearchQuery query,
        CancellationToken cancellationToken = default) =>
        SearchGroupOrganizationalUnitsInternalAsync(query, cancellationToken);

    public async Task<CreateAdGroupResult> CreateGroupAsync(
        CreateAdGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeCreateGroupRequest(request);

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new CreateAdGroupResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (!AdCreateGroupRequestValidator.TryValidate(
                normalizedRequest,
                groupsSearchBase,
                out var validationMessage))
        {
            return new CreateAdGroupResult(
                false,
                validationMessage,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        if (!AdGroupTypeHelper.TryParseScopeCode(normalizedRequest.GroupScope, out var groupScope))
        {
            return new CreateAdGroupResult(
                false,
                AdManagementApiMessageKeys.Groups.InvalidGroupScope,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var connection = connectionResult.Context.Connection;

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var domainSearchBase = connection.DefaultNamingContext ?? connection.BaseDn;
            if (string.IsNullOrWhiteSpace(domainSearchBase))
            {
                return new CreateAdGroupResult(
                    false,
                    AdManagementApiMessageKeys.Common.NotConfigured,
                    null,
                    AdDirectoryFailureKind.NotConfigured);
            }

            var commonName = AdGroupNameNormalizer.NormalizeTechnicalName(normalizedRequest.Name);
            var samAccountName = normalizedRequest.SamAccountName.Trim();
            var targetOu = normalizedRequest.TargetOuDistinguishedName.Trim();

            var preflightFailure = RunCreateGroupPreflightChecks(
                ldapConnection,
                domainSearchBase,
                targetOu,
                commonName,
                samAccountName);
            if (preflightFailure is not null)
            {
                await WriteCreateGroupFailureLogsAsync(
        normalizedRequest,
                    preflightFailure.UserMessage,
                    connection,
                    step: "Preflight",
                    cancellationToken);
                return new CreateAdGroupResult(
                    false,
                    preflightFailure.UserMessage,
                    null,
                    AdDirectoryFailureKind.InvalidRequest);
            }

            var distinguishedName = AdLdapDnHelper.BuildUserDistinguishedName(commonName, targetOu);
            var groupType = AdGroupTypeHelper.BuildSecurityGroupType(groupScope);

            try
            {
                ExecuteAddGroup(
                    ldapConnection,
                    distinguishedName,
                    normalizedRequest,
                    commonName,
                    samAccountName,
                    groupType);
            }
            catch (CreateGroupLdapException ex)
            {
                await WriteCreateGroupFailureLogsAsync(
        normalizedRequest,
                    ex.UserMessage,
                    connection,
                    step: "CreateGroup",
                    cancellationToken);
                return new CreateAdGroupResult(
                    false,
                    ex.UserMessage,
                    null,
                    ex.FailureKind);
            }

            if (!TryGetObjectGuid(ldapConnection, distinguishedName, out var objectGuid))
            {
                await WriteCreateGroupFailureLogsAsync(
        normalizedRequest,
                    AdManagementApiMessageKeys.Groups.CreateFailed,
                    connection,
                    step: "ReloadGroup",
                    cancellationToken);
                return new CreateAdGroupResult(
                    false,
                    AdManagementApiMessageKeys.Groups.CreateFailed,
                    null,
                    AdDirectoryFailureKind.ConnectionFailed);
            }

            var detailResult = await GetGroupByIdAsync(objectGuid, cancellationToken);
            if (!detailResult.IsSuccess || detailResult.Group is null)
            {
                await WriteCreateGroupFailureLogsAsync(
        normalizedRequest,
                    AdManagementApiMessageKeys.Groups.CreateFailed,
                    connection,
                    step: "ReloadGroup",
                    cancellationToken);
                return new CreateAdGroupResult(
                    false,
                    AdManagementApiMessageKeys.Groups.CreateFailed,
                    null,
                    detailResult.FailureKind ?? AdDirectoryFailureKind.ConnectionFailed);
            }

            await WriteCreateGroupSuccessLogsAsync(
                normalizedRequest,
                detailResult.Group,
                connection,
                cancellationToken);

            return new CreateAdGroupResult(true, string.Empty, detailResult.Group);
        }
        catch (LdapException ex)
        {
            await WriteCreateGroupFailureLogsAsync(
        normalizedRequest,
                AdLdapErrorNormalizer.NormalizeMessageKey(ex.ErrorCode, ex.Message),
                connection,
                step: "CreateGroup",
                cancellationToken);
            return new CreateAdGroupResult(
                false,
                AdLdapErrorNormalizer.NormalizeMessageKey(ex.ErrorCode, ex.Message),
                null,
                AdDirectoryFailureKind.ConnectionFailed);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD group create unexpected failure. ActorUserId={ActorUserId}",
                normalizedRequest.ActorUserId);
            await WriteCreateGroupFailureLogsAsync(
        normalizedRequest,
                AdManagementApiMessageKeys.Groups.CreateFailed,
                connection,
                step: "CreateGroup",
                cancellationToken);
            return new CreateAdGroupResult(
                false,
                AdManagementApiMessageKeys.Groups.CreateFailed,
                null,
                AdDirectoryFailureKind.ConnectionFailed);
        }
    }

    private async Task<AdOrganizationalUnitSearchResult> SearchGroupOrganizationalUnitsInternalAsync(
        AdOrganizationalUnitSearchQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = query.PageSize <= 0
            ? GroupOuSearchDefaultPageSize
            : Math.Min(query.PageSize, GroupOuSearchMaxPageSize);

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdOrganizationalUnitSearchResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdOrganizationalUnitSearchResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var filter = BuildOrganizationalUnitSearchFilter(query.Search);
            var searchRequest = new SearchRequest(
                groupsSearchBase,
                filter,
                SearchScope.Subtree,
                "distinguishedName",
                "displayName",
                "name",
                "ou")
            {
                TimeLimit = LdapOperationTimeout,
            };

            var pageControl = new PageResultRequestControl(pageSize + 1);
            searchRequest.Controls.Add(pageControl);

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return GroupOuConnectionFailed();
            }

            var items = new List<AdOrganizationalUnitListItem>();
            foreach (SearchResultEntry entry in response.Entries)
            {
                if (!TryMapOrganizationalUnit(entry, out var item))
                {
                    continue;
                }

                items.Add(item);
                if (items.Count > pageSize)
                {
                    break;
                }
            }

            var hasMore = items.Count > pageSize;
            if (hasMore)
            {
                items.RemoveAt(items.Count - 1);
            }

            return new AdOrganizationalUnitSearchResult(
                true,
                string.Empty,
                new AdOrganizationalUnitSearchPage(items, hasMore));
        }
        catch (LdapException)
        {
            return GroupOuConnectionFailed();
        }
        catch (Exception)
        {
            return GroupOuConnectionFailed();
        }
    }

    private static CreateAdGroupRequest NormalizeCreateGroupRequest(CreateAdGroupRequest request) =>
        request with
        {
            DisplayName = request.DisplayName.Trim(),
            Name = AdGroupNameNormalizer.NormalizeTechnicalName(request.Name),
            SamAccountName = request.SamAccountName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            GroupScope = request.GroupScope.Trim(),
            TargetOuDistinguishedName = request.TargetOuDistinguishedName.Trim(),
        };

    private sealed record CreateGroupPreflightFailure(string UserMessage);

    private static CreateGroupPreflightFailure? RunCreateGroupPreflightChecks(
        LdapConnection ldapConnection,
        string domainSearchBase,
        string targetOuDistinguishedName,
        string commonName,
        string samAccountName)
    {
        if (HasDuplicateGroupCnInParentOu(ldapConnection, targetOuDistinguishedName, commonName))
        {
            return new CreateGroupPreflightFailure(AdManagementApiMessageKeys.OperationFailures.PreflightGroupCnDuplicate);
        }

        if (HasDuplicateGroupSamAccountName(ldapConnection, domainSearchBase, samAccountName))
        {
            return new CreateGroupPreflightFailure(AdManagementApiMessageKeys.OperationFailures.PreflightGroupSamAccountNameDuplicate);
        }

        return null;
    }

    private static bool HasDuplicateGroupCnInParentOu(
        LdapConnection ldapConnection,
        string parentDistinguishedName,
        string commonName)
    {
        var escapedCn = AdLdapFilterHelper.EscapeFilterValue(commonName);
        var filter =
            $"(&(objectCategory=group)(objectClass=group)(cn={escapedCn}))";
        return ExistsForGroupPreflight(ldapConnection, parentDistinguishedName, filter, SearchScope.OneLevel);
    }

    private static bool HasDuplicateGroupSamAccountName(
        LdapConnection ldapConnection,
        string searchBase,
        string samAccountName)
    {
        var escapedSam = AdLdapFilterHelper.EscapeFilterValue(samAccountName);
        var filter =
            $"(&(objectCategory=group)(objectClass=group)(sAMAccountName={escapedSam}))";
        return ExistsForGroupPreflight(ldapConnection, searchBase, filter, SearchScope.Subtree);
    }

    private static bool ExistsForGroupPreflight(
        LdapConnection ldapConnection,
        string searchBase,
        string filter,
        SearchScope scope)
    {
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            scope,
            "objectGUID",
            "distinguishedName")
        {
            SizeLimit = 2,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        return response.ResultCode == ResultCode.Success && response.Entries.Count > 0;
    }

    private static void ExecuteAddGroup(
        LdapConnection ldapConnection,
        string distinguishedName,
        CreateAdGroupRequest request,
        string commonName,
        string samAccountName,
        int groupType)
    {
        var addRequest = new AddRequest(distinguishedName);
        addRequest.Attributes.Add(new DirectoryAttribute("objectClass", "top", "group"));
        addRequest.Attributes.Add(new DirectoryAttribute("cn", commonName));
        addRequest.Attributes.Add(new DirectoryAttribute("name", commonName));
        addRequest.Attributes.Add(new DirectoryAttribute("displayName", request.DisplayName.Trim()));
        addRequest.Attributes.Add(new DirectoryAttribute("sAMAccountName", samAccountName));
        addRequest.Attributes.Add(new DirectoryAttribute("groupType", groupType.ToString()));

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            addRequest.Attributes.Add(new DirectoryAttribute("description", request.Description.Trim()));
        }

        var response = (DirectoryResponse)ldapConnection.SendRequest(addRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new CreateGroupLdapException(
                AdLdapErrorNormalizer.NormalizeMessageKey((int)response.ResultCode, response.ErrorMessage),
                MapGroupFailureKind(response.ResultCode));
        }
    }

    private static bool TryGetObjectGuid(
        LdapConnection ldapConnection,
        string distinguishedName,
        out Guid objectGuid)
    {
        objectGuid = Guid.Empty;
        var searchRequest = new SearchRequest(
            distinguishedName,
            "(objectClass=*)",
            SearchScope.Base,
            "objectGUID")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        return TryGetObjectGuid(response.Entries[0], out objectGuid);
    }

    private static AdDirectoryFailureKind MapGroupFailureKind(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdDirectoryFailureKind.NotFound,
            ResultCode.EntryAlreadyExists
                or ResultCode.AttributeOrValueExists
                or ResultCode.ConstraintViolation
                or ResultCode.InvalidDNSyntax
                or ResultCode.NamingViolation
                or ResultCode.UnwillingToPerform => AdDirectoryFailureKind.InvalidRequest,
            _ => AdDirectoryFailureKind.ConnectionFailed,
        };

    private static AdOrganizationalUnitSearchResult GroupOuConnectionFailed() =>
        new(false, AdLdapErrorNormalizer.ConnectionFailedMessage, null, AdDirectoryFailureKind.ConnectionFailed);

    private async Task WriteCreateGroupSuccessLogsAsync(
        CreateAdGroupRequest request,
        AdGroupDetail group,
        AdManagementConnectionParameters connection,
        CancellationToken cancellationToken)
    {
        var requestSummary = AdOperationLogSnapshotBuilder.BuildGroupCreateRequestSummary(request);
        var afterSnapshot = AdOperationLogSnapshotBuilder.BuildGroupCreateAfterSnapshot(group);

        try
        {
            await adOperationLogService.WriteAsync(
                new AdOperationLogEntry
                {
                    OperationType = AdManagementOperationTypes.GroupCreate,
                    Status = AdManagementOperationStatuses.Succeeded,
                    TargetObjectType = AdManagementTargetGroupTypes.AdGroup,
                    TargetDistinguishedName = group.DistinguishedName,
                    TargetObjectGuid = group.Id,
                    TargetSamAccountName = group.SamAccountName,
                    RequestSummaryJson = requestSummary,
                    BeforeSnapshotJson = null,
                    AfterSnapshotJson = afterSnapshot,
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                    DomainController = ResolvePrimaryHost(connection),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} SamAccountName={SamAccountName} GroupId={GroupId} ActorUserId={ActorUserId}",
                CreateGroupSuccessLoggingFailedMessage,
                group.SamAccountName,
                group.Id,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Create",
                    EntityName = "AdGroup",
                    EntityId = group.Id,
                    Description = "AD security group created.",
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
                "{LogMessage} SamAccountName={SamAccountName} GroupId={GroupId} ActorUserId={ActorUserId}",
                CreateGroupSuccessLoggingFailedMessage,
                group.SamAccountName,
                group.Id,
                request.ActorUserId);
        }
    }

    private async Task WriteCreateGroupFailureLogsAsync(
        CreateAdGroupRequest request,
        string message,
        AdManagementConnectionParameters connection,
        string step,
        CancellationToken cancellationToken)
    {
        var requestSummary = AdOperationLogSnapshotBuilder.BuildGroupCreateRequestSummary(request);
        var diagnosticJson = AdOperationErrorDiagnosticBuilder.BuildCreateGroupFailureJson(
            step,
            englishMessageOverride: ResolveCreateGroupFailureEnglishMessage(message),
            normalizedReasonOverride: ResolveCreateGroupFailureReason(message));

        try
        {
            await adOperationLogService.WriteAsync(
                new AdOperationLogEntry
                {
                    OperationType = AdManagementOperationTypes.GroupCreate,
                    Status = AdManagementOperationStatuses.Failed,
                    TargetObjectType = AdManagementTargetGroupTypes.AdGroup,
                    TargetDistinguishedName = request.TargetOuDistinguishedName,
                    ErrorCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson),
                    ErrorMessage = diagnosticJson,
                    RequestSummaryJson = requestSummary,
                    BeforeSnapshotJson = null,
                    AfterSnapshotJson = null,
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                    DomainController = ResolvePrimaryHost(connection),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                CreateGroupFailureLoggingFailedMessage,
                request.SamAccountName,
                request.ActorUserId);
        }
    }

    private static string ResolveCreateGroupFailureEnglishMessage(string message) =>
        message switch
        {
            _ when string.Equals(message, AdManagementApiMessageKeys.OperationFailures.PreflightGroupCnDuplicate, StringComparison.Ordinal) =>
                "A group with the same technical name already exists in the target OU.",
            _ when string.Equals(message, AdManagementApiMessageKeys.OperationFailures.PreflightGroupSamAccountNameDuplicate, StringComparison.Ordinal) =>
                "The sAMAccountName value is already used by another AD group.",
            _ when string.Equals(message, AdManagementApiMessageKeys.Users.InvalidTargetOu, StringComparison.Ordinal) =>
                "The selected OU is not valid for group creation.",
            _ => "The AD security group could not be created.",
        };

    private static string ResolveCreateGroupFailureReason(string message) =>
        message switch
        {
            _ when string.Equals(message, AdManagementApiMessageKeys.OperationFailures.PreflightGroupCnDuplicate, StringComparison.Ordinal)
                || string.Equals(message, AdManagementApiMessageKeys.OperationFailures.PreflightGroupSamAccountNameDuplicate, StringComparison.Ordinal) =>
                AdUserUpdateNormalizedReasons.DuplicateValue,
            _ when string.Equals(message, AdManagementApiMessageKeys.Users.InvalidTargetOu, StringComparison.Ordinal) =>
                AdUserUpdateNormalizedReasons.InvalidDnSyntax,
            _ => AdUserUpdateNormalizedReasons.Unknown,
        };

    private sealed class CreateGroupLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind) : Exception(userMessage)
    {
        public string UserMessage { get; } = userMessage;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
    }
}
