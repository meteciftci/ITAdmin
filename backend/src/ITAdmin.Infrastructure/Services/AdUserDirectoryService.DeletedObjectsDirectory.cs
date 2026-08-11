using System.Collections;
using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdDeletedObjectsDirectoryService(
    IAdManagementSettingsService settingsServiceDependency,
    IAdAttributeMappingService attributeMappingServiceDependency,
    IAdOperationLogService adOperationLogServiceDependency,
    IAuditLogWriter auditLogWriterDependency,
    IAdManagementNotificationEnqueueService notificationEnqueueServiceDependency,
    IAdDeletedObjectRestoreCommandRunner deletedObjectRestoreCommandRunnerDependency,
    ILogger<AdDeletedObjectsDirectoryService> loggerDependency)
    : AdDirectoryServiceBase(
        settingsServiceDependency,
        attributeMappingServiceDependency,
        adOperationLogServiceDependency,
        auditLogWriterDependency,
        notificationEnqueueServiceDependency,
        deletedObjectRestoreCommandRunnerDependency,
        loggerDependency),
        IAdDeletedObjectDirectoryService
{
    private const int DeletedObjectMemberOfLimit = 25;

    private static readonly string[] DeletedObjectListAttributes =
    [
        "objectGUID",
        "objectClass",
        "name",
        "displayName",
        "sAMAccountName",
        "userPrincipalName",
        "distinguishedName",
        "lastKnownParent",
        "whenChanged",
        "whenCreated",
        "isDeleted",
        "whenDeleted",
    ];

    private static readonly string[] DeletedObjectDetailAttributes =
    [
        "objectGUID",
        "objectClass",
        "name",
        "displayName",
        "sAMAccountName",
        "userPrincipalName",
        "distinguishedName",
        "lastKnownParent",
        "whenChanged",
        "whenCreated",
        "isDeleted",
        "whenDeleted",
        "cn",
        "description",
        "objectSid",
        "mail",
        "department",
        "dNSHostName",
        "operatingSystem",
        "memberOf",
        "msDS-LastKnownRDN",
    ];

    private static readonly HashSet<string> DetailMappedAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "objectGUID",
        "objectClass",
        "name",
        "displayName",
        "sAMAccountName",
        "userPrincipalName",
        "distinguishedName",
        "lastKnownParent",
        "whenChanged",
        "whenCreated",
        "isDeleted",
        "whenDeleted",
        "cn",
        "description",
        "objectSid",
        "mail",
        "department",
        "dNSHostName",
        "operatingSystem",
        "memberOf",
        "msDS-LastKnownRDN",
    };

    public Task<AdDeletedObjectSearchResult> SearchDeletedObjectsAsync(
        AdDeletedObjectSearchQuery query,
        CancellationToken cancellationToken = default) =>
        SearchDeletedObjectsInternalAsync(query, cancellationToken);

    public Task<AdDeletedObjectDetailResult> GetDeletedObjectByIdAsync(
        Guid objectGuid,
        CancellationToken cancellationToken = default) =>
        GetDeletedObjectByIdInternalAsync(objectGuid, cancellationToken);

    private async Task<AdDeletedObjectSearchResult> SearchDeletedObjectsInternalAsync(
        AdDeletedObjectSearchQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = AdLdapValueConverter.ClampPageSize(query.PageSize, min: 1);
        var pageNumber = AdLdapValueConverter.NormalizePageNumber(query.PageNumber);

        if (!AdLdapDeletedObjectFilterHelper.IsQueryEnabled(query.Search, query.Type, query.IncludeAll))
        {
            return new AdDeletedObjectSearchResult(
                true,
                string.Empty,
                new AdDeletedObjectSearchPage([], pageNumber, pageSize, false));
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdDeletedObjectSearchResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var searchBase = ResolveDeletedObjectsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdDeletedObjectSearchResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context, cancellationToken);
            var filter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectSearchFilter(
                query.Search,
                query.Type,
                query.IncludeAll);
            var items = new List<AdDeletedObjectListItem>(pageSize);
            byte[]? cookie = null;
            var hasNextPage = false;

            for (var currentPage = 1; currentPage <= pageNumber; currentPage++)
            {
                var searchRequest = new SearchRequest(
                    searchBase,
                    filter,
                    SearchScope.Subtree,
                    DeletedObjectListAttributes)
                {
                    TimeLimit = LdapOperationTimeout,
                };

                searchRequest.Controls.Add(
                    new DirectoryControl(
                        AdLdapDeletedObjectFilterHelper.ShowDeletedControlOid,
                        null,
                        true,
                        true));

                var pageControl = new PageResultRequestControl(pageSize)
                {
                    Cookie = cookie,
                };
                searchRequest.Controls.Add(pageControl);

                var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
                if (response.ResultCode != ResultCode.Success)
                {
                    return DeletedObjectListFailure(response.ResultCode);
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
                    if (TryMapDeletedObjectListItem(entry, out var item))
                    {
                        items.Add(item);
                    }
                }
            }

            return new AdDeletedObjectSearchResult(
                true,
                string.Empty,
                new AdDeletedObjectSearchPage(items, pageNumber, pageSize, hasNextPage));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException ex)
        {
            logger.LogWarning(ex, "AD deleted objects search failed");
            return DeletedObjectListConnectionFailed();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AD deleted objects search failed");
            return DeletedObjectListConnectionFailed();
        }
    }

    private async Task<AdDeletedObjectDetailResult> GetDeletedObjectByIdInternalAsync(
        Guid objectGuid,
        CancellationToken cancellationToken)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdDeletedObjectDetailResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var searchBase = ResolveDeletedObjectsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdDeletedObjectDetailResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context, cancellationToken);
            var filter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectGuidFilter(objectGuid);
            var searchRequest = new SearchRequest(
                searchBase,
                filter,
                SearchScope.Subtree,
                DeletedObjectDetailAttributes)
            {
                SizeLimit = 1,
                TimeLimit = LdapOperationTimeout,
            };

            searchRequest.Controls.Add(
                new DirectoryControl(
                    AdLdapDeletedObjectFilterHelper.ShowDeletedControlOid,
                    null,
                    true,
                    true));

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return DeletedObjectDetailFailure(response.ResultCode);
            }

            if (response.Entries.Count == 0)
            {
                return new AdDeletedObjectDetailResult(
                    false,
                    AdManagementApiMessageKeys.DeletedObjects.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            if (!TryMapDeletedObjectDetail(response.Entries[0], out var detail))
            {
                return new AdDeletedObjectDetailResult(
                    false,
                    AdManagementApiMessageKeys.DeletedObjects.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            return new AdDeletedObjectDetailResult(true, string.Empty, detail);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException ex)
        {
            logger.LogWarning(ex, "AD deleted object detail lookup failed for {ObjectGuid}", objectGuid);
            return DeletedObjectDetailConnectionFailed();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AD deleted object detail lookup failed for {ObjectGuid}", objectGuid);
            return DeletedObjectDetailConnectionFailed();
        }
    }

    private static string? ResolveDeletedObjectsSearchBase(AdManagementConnectionParameters connection)
    {
        var namingContext = string.IsNullOrWhiteSpace(connection.DefaultNamingContext)
            ? connection.BaseDn
            : connection.DefaultNamingContext;

        return AdLdapDeletedObjectFilterHelper.ResolveDeletedObjectsSearchBase(namingContext);
    }

    private static bool TryMapDeletedObjectListItem(SearchResultEntry entry, out AdDeletedObjectListItem item)
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

        var objectClasses = GetAllStrings(entry, "objectClass");
        var deletedAt = ResolveDeletedAt(entry);

        item = new AdDeletedObjectListItem(
            objectGuid.ToString("D"),
            ResolveDeletedObjectType(objectClasses),
            GetFirstString(entry, "name"),
            GetFirstString(entry, "displayName"),
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            distinguishedName,
            GetFirstString(entry, "lastKnownParent"),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")),
            deletedAt);

        return true;
    }

    private static bool TryMapDeletedObjectDetail(SearchResultEntry entry, out AdDeletedObjectDetail detail)
    {
        detail = null!;
        if (!TryGetObjectGuid(entry, out var objectGuid))
        {
            return false;
        }

        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var objectClasses = GetAllStrings(entry, "objectClass");
        var memberOf = GetAllStrings(entry, "memberOf");
        var memberOfTruncated = memberOf.Count > DeletedObjectMemberOfLimit;
        var memberOfItems = memberOf.Take(DeletedObjectMemberOfLimit).ToList();
        var deletedAt = ResolveDeletedAt(entry);

        detail = new AdDeletedObjectDetail(
            objectGuid.ToString("D"),
            ResolveDeletedObjectType(objectClasses),
            GetFirstString(entry, "name"),
            GetFirstString(entry, "displayName"),
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            GetFirstString(entry, "description"),
            distinguishedName,
            GetFirstString(entry, "lastKnownParent"),
            GetFirstString(entry, "msDS-LastKnownRDN"),
            objectClasses,
            AdLdapSidHelper.FormatObjectSid(GetFirstBytes(entry, "objectSid")),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenCreated")),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")),
            deletedAt,
            GetFirstString(entry, "mail"),
            GetFirstString(entry, "department"),
            GetFirstString(entry, "dNSHostName"),
            GetFirstString(entry, "operatingSystem"),
            memberOf.Count,
            memberOfItems,
            memberOfTruncated,
            BuildDeletedObjectAdditionalAttributes(entry));

        return true;
    }

    private static DateTimeOffset? ResolveDeletedAt(SearchResultEntry entry) =>
        AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenDeleted"))
        ?? AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged"));

    private static AdDeletedObjectType ResolveDeletedObjectType(IReadOnlyList<string> objectClasses)
    {
        if (objectClasses.Any(static value => value.Equals("computer", StringComparison.OrdinalIgnoreCase)))
        {
            return AdDeletedObjectType.Computer;
        }

        if (objectClasses.Any(static value => value.Equals("group", StringComparison.OrdinalIgnoreCase)))
        {
            return AdDeletedObjectType.Group;
        }

        if (objectClasses.Any(static value => value.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            return AdDeletedObjectType.User;
        }

        return AdDeletedObjectType.Unknown;
    }

    private static IReadOnlyDictionary<string, string> BuildDeletedObjectAdditionalAttributes(
        SearchResultEntry entry)
    {
        var additionalAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (DictionaryEntry kv in entry.Attributes)
        {
            var attributeName = kv.Key.ToString();
            if (string.IsNullOrWhiteSpace(attributeName)
                || DetailMappedAttributeNames.Contains(attributeName)
                || AdLdapAttributeCatalog.IsSensitiveAttributeName(attributeName))
            {
                continue;
            }

            var values = GetAllStrings(entry, attributeName);
            if (values.Count == 0)
            {
                continue;
            }

            additionalAttributes[attributeName] = string.Join("; ", values);
        }

        return additionalAttributes;
    }

    private static AdDeletedObjectSearchResult DeletedObjectListConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.DeletedObjects.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdDeletedObjectDetailResult DeletedObjectDetailConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.DeletedObjects.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private AdDeletedObjectSearchResult DeletedObjectListFailure(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.InsufficientAccessRights or ResultCode.UnwillingToPerform =>
                new AdDeletedObjectSearchResult(
                    false,
                    AdManagementApiMessageKeys.DeletedObjects.AccessDenied,
                    null,
                    AdDirectoryFailureKind.ConnectionFailed),
            _ => DeletedObjectListConnectionFailed(),
        };

    private AdDeletedObjectDetailResult DeletedObjectDetailFailure(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.InsufficientAccessRights or ResultCode.UnwillingToPerform =>
                new AdDeletedObjectDetailResult(
                    false,
                    AdManagementApiMessageKeys.DeletedObjects.AccessDenied,
                    null,
                    AdDirectoryFailureKind.ConnectionFailed),
            _ => DeletedObjectDetailConnectionFailed(),
        };
}
