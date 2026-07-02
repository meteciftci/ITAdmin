using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUsersDirectoryService(
    IAdManagementSettingsService settingsServiceDependency,
    IAdAttributeMappingService attributeMappingServiceDependency,
    IAdOperationLogService adOperationLogServiceDependency,
    IAuditLogWriter auditLogWriterDependency,
    IAdManagementNotificationEnqueueService notificationEnqueueServiceDependency,
    IAdDeletedObjectRestoreCommandRunner deletedObjectRestoreCommandRunnerDependency,
    ILogger<AdUsersDirectoryService> loggerDependency)
    : AdDirectoryServiceBase(
        settingsServiceDependency,
        attributeMappingServiceDependency,
        adOperationLogServiceDependency,
        auditLogWriterDependency,
        notificationEnqueueServiceDependency,
        deletedObjectRestoreCommandRunnerDependency,
        loggerDependency),
        IAdUserDirectoryService,
        IAdUserAccountOperationService,
        IAdUserGroupMembershipService,
        IAdUserOuMoveService,
        IAdUserManagerUpdateService,
        IAdUserAccountExpirationUpdateService
{
    public async Task<AdUserDirectorySearchResult> SearchUsersAsync(
        AdUserSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageSize = AdLdapValueConverter.ClampPageSize(query.PageSize);
        var pageNumber = AdLdapValueConverter.NormalizePageNumber(query.PageNumber);

        if (!AdLdapAttributeCatalog.IsSearchTermValid(query.Search))
        {
            return new AdUserDirectorySearchResult(
                true,
                string.Empty,
                new AdUserSearchPage([], pageNumber, pageSize, false));
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdUserDirectorySearchResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var mappings = await attributeMappingService.GetMappingsAsync(cancellationToken);
        var context = connectionResult.Context;
        var searchBase = ResolveListSearchBase(context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdUserDirectorySearchResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            var searchableMappings = AdLdapAttributeCatalog.GetSearchableMappingAttributeNames(mappings);
            var filter = AdLdapAttributeCatalog.BuildUserSearchFilter(
                query.Search!.Trim(),
                query.Status,
                searchableMappings);
            var listAttributes = AdLdapAttributeCatalog.BuildListLdapAttributeNames(mappings);
            var items = new List<AdUserListItem>(pageSize);
            byte[]? cookie = null;
            var hasNextPage = false;

            for (var currentPage = 1; currentPage <= pageNumber; currentPage++)
            {
                var searchRequest = new SearchRequest(
                    searchBase,
                    filter,
                    SearchScope.Subtree,
                    listAttributes)
                {
                    TimeLimit = LdapOperationTimeout,
                };

                var pageControl = new PageResultRequestControl(pageSize)
                {
                    Cookie = cookie,
                };
                searchRequest.Controls.Add(pageControl);

                var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
                if (response.ResultCode != ResultCode.Success)
                {
                    return ConnectionFailed();
                }

                var pageResponse = response.Controls
                    .OfType<PageResultResponseControl>()
                    .FirstOrDefault();
                cookie = pageResponse?.Cookie;
                hasNextPage = cookie is { Length: > 0 };

                if (currentPage != pageNumber)
                {
                    continue;
                }

                foreach (SearchResultEntry entry in response.Entries)
                {
                    if (TryMapListItem(entry, out var item))
                    {
                        items.Add(item);
                    }
                }
            }

            return new AdUserDirectorySearchResult(
                true,
                string.Empty,
                new AdUserSearchPage(items, pageNumber, pageSize, hasNextPage));
        }
        catch (LdapException)
        {
            return ConnectionFailed();
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return ConnectionFailed();
        }
    }

    public async Task<AdUserDirectoryDetailResult> GetUserByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdUserDirectoryDetailResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var mappings = await attributeMappingService.GetMappingsAsync(cancellationToken);
        var activeMappings = mappings.Where(static mapping => mapping.IsEnabled).ToList();
        var context = connectionResult.Context;
        var searchBase = ResolveDetailSearchBase(context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdUserDirectoryDetailResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(id);
            var filter =
                $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))(objectGUID={guidFilter}))";

            var detailAttributes = AdLdapAttributeCatalog.BuildDetailLdapAttributeNames(activeMappings);
            var searchRequest = new SearchRequest(
                searchBase,
                filter,
                SearchScope.Subtree,
                detailAttributes)
            {
                SizeLimit = 2,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return ConnectionFailedDetail();
            }

            if (response.Entries.Count == 0)
            {
                return new AdUserDirectoryDetailResult(
                    false,
                    AdManagementApiMessageKeys.Users.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            if (!TryMapDetailItem(response.Entries[0], activeMappings, out var detail))
            {
                return new AdUserDirectoryDetailResult(
                    false,
                    AdManagementApiMessageKeys.Users.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            detail = TryEnrichDetailWithResolvedManager(ldapConnection, detail);

            return new AdUserDirectoryDetailResult(true, string.Empty, detail);
        }
        catch (LdapException)
        {
            return ConnectionFailedDetail();
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return ConnectionFailedDetail();
        }
    }

    private static string? ResolveListSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.UsersRootOu)
            ? connection.DefaultNamingContext ?? connection.BaseDn
            : connection.UsersRootOu;

    private static string? ResolveDetailSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.DefaultNamingContext)
            ? connection.BaseDn
            : connection.DefaultNamingContext;

    private static bool TryMapListItem(SearchResultEntry entry, out AdUserListItem item)
    {
        item = null!;
        if (!TryGetObjectGuid(entry, out var objectGuid))
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

        item = new AdUserListItem(
            objectGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            GetFirstString(entry, "displayName"),
            GetFirstString(entry, "mail"),
            GetFirstString(entry, "department"),
            AdLdapValueConverter.IsAccountEnabled(userAccountControl),
            AdLdapValueConverter.IsAccountLockedOut(lockoutTime),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenCreated")),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")),
            AdLdapValueConverter.FromAdFileTime(GetFirstLong(entry, "lastLogonTimestamp")));

        return true;
    }

    private static bool TryMapDetailItem(
        SearchResultEntry entry,
        IReadOnlyList<AdAttributeMappingItem> activeMappings,
        out AdUserDetail detail)
    {
        detail = null!;
        if (!TryMapListItem(entry, out var listItem))
        {
            return false;
        }

        var groups = AdLdapDnHelper.BuildGroupMemberships(GetAllStrings(entry, "memberOf"));

        var mappedAttributes = BuildMappedAttributes(entry, activeMappings);

        var userAccountControl = GetFirstInt(entry, "userAccountControl");
        var lockoutTime = GetFirstLong(entry, "lockoutTime");
        var lastLogonTimestampAt = AdLdapValueConverter.FromAdFileTime(
            GetFirstLong(entry, "lastLogonTimestamp"));

        var managerDistinguishedName = GetFirstString(entry, "manager");
        var accountExpiresRaw = GetFirstLong(entry, "accountExpires");

        detail = new AdUserDetail(
            listItem.Id,
            listItem.DistinguishedName,
            listItem.SamAccountName,
            listItem.UserPrincipalName,
            listItem.DisplayName,
            listItem.Mail,
            GetFirstString(entry, "givenName"),
            GetFirstString(entry, "sn"),
            listItem.Department,
            listItem.IsEnabled,
            listItem.IsLockedOut,
            AdLdapValueConverter.FromAdFileTime(GetFirstLong(entry, "pwdLastSet")),
            listItem.LastLogonAt,
            listItem.WhenCreated,
            listItem.WhenChanged,
            userAccountControl,
            AdLdapValueConverter.FromAdFileTime(accountExpiresRaw),
            AdAccountExpirationDateConverter.ToDisplayDateString(accountExpiresRaw),
            AdLdapValueConverter.FromAdFileTime(lockoutTime),
            GetFirstInt(entry, "badPwdCount"),
            AdLdapValueConverter.FromAdFileTime(GetFirstLong(entry, "badPasswordTime")),
            lastLogonTimestampAt,
            groups,
            mappedAttributes,
            managerDistinguishedName);

        return true;
    }

    private AdUserDetail TryEnrichDetailWithResolvedManager(LdapConnection ldapConnection, AdUserDetail detail)
    {
        if (string.IsNullOrWhiteSpace(detail.ManagerDistinguishedName))
        {
            return detail;
        }

        try
        {
            if (!TryResolveManagerByDistinguishedName(
                    ldapConnection,
                    detail.ManagerDistinguishedName,
                    out var managerId,
                    out var managerSamAccountName,
                    out var managerUserPrincipalName,
                    out var managerDisplayName))
            {
                return detail;
            }

            return detail with
            {
                ManagerId = managerId,
                ManagerSamAccountName = managerSamAccountName,
                ManagerUserPrincipalName = managerUserPrincipalName,
                ManagerDisplayName = managerDisplayName,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AD user manager resolve failed for user {UserId}",
                detail.Id);
            return detail;
        }
    }

    private static bool TryResolveManagerByDistinguishedName(
        LdapConnection ldapConnection,
        string managerDistinguishedName,
        out string? managerId,
        out string? managerSamAccountName,
        out string? managerUserPrincipalName,
        out string? managerDisplayName)
    {
        managerId = null;
        managerSamAccountName = null;
        managerUserPrincipalName = null;
        managerDisplayName = null;

        var searchRequest = new SearchRequest(
            managerDistinguishedName.Trim(),
            "(&(objectCategory=person)(objectClass=user))",
            SearchScope.Base,
            "objectGUID",
            "sAMAccountName",
            "userPrincipalName",
            "displayName")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        var entry = response.Entries[0];
        if (!TryGetObjectGuid(entry, out var objectGuid))
        {
            return false;
        }

        managerId = objectGuid.ToString("D");
        managerSamAccountName = GetFirstString(entry, "sAMAccountName");
        managerUserPrincipalName = GetFirstString(entry, "userPrincipalName");
        managerDisplayName = GetFirstString(entry, "displayName");
        return true;
    }

    private static IReadOnlyList<MappedAdUserAttribute> BuildMappedAttributes(
        SearchResultEntry entry,
        IReadOnlyList<AdAttributeMappingItem> activeMappings) =>
        AdUserMappedAttributeBuilder.Build(
            attributeName => GetAllStrings(entry, attributeName),
            activeMappings);

    private static AdUserDirectorySearchResult ConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.Users.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdUserDirectoryDetailResult ConnectionFailedDetail() =>
        new(
            false,
            AdManagementApiMessageKeys.Users.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);
}
