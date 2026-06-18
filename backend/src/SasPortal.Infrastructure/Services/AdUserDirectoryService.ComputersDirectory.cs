using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdComputerDirectoryService
{
    private const int ComputerOuSearchDefaultPageSize = 50;
    private const int ComputerOuSearchMaxPageSize = 200;

    private static readonly string[] ComputerListAttributes =
    [
        "objectGUID",
        "cn",
        "name",
        "sAMAccountName",
        "distinguishedName",
        "dNSHostName",
        "operatingSystem",
        "whenChanged",
        "userAccountControl",
    ];

    private static readonly string[] ComputerDetailAttributes =
    [
        "objectGUID",
        "cn",
        "name",
        "sAMAccountName",
        "distinguishedName",
        "dNSHostName",
        "description",
        "operatingSystem",
        "operatingSystemVersion",
        "operatingSystemServicePack",
        "managedBy",
        "lastLogonTimestamp",
        "whenCreated",
        "whenChanged",
        "primaryGroupID",
        "memberOf",
        "userAccountControl",
    ];

    public Task<AdComputerDirectoryListResult> SearchComputersAsync(
        AdComputerListQuery query,
        CancellationToken cancellationToken = default) =>
        SearchComputersInternalAsync(query, cancellationToken);

    public Task<AdComputerDirectoryDetailResult> GetComputerByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetComputerByIdInternalAsync(id, cancellationToken);

    public Task<AdOrganizationalUnitSearchResult> SearchComputerOrganizationalUnitsAsync(
        AdOrganizationalUnitSearchQuery query,
        CancellationToken cancellationToken = default) =>
        SearchComputerOrganizationalUnitsInternalAsync(query, cancellationToken);

    public Task<AdComputerOperatingSystemOptionsResult> GetComputerOperatingSystemsAsync(
        CancellationToken cancellationToken = default) =>
        GetComputerOperatingSystemsInternalAsync(cancellationToken);

    private async Task<AdComputerDirectoryListResult> SearchComputersInternalAsync(
        AdComputerListQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = AdLdapValueConverter.ClampPageSize(query.PageSize);
        var pageNumber = AdLdapValueConverter.NormalizePageNumber(query.PageNumber);

        if (!AdLdapAttributeCatalog.IsSearchTermValid(query.Search))
        {
            return new AdComputerDirectoryListResult(
                true,
                string.Empty,
                new AdComputerSearchPage([], pageNumber, pageSize, false));
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdComputerDirectoryListResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return new AdComputerDirectoryListResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var filter = AdLdapComputerFilterHelper.BuildComputerDirectorySearchFilter(
                query.Search!.Trim(),
                query.Status,
                query.OperatingSystem);
            var items = new List<AdComputerListItem>(pageSize);
            byte[]? cookie = null;
            var hasNextPage = false;

            for (var currentPage = 1; currentPage <= pageNumber; currentPage++)
            {
                var searchRequest = new SearchRequest(
                    computersSearchBase,
                    filter,
                    SearchScope.Subtree,
                    ComputerListAttributes)
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
                    return ComputerListConnectionFailed();
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
                    if (TryMapComputerListItem(entry, out var item))
                    {
                        items.Add(item);
                    }
                }
            }

            return new AdComputerDirectoryListResult(
                true,
                string.Empty,
                new AdComputerSearchPage(items, pageNumber, pageSize, hasNextPage));
        }
        catch (LdapException)
        {
            return ComputerListConnectionFailed();
        }
        catch (Exception)
        {
            return ComputerListConnectionFailed();
        }
    }

    private async Task<AdComputerOperatingSystemOptionsResult> GetComputerOperatingSystemsInternalAsync(
        CancellationToken cancellationToken)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdComputerOperatingSystemOptionsResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return new AdComputerOperatingSystemOptionsResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var filter = AdLdapComputerFilterHelper.BuildComputerOperatingSystemOptionsFilter();
            var collectedValues = new List<string>();
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            byte[]? cookie = null;
            var pagesScanned = 0;

            while (pagesScanned < AdComputerDirectoryLimits.OperatingSystemOptionsMaxPages)
            {
                pagesScanned++;
                var searchRequest = new SearchRequest(
                    computersSearchBase,
                    filter,
                    SearchScope.Subtree,
                    "operatingSystem")
                {
                    TimeLimit = LdapOperationTimeout,
                };

                var pageControl = new PageResultRequestControl(AdComputerDirectoryLimits.OperatingSystemOptionsPageSize)
                {
                    Cookie = cookie,
                };
                searchRequest.Controls.Add(pageControl);

                var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
                if (response.ResultCode != ResultCode.Success)
                {
                    return ComputerOperatingSystemOptionsConnectionFailed();
                }

                foreach (SearchResultEntry entry in response.Entries)
                {
                    var operatingSystem = GetFirstString(entry, "operatingSystem");
                    if (string.IsNullOrWhiteSpace(operatingSystem))
                    {
                        continue;
                    }

                    var trimmed = operatingSystem.Trim();
                    if (seen.TryAdd(trimmed, trimmed))
                    {
                        collectedValues.Add(trimmed);
                    }

                    if (seen.Count >= AdComputerDirectoryLimits.OperatingSystemOptionsMaxCount)
                    {
                        break;
                    }
                }

                if (seen.Count >= AdComputerDirectoryLimits.OperatingSystemOptionsMaxCount)
                {
                    break;
                }

                var pageResponse = response.Controls
                    .OfType<PageResultResponseControl>()
                    .FirstOrDefault();
                cookie = pageResponse?.Cookie;
                if (cookie is not { Length: > 0 })
                {
                    break;
                }
            }

            var items = AdComputerOperatingSystemOptionsNormalizer.NormalizeDistinctSorted(
                collectedValues,
                AdComputerDirectoryLimits.OperatingSystemOptionsMaxCount);

            return new AdComputerOperatingSystemOptionsResult(
                true,
                string.Empty,
                new AdComputerOperatingSystemOptionsPage(items));
        }
        catch (LdapException)
        {
            return ComputerOperatingSystemOptionsConnectionFailed();
        }
        catch (Exception)
        {
            return ComputerOperatingSystemOptionsConnectionFailed();
        }
    }

    private async Task<AdComputerDirectoryDetailResult> GetComputerByIdInternalAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdComputerDirectoryDetailResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
        {
            return new AdComputerDirectoryDetailResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var filter = AdLdapComputerFilterHelper.BuildComputerObjectGuidFilter(id);
            var searchRequest = new SearchRequest(
                computersSearchBase,
                filter,
                SearchScope.Subtree,
                ComputerDetailAttributes)
            {
                SizeLimit = 2,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return ComputerDetailConnectionFailed();
            }

            if (response.Entries.Count == 0)
            {
                return new AdComputerDirectoryDetailResult(
                    false,
                    AdManagementApiMessageKeys.Computers.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            if (!TryMapComputerDetail(ldapConnection, response.Entries[0], out var detail))
            {
                return new AdComputerDirectoryDetailResult(
                    false,
                    AdManagementApiMessageKeys.Computers.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            detail = TryEnrichComputerDetailWithResolvedManagedBy(ldapConnection, detail);

            return new AdComputerDirectoryDetailResult(true, string.Empty, detail);
        }
        catch (LdapException)
        {
            return ComputerDetailConnectionFailed();
        }
        catch (Exception)
        {
            return ComputerDetailConnectionFailed();
        }
    }

    private async Task<AdOrganizationalUnitSearchResult> SearchComputerOrganizationalUnitsInternalAsync(
        AdOrganizationalUnitSearchQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = query.PageSize <= 0
            ? ComputerOuSearchDefaultPageSize
            : Math.Min(query.PageSize, ComputerOuSearchMaxPageSize);

        if (!AdLdapAttributeCatalog.IsSearchTermValid(query.Search))
        {
            return new AdOrganizationalUnitSearchResult(
                true,
                string.Empty,
                new AdOrganizationalUnitSearchPage([], false));
        }

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

        var computersSearchBase = AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(
            connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(computersSearchBase))
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
                computersSearchBase,
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
                return ComputerOuConnectionFailed();
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
            return ComputerOuConnectionFailed();
        }
        catch (Exception)
        {
            return ComputerOuConnectionFailed();
        }
    }

    private AdComputerDetail TryEnrichComputerDetailWithResolvedManagedBy(
        LdapConnection ldapConnection,
        AdComputerDetail detail)
    {
        if (string.IsNullOrWhiteSpace(detail.ManagedByDistinguishedName))
        {
            return detail;
        }

        try
        {
            if (!TryResolveManagedByDisplayName(ldapConnection, detail.ManagedByDistinguishedName, out var displayName))
            {
                return detail;
            }

            return detail with { ManagedByDisplayName = displayName };
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AD computer managedBy resolve failed for computer {ComputerId}",
                detail.Id);
            return detail;
        }
    }

    private static bool TryMapComputerListItem(SearchResultEntry entry, out AdComputerListItem item)
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

        var cn = GetFirstString(entry, "cn");
        var name = GetFirstString(entry, "name")
            ?? cn
            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;
        var userAccountControl = GetFirstInt(entry, "userAccountControl");

        item = new AdComputerListItem(
            objectGuid.ToString("D"),
            name,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "dNSHostName"),
            GetFirstString(entry, "operatingSystem"),
            distinguishedName,
            AdLdapValueConverter.IsAccountEnabled(userAccountControl),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")));

        return true;
    }

    private static bool TryMapComputerDetail(
        LdapConnection ldapConnection,
        SearchResultEntry entry,
        out AdComputerDetail detail)
    {
        detail = null!;
        if (!TryMapComputerListItem(entry, out var listItem))
        {
            return false;
        }

        var memberOfDns = GetAllStrings(entry, "memberOf");
        var memberOfCount = memberOfDns.Count;
        var memberOfTruncated = memberOfCount > AdGroupDirectoryLimits.MemberOfDisplayLimit;
        var memberOf = ResolveComputerMemberOfItems(
            ldapConnection,
            memberOfDns.Take(AdGroupDirectoryLimits.MemberOfDisplayLimit));
        var userAccountControl = GetFirstInt(entry, "userAccountControl");

        detail = new AdComputerDetail(
            listItem.Id,
            listItem.Name,
            GetFirstString(entry, "cn"),
            listItem.SamAccountName,
            listItem.DnsHostName,
            listItem.DistinguishedName,
            AdLdapDnHelper.GetParentDistinguishedName(listItem.DistinguishedName),
            GetFirstString(entry, "description"),
            listItem.OperatingSystem,
            GetFirstString(entry, "operatingSystemVersion"),
            GetFirstString(entry, "operatingSystemServicePack"),
            GetFirstString(entry, "managedBy"),
            null,
            AdLdapValueConverter.FromAdFileTime(GetFirstLong(entry, "lastLogonTimestamp")),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenCreated")),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")),
            userAccountControl,
            AdLdapValueConverter.IsAccountEnabled(userAccountControl),
            GetFirstInt(entry, "primaryGroupID"),
            memberOfCount,
            memberOf,
            memberOfTruncated);

        return true;
    }

    private static IReadOnlyList<AdComputerMemberOfItem> ResolveComputerMemberOfItems(
        LdapConnection ldapConnection,
        IEnumerable<string> distinguishedNames)
    {
        var items = new List<AdComputerMemberOfItem>();
        foreach (var distinguishedName in distinguishedNames)
        {
            items.Add(ResolveComputerMemberOfItem(ldapConnection, distinguishedName));
        }

        return items;
    }

    private static AdComputerMemberOfItem ResolveComputerMemberOfItem(
        LdapConnection ldapConnection,
        string distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return new AdComputerMemberOfItem(distinguishedName ?? string.Empty, null, null);
        }

        var trimmedDn = distinguishedName.Trim();
        try
        {
            var searchRequest = new SearchRequest(
                trimmedDn,
                "(objectClass=group)",
                SearchScope.Base,
                "cn",
                "name",
                "sAMAccountName",
                "distinguishedName")
            {
                SizeLimit = 1,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
            {
                return BuildComputerMemberOfFallback(trimmedDn);
            }

            var entry = response.Entries[0];
            var resolvedDn = GetFirstString(entry, "distinguishedName") ?? trimmedDn;
            var name = GetFirstString(entry, "name")
                ?? GetFirstString(entry, "cn")
                ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(resolvedDn)
                ?? resolvedDn;

            return new AdComputerMemberOfItem(
                resolvedDn,
                name,
                GetFirstString(entry, "sAMAccountName"));
        }
        catch (LdapException)
        {
            return BuildComputerMemberOfFallback(trimmedDn);
        }
        catch (Exception)
        {
            return BuildComputerMemberOfFallback(trimmedDn);
        }
    }

    private static AdComputerMemberOfItem BuildComputerMemberOfFallback(string distinguishedName)
    {
        var fallbackName = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;
        return new AdComputerMemberOfItem(distinguishedName, fallbackName, null);
    }

    private static AdComputerDirectoryListResult ComputerListConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.Computers.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdComputerDirectoryDetailResult ComputerDetailConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.Computers.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdOrganizationalUnitSearchResult ComputerOuConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.Computers.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdComputerOperatingSystemOptionsResult ComputerOperatingSystemOptionsConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.Computers.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);
}
