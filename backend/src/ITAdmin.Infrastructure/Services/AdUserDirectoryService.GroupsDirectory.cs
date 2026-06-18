using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdGroupDirectoryService
{
    private static readonly string[] GroupListAttributes =
    [
        "objectGUID",
        "distinguishedName",
        "displayName",
        "cn",
        "name",
        "sAMAccountName",
        "description",
        "groupType",
    ];

    private static readonly string[] GroupDetailAttributes =
    [
        "objectGUID",
        "distinguishedName",
        "displayName",
        "cn",
        "name",
        "sAMAccountName",
        "description",
        "groupType",
        "whenCreated",
        "whenChanged",
        "managedBy",
        "member",
        "memberOf",
    ];

    private static readonly string[] MemberResolveAttributes =
    [
        "objectClass",
        "displayName",
        "cn",
        "name",
        "sAMAccountName",
        "description",
        "distinguishedName",
    ];

    public Task<AdGroupDirectoryListResult> SearchGroupsAsync(
        AdGroupListQuery query,
        CancellationToken cancellationToken = default) =>
        SearchSecurityGroupsAsync(query, cancellationToken);

    public Task<AdGroupDirectoryDetailResult> GetGroupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSecurityGroupByIdAsync(id, cancellationToken);

    private async Task<AdGroupDirectoryListResult> SearchSecurityGroupsAsync(
        AdGroupListQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = AdLdapValueConverter.ClampPageSize(query.PageSize);
        var pageNumber = AdLdapValueConverter.NormalizePageNumber(query.PageNumber);

        if (!AdLdapAttributeCatalog.IsSearchTermValid(query.Search))
        {
            return new AdGroupDirectoryListResult(
                true,
                string.Empty,
                new AdGroupSearchPage([], pageNumber, pageSize, false));
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdGroupDirectoryListResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdGroupDirectoryListResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var filter = AdLdapGroupFilterHelper.BuildSecurityGroupSearchFilter(query.Search!.Trim());
            var items = new List<AdGroupListItem>(pageSize);
            byte[]? cookie = null;
            var hasNextPage = false;

            for (var currentPage = 1; currentPage <= pageNumber; currentPage++)
            {
                var searchRequest = new SearchRequest(
                    groupsSearchBase,
                    filter,
                    SearchScope.Subtree,
                    GroupListAttributes)
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
                    return GroupListConnectionFailed();
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
                    if (TryMapGroupListItem(entry, out var item))
                    {
                        items.Add(item);
                    }
                }
            }

            return new AdGroupDirectoryListResult(
                true,
                string.Empty,
                new AdGroupSearchPage(items, pageNumber, pageSize, hasNextPage));
        }
        catch (LdapException)
        {
            return GroupListConnectionFailed();
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return GroupListConnectionFailed();
        }
    }

    private async Task<AdGroupDirectoryDetailResult> GetSecurityGroupByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdGroupDirectoryDetailResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var groupsSearchBase = ResolveRequiredGroupsSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(groupsSearchBase))
        {
            return new AdGroupDirectoryDetailResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var filter = AdLdapGroupFilterHelper.BuildSecurityGroupObjectGuidFilter(id);
            var searchRequest = new SearchRequest(
                groupsSearchBase,
                filter,
                SearchScope.Subtree,
                GroupDetailAttributes)
            {
                SizeLimit = 2,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return GroupDetailConnectionFailed();
            }

            if (response.Entries.Count == 0)
            {
                return new AdGroupDirectoryDetailResult(
                    false,
                    AdManagementApiMessageKeys.Groups.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            if (!TryMapGroupDetail(ldapConnection, response.Entries[0], out var detail))
            {
                return new AdGroupDirectoryDetailResult(
                    false,
                    AdManagementApiMessageKeys.Groups.NotFound,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            detail = TryEnrichDetailWithResolvedManagedBy(ldapConnection, detail);

            return new AdGroupDirectoryDetailResult(true, string.Empty, detail);
        }
        catch (LdapException)
        {
            return GroupDetailConnectionFailed();
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return GroupDetailConnectionFailed();
        }
    }

    private AdGroupDetail TryEnrichDetailWithResolvedManagedBy(LdapConnection ldapConnection, AdGroupDetail detail)
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
                "AD group managedBy resolve failed for group {GroupId}",
                detail.Id);
            return detail;
        }
    }

    private static bool TryResolveManagedByDisplayName(
        LdapConnection ldapConnection,
        string managedByDistinguishedName,
        out string? displayName)
    {
        displayName = null;
        var searchRequest = new SearchRequest(
            managedByDistinguishedName.Trim(),
            "(|(objectClass=user)(objectClass=group)(objectClass=contact))",
            SearchScope.Base,
            "displayName",
            "cn",
            "name",
            "sAMAccountName")
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
        displayName = GetFirstString(entry, "displayName")
            ?? GetFirstString(entry, "cn")
            ?? GetFirstString(entry, "name")
            ?? GetFirstString(entry, "sAMAccountName");
        return !string.IsNullOrWhiteSpace(displayName);
    }

    private static bool TryMapGroupListItem(SearchResultEntry entry, out AdGroupListItem item)
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

        var groupTypeRaw = GetFirstInt(entry, "groupType");
        var typeInfo = AdGroupTypeHelper.Parse(groupTypeRaw);
        if (!typeInfo.SecurityEnabled)
        {
            return false;
        }

        var cn = GetFirstString(entry, "cn");
        var name = GetFirstString(entry, "name")
            ?? cn
            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;

        item = new AdGroupListItem(
            objectGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "displayName"),
            name,
            cn,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "description"),
            AdGroupTypeHelper.ScopeToCode(typeInfo.Scope),
            typeInfo.SecurityEnabled,
            groupTypeRaw);

        return true;
    }

    private static bool TryMapGroupDetail(
        LdapConnection ldapConnection,
        SearchResultEntry entry,
        out AdGroupDetail detail)
    {
        detail = null!;
        if (!TryMapGroupListItem(entry, out var listItem))
        {
            return false;
        }

        var memberDns = ReadAllAttributeValues(ldapConnection, listItem.DistinguishedName, "member");
        var memberOfDns = GetAllStrings(entry, "memberOf");
        var memberCount = memberDns.Count;
        var memberOfCount = memberOfDns.Count;
        var membersTruncated = memberCount > AdGroupDirectoryLimits.MemberDisplayLimit;
        var memberOfTruncated = memberOfCount > AdGroupDirectoryLimits.MemberOfDisplayLimit;

        var members = ResolveMemberItems(
            ldapConnection,
            memberDns.Take(AdGroupDirectoryLimits.MemberDisplayLimit));
        var memberOf = ResolveMemberItems(
            ldapConnection,
            memberOfDns.Take(AdGroupDirectoryLimits.MemberOfDisplayLimit));

        detail = new AdGroupDetail(
            listItem.Id,
            listItem.DistinguishedName,
            listItem.DisplayName,
            listItem.Name,
            listItem.Cn,
            listItem.SamAccountName,
            listItem.Description,
            listItem.GroupScope,
            listItem.SecurityEnabled,
            listItem.GroupType,
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenCreated")),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")),
            GetFirstString(entry, "managedBy"),
            null,
            memberCount,
            memberOfCount,
            members,
            memberOf,
            membersTruncated,
            memberOfTruncated);

        return true;
    }

    private static IReadOnlyList<AdGroupMemberItem> ResolveMemberItems(
        LdapConnection ldapConnection,
        IEnumerable<string> distinguishedNames)
    {
        var items = new List<AdGroupMemberItem>();
        foreach (var distinguishedName in distinguishedNames)
        {
            items.Add(ResolveMemberItem(ldapConnection, distinguishedName));
        }

        return items;
    }

    private static AdGroupMemberItem ResolveMemberItem(
        LdapConnection ldapConnection,
        string distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return new AdGroupMemberItem(
                "Unknown",
                null,
                null,
                null,
                distinguishedName ?? string.Empty,
                null);
        }

        var trimmedDn = distinguishedName.Trim();
        try
        {
            var searchRequest = new SearchRequest(
                trimmedDn,
                "(objectClass=*)",
                SearchScope.Base,
                MemberResolveAttributes)
            {
                SizeLimit = 1,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
            {
                return BuildMemberFallback(trimmedDn);
            }

            var entry = response.Entries[0];
            var objectClasses = GetAllStrings(entry, "objectClass");
            var resolvedDn = GetFirstString(entry, "distinguishedName") ?? trimmedDn;
            var name = GetFirstString(entry, "name")
                ?? GetFirstString(entry, "cn")
                ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(resolvedDn)
                ?? resolvedDn;

            return new AdGroupMemberItem(
                ResolveMemberType(objectClasses),
                GetFirstString(entry, "displayName"),
                name,
                GetFirstString(entry, "sAMAccountName"),
                resolvedDn,
                GetFirstString(entry, "description"));
        }
        catch (LdapException)
        {
            return BuildMemberFallback(trimmedDn);
        }
        catch (Exception)
        {
            // Unexpected member lookup failure falls back to DN-only metadata.
            return BuildMemberFallback(trimmedDn);
        }
    }

    private static AdGroupMemberItem BuildMemberFallback(string distinguishedName)
    {
        var fallbackName = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;
        return new AdGroupMemberItem(
            "Unknown",
            null,
            fallbackName,
            null,
            distinguishedName,
            null);
    }

    private static string ResolveMemberType(IReadOnlyList<string> objectClasses)
    {
        if (objectClasses.Any(value => value.Equals("computer", StringComparison.OrdinalIgnoreCase)))
        {
            return "Computer";
        }

        if (objectClasses.Any(value => value.Equals("group", StringComparison.OrdinalIgnoreCase)))
        {
            return "Group";
        }

        if (objectClasses.Any(value =>
                value.Equals("user", StringComparison.OrdinalIgnoreCase)
                || value.Equals("person", StringComparison.OrdinalIgnoreCase)))
        {
            return "User";
        }

        return "Unknown";
    }

    private static IReadOnlyList<string> ReadAllAttributeValues(
        LdapConnection ldapConnection,
        string distinguishedName,
        string attributeName)
    {
        var values = new List<string>();
        var rangeStart = 0;
        const int rangeStep = 1500;

        while (true)
        {
            var rangeEnd = rangeStart + rangeStep - 1;
            var rangedAttribute = $"{attributeName};range={rangeStart}-{rangeEnd}";
            SearchResponse response;
            try
            {
                var searchRequest = new SearchRequest(
                    distinguishedName,
                    "(objectClass=*)",
                    SearchScope.Base,
                    rangedAttribute)
                {
                    SizeLimit = 1,
                    TimeLimit = LdapOperationTimeout,
                };
                response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            }
            catch (LdapException)
            {
                break;
            }

            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
            {
                break;
            }

            var entry = response.Entries[0];
            var resolvedAttributeName = ResolveRangedAttributeName(entry, attributeName);
            if (resolvedAttributeName is null)
            {
                break;
            }

            var attributeValues = GetAllStrings(entry, resolvedAttributeName);
            if (attributeValues.Count == 0)
            {
                break;
            }

            values.AddRange(attributeValues);

            if (resolvedAttributeName.Contains(";range=", StringComparison.OrdinalIgnoreCase)
                && resolvedAttributeName.Contains('*', StringComparison.Ordinal))
            {
                break;
            }

            rangeStart += rangeStep;
        }

        if (values.Count == 0)
        {
            return GetAllStringsFromBaseEntry(ldapConnection, distinguishedName, attributeName);
        }

        return values;
    }

    private static IReadOnlyList<string> GetAllStringsFromBaseEntry(
        LdapConnection ldapConnection,
        string distinguishedName,
        string attributeName)
    {
        try
        {
            var searchRequest = new SearchRequest(
                distinguishedName,
                "(objectClass=*)",
                SearchScope.Base,
                attributeName)
            {
                SizeLimit = 1,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            return GetAllStrings(response.Entries[0], attributeName);
        }
        catch (LdapException)
        {
            return Array.Empty<string>();
        }
    }

    private static string? ResolveRangedAttributeName(SearchResultEntry entry, string attributeName)
    {
        foreach (string key in entry.Attributes.AttributeNames)
        {
            if (key.Equals(attributeName, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(attributeName + ";range=", StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    private static string? ResolveRequiredGroupsSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.GroupsSearchBase) ? null : connection.GroupsSearchBase.Trim();

    private static AdGroupDirectoryListResult GroupListConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.Groups.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);

    private static AdGroupDirectoryDetailResult GroupDetailConnectionFailed() =>
        new(
            false,
            AdManagementApiMessageKeys.Groups.QueryFailed,
            null,
            AdDirectoryFailureKind.ConnectionFailed);
}
