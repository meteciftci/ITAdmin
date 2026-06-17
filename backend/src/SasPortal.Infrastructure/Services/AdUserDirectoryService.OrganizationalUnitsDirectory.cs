using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdOrganizationalUnitDirectoryService
{
    private static readonly string[] OrganizationalUnitListAttributes =
    [
        "objectGUID",
        "distinguishedName",
        "displayName",
        "name",
        "ou",
    ];

    private static readonly string[] OrganizationalUnitDetailAttributes =
    [
        "objectGUID",
        "distinguishedName",
        "displayName",
        "name",
        "ou",
    ];

    private const string ChildOrganizationalUnitFilter = "(objectClass=organizationalUnit)";
    private const string ChildUserFilter = "(&(objectCategory=person)(objectClass=user))";
    private const string ChildGroupFilter = "(objectClass=group)";
    private const string ChildComputerFilter = "(objectClass=computer)";

    public Task<AdOrganizationalUnitManageListResult> SearchManageOrganizationalUnitsAsync(
        AdOrganizationalUnitManageListQuery query,
        CancellationToken cancellationToken = default) =>
        SearchManageOrganizationalUnitsInternalAsync(query, cancellationToken);

    public Task<AdOrganizationalUnitDetailResult> GetOrganizationalUnitByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetManageOrganizationalUnitByIdInternalAsync(id, cancellationToken);

    private async Task<AdOrganizationalUnitManageListResult> SearchManageOrganizationalUnitsInternalAsync(
        AdOrganizationalUnitManageListQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = AdLdapValueConverter.ClampPageSize(query.PageSize);
        var pageNumber = AdLdapValueConverter.NormalizePageNumber(query.PageNumber);

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdOrganizationalUnitManageListResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var searchBase = ResolveOrganizationalUnitsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdOrganizationalUnitManageListResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        if (!string.IsNullOrWhiteSpace(query.Search)
            && !AdLdapAttributeCatalog.IsSearchTermValid(query.Search))
        {
            return new AdOrganizationalUnitManageListResult(
                true,
                string.Empty,
                new AdOrganizationalUnitManagePage([], pageNumber, pageSize, false));
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var filter = BuildOrganizationalUnitSearchFilter(query.Search);
            var items = new List<AdOrganizationalUnitManageListItem>(pageSize);
            byte[]? cookie = null;
            var hasNextPage = false;

            for (var currentPage = 1; currentPage <= pageNumber; currentPage++)
            {
                var searchRequest = new SearchRequest(
                    searchBase,
                    filter,
                    SearchScope.Subtree,
                    OrganizationalUnitListAttributes)
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
                    return OrganizationalUnitListConnectionFailed();
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
                    if (TryMapOrganizationalUnitManageListItem(ldapConnection, entry, out var item))
                    {
                        items.Add(item);
                    }
                }
            }

            return new AdOrganizationalUnitManageListResult(
                true,
                string.Empty,
                new AdOrganizationalUnitManagePage(items, pageNumber, pageSize, hasNextPage));
        }
        catch (LdapException)
        {
            return OrganizationalUnitListConnectionFailed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AD organizational unit list query failed.");
            return OrganizationalUnitListConnectionFailed();
        }
    }

    private async Task<AdOrganizationalUnitDetailResult> GetManageOrganizationalUnitByIdInternalAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdOrganizationalUnitDetailResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var searchBase = ResolveOrganizationalUnitsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdOrganizationalUnitDetailResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadOrganizationalUnitDetail(
                    ldapConnection,
                    searchBase,
                    id,
                    out var detail))
            {
                return new AdOrganizationalUnitDetailResult(
                    false,
                    AdManagementApiMessageKeys.OrganizationalUnits.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            return new AdOrganizationalUnitDetailResult(true, string.Empty, detail);
        }
        catch (LdapException)
        {
            return OrganizationalUnitDetailConnectionFailed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AD organizational unit detail query failed. Id={OrganizationalUnitId}", id);
            return OrganizationalUnitDetailConnectionFailed();
        }
    }

    private static string? ResolveOrganizationalUnitsSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.DefaultNamingContext)
            ? connection.BaseDn
            : connection.DefaultNamingContext;

    private static AdOrganizationalUnitManageListResult OrganizationalUnitListConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.OrganizationalUnits.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdOrganizationalUnitDetailResult OrganizationalUnitDetailConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.OrganizationalUnits.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private bool TryMapOrganizationalUnitManageListItem(
        LdapConnection ldapConnection,
        SearchResultEntry entry,
        out AdOrganizationalUnitManageListItem item)
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

        var displayName = GetFirstString(entry, "displayName");
        var name = GetFirstString(entry, "name");
        var ou = GetFirstString(entry, "ou");
        var parentDistinguishedName = AdLdapDnHelper.GetParentDistinguishedName(distinguishedName);
        var contentSummary = CountOrganizationalUnitChildren(ldapConnection, distinguishedName);

        item = new AdOrganizationalUnitManageListItem(
            objectGuid.ToString("D"),
            name,
            ou,
            distinguishedName,
            parentDistinguishedName,
            AdOrganizationalUnitCanonicalNameBuilder.Build(distinguishedName),
            contentSummary.ChildOuCount,
            contentSummary.UserCount,
            contentSummary.GroupCount,
            contentSummary.ComputerCount);
        return true;
    }

    private bool TryLoadOrganizationalUnitDetail(
        LdapConnection ldapConnection,
        string searchBase,
        Guid id,
        out AdOrganizationalUnitDetail detail)
    {
        detail = null!;
        var filter = BuildOrganizationalUnitObjectGuidFilter(id);
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            OrganizationalUnitDetailAttributes)
        {
            SizeLimit = 2,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        if (!TryMapOrganizationalUnitDetail(ldapConnection, response.Entries[0], out detail))
        {
            return false;
        }

        var childItems = LoadChildOrganizationalUnits(ldapConnection, detail.DistinguishedName);
        detail = detail with { ChildOrganizationalUnits = childItems };
        return true;
    }

    private bool TryMapOrganizationalUnitDetail(
        LdapConnection ldapConnection,
        SearchResultEntry entry,
        out AdOrganizationalUnitDetail detail)
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

        var displayName = GetFirstString(entry, "displayName");
        var name = GetFirstString(entry, "name");
        var ou = GetFirstString(entry, "ou");
        var parentDistinguishedName = AdLdapDnHelper.GetParentDistinguishedName(distinguishedName);
        var contentSummary = CountOrganizationalUnitChildren(ldapConnection, distinguishedName);

        detail = new AdOrganizationalUnitDetail(
            objectGuid.ToString("D"),
            name,
            ou,
            displayName,
            distinguishedName,
            parentDistinguishedName,
            AdOrganizationalUnitCanonicalNameBuilder.Build(distinguishedName),
            contentSummary,
            []);
        return true;
    }

    private List<AdOrganizationalUnitChildListItem> LoadChildOrganizationalUnits(
        LdapConnection ldapConnection,
        string parentDistinguishedName)
    {
        var searchRequest = new SearchRequest(
            parentDistinguishedName,
            ChildOrganizationalUnitFilter,
            SearchScope.OneLevel,
            OrganizationalUnitListAttributes)
        {
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            return [];
        }

        var items = new List<AdOrganizationalUnitChildListItem>();
        foreach (SearchResultEntry entry in response.Entries)
        {
            if (!TryGetObjectGuid(entry, out var objectGuid))
            {
                continue;
            }

            var distinguishedName = GetFirstString(entry, "distinguishedName");
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                continue;
            }

            items.Add(new AdOrganizationalUnitChildListItem(
                objectGuid.ToString("D"),
                GetFirstString(entry, "name"),
                GetFirstString(entry, "ou"),
                distinguishedName,
                AdOrganizationalUnitCanonicalNameBuilder.Build(distinguishedName)));
        }

        return items
            .OrderBy(static item => item.Name ?? item.Ou ?? item.DistinguishedName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AdOrganizationalUnitContentSummary CountOrganizationalUnitChildren(
        LdapConnection ldapConnection,
        string distinguishedName) =>
        new(
            CountOneLevelEntries(ldapConnection, distinguishedName, ChildOrganizationalUnitFilter),
            CountOneLevelEntries(ldapConnection, distinguishedName, ChildUserFilter),
            CountOneLevelEntries(ldapConnection, distinguishedName, ChildGroupFilter),
            CountOneLevelEntries(ldapConnection, distinguishedName, ChildComputerFilter));

    private static int CountOneLevelEntries(
        LdapConnection ldapConnection,
        string searchBase,
        string filter)
    {
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.OneLevel,
            "distinguishedName")
        {
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        return response.ResultCode == ResultCode.Success ? response.Entries.Count : 0;
    }

    private static string BuildOrganizationalUnitObjectGuidFilter(Guid id)
    {
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(id);
        return $"(&(objectClass=organizationalUnit)(objectGUID={guidFilter}))";
    }

    private static bool OrganizationalUnitHasChildren(LdapConnection ldapConnection, string distinguishedName) =>
        CountOneLevelEntries(ldapConnection, distinguishedName, ChildOrganizationalUnitFilter) > 0
        || CountOneLevelEntries(ldapConnection, distinguishedName, ChildUserFilter) > 0
        || CountOneLevelEntries(ldapConnection, distinguishedName, ChildGroupFilter) > 0
        || CountOneLevelEntries(ldapConnection, distinguishedName, ChildComputerFilter) > 0;

    private static bool ExistsOrganizationalUnitWithNameUnderParent(
        LdapConnection ldapConnection,
        string parentDistinguishedName,
        string ouName,
        string? excludeDistinguishedName = null)
    {
        var escaped = AdLdapFilterHelper.EscapeFilterValue(ouName.Trim());
        var filter =
            $"(&(objectClass=organizationalUnit)(|(ou={escaped})(name={escaped})))";
        var searchRequest = new SearchRequest(
            parentDistinguishedName.Trim(),
            filter,
            SearchScope.OneLevel,
            "distinguishedName")
        {
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            return false;
        }

        foreach (SearchResultEntry entry in response.Entries)
        {
            var distinguishedName = GetFirstString(entry, "distinguishedName");
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(excludeDistinguishedName)
                && AdLdapDnHelper.AreDistinguishedNamesEqual(distinguishedName, excludeDistinguishedName))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryValidateParentExists(
        LdapConnection ldapConnection,
        string parentDistinguishedName)
    {
        if (AdOrganizationalUnitGuard.IsDomainNamingContext(parentDistinguishedName))
        {
            return true;
        }

        return TryLoadOrganizationalUnit(ldapConnection, parentDistinguishedName);
    }
}
