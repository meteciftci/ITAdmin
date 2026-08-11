using System.DirectoryServices.Protocols;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUsersDirectoryService
{
    public Task<AdUserEffectiveGroupsResult> GetUserEffectiveGroupsAsync(
        AdUserEffectiveGroupsRequest request,
        CancellationToken cancellationToken = default) =>
        LoadUserEffectiveGroupsAsync(request, cancellationToken);

    private async Task<AdUserEffectiveGroupsResult> LoadUserEffectiveGroupsAsync(
        AdUserEffectiveGroupsRequest request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var maxDepth = AdEffectiveGroupMembershipResolver.NormalizeMaxDepth(request.MaxDepth);

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return EffectiveGroupsFailure(
                connectionResult.MessageKey,
                request.UserId,
                maxDepth,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var searchBase = ResolveDetailSearchBase(connectionResult.Context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return EffectiveGroupsFailure(
                AdManagementApiMessageKeys.Common.NotConfigured,
                request.UserId,
                maxDepth,
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
                return EffectiveGroupsFailure(
                    AdManagementApiMessageKeys.Users.NotFound,
                    request.UserId,
                    maxDepth,
                    AdDirectoryFailureKind.NotFound);
            }

            var groupSearchBases = AdLdapGroupSearchBases.ResolveDistinctSearchBases(
                connectionResult.Context.Connection);
            var groupCache = new Dictionary<string, AdEffectiveGroupResolvedGroup?>(StringComparer.OrdinalIgnoreCase);
            var memberOfCache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

            AdEffectiveGroupResolvedGroup? ResolveGroup(string groupDn)
            {
                if (groupCache.TryGetValue(groupDn, out var cached))
                {
                    return cached;
                }

                if (TryLoadGroupDirectoryInfo(
                        ldapConnection,
                        groupSearchBases,
                        groupDn,
                        out var groupInfo))
                {
                    var resolved = new AdEffectiveGroupResolvedGroup(
                        groupInfo.DistinguishedName,
                        groupInfo.DisplayName,
                        groupInfo.Name,
                        groupInfo.SamAccountName,
                        groupInfo.Description);
                    groupCache[groupDn] = resolved;
                    return resolved;
                }

                groupCache[groupDn] = null;
                return null;
            }

            IReadOnlyList<string> GetParentGroupDns(string groupDn)
            {
                if (memberOfCache.TryGetValue(groupDn, out var cached))
                {
                    return cached;
                }

                var parents = TryLoadGroupMemberOfDns(
                    ldapConnection,
                    groupSearchBases,
                    groupDn,
                    out var memberOfDns)
                    ? memberOfDns
                    : [];

                memberOfCache[groupDn] = parents;
                return parents;
            }

            var buildResult = AdEffectiveGroupMembershipResolver.Build(
                new AdEffectiveGroupMembershipUserContext(
                    userContext.UserId,
                    userContext.DistinguishedName,
                    userContext.SamAccountName,
                    userContext.UserPrincipalName,
                    userContext.DisplayName),
                userContext.MemberOfDns.ToList(),
                ResolveGroup,
                GetParentGroupDns,
                maxDepth);

            return new AdUserEffectiveGroupsResult(
                true,
                string.Empty,
                userContext.UserId,
                userContext.DisplayName,
                userContext.SamAccountName,
                userContext.UserPrincipalName,
                userContext.DistinguishedName,
                buildResult.DirectGroups,
                buildResult.EffectiveGroups,
                buildResult.MaxDepth,
                buildResult.Truncated,
                buildResult.TruncatedReason);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException)
        {
            return EffectiveGroupsFailure(
                AdManagementApiMessageKeys.Users.QueryFailed,
                request.UserId,
                maxDepth,
                AdDirectoryFailureKind.ConnectionFailed);
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return EffectiveGroupsFailure(
                AdManagementApiMessageKeys.Users.EffectiveGroupsFailed,
                request.UserId,
                maxDepth,
                AdDirectoryFailureKind.ConnectionFailed);
        }
    }

    private static bool TryLoadGroupDirectoryInfo(
        LdapConnection ldapConnection,
        IReadOnlyList<string> groupSearchBases,
        string groupDistinguishedName,
        out AdGroupDirectoryInfo groupInfo)
    {
        if (TryLoadGroupByDn(ldapConnection, groupDistinguishedName, out groupInfo))
        {
            return true;
        }

        return TryFindGroupByDistinguishedName(
            ldapConnection,
            groupSearchBases,
            groupDistinguishedName,
            out groupInfo);
    }

    private static bool TryLoadGroupMemberOfDns(
        LdapConnection ldapConnection,
        IReadOnlyList<string> groupSearchBases,
        string groupDistinguishedName,
        out IReadOnlyList<string> memberOfDns)
    {
        memberOfDns = [];

        if (TryLoadGroupEntryMemberOf(ldapConnection, groupDistinguishedName, out memberOfDns))
        {
            return true;
        }

        if (!TryFindGroupByDistinguishedName(
                ldapConnection,
                groupSearchBases,
                groupDistinguishedName,
                out var groupInfo))
        {
            return false;
        }

        return TryLoadGroupEntryMemberOf(ldapConnection, groupInfo.DistinguishedName, out memberOfDns);
    }

    private static bool TryLoadGroupEntryMemberOf(
        LdapConnection ldapConnection,
        string groupDistinguishedName,
        out IReadOnlyList<string> memberOfDns)
    {
        memberOfDns = [];
        if (string.IsNullOrWhiteSpace(groupDistinguishedName))
        {
            return false;
        }

        var searchRequest = new SearchRequest(
            groupDistinguishedName.Trim(),
            "(objectClass=group)",
            SearchScope.Base,
            "memberOf")
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

        memberOfDns = GetAllStrings(response.Entries[0], "memberOf");
        return true;
    }

    private static bool TryFindGroupByDistinguishedName(
        LdapConnection ldapConnection,
        IReadOnlyList<string> searchBases,
        string groupDistinguishedName,
        out AdGroupDirectoryInfo groupInfo)
    {
        groupInfo = null!;
        if (string.IsNullOrWhiteSpace(groupDistinguishedName) || searchBases.Count == 0)
        {
            return false;
        }

        var escapedDn = AdLdapFilterHelper.EscapeFilterValue(groupDistinguishedName.Trim());
        var filter = $"(&(objectCategory=group)(objectClass=group)(distinguishedName={escapedDn}))";

        foreach (var searchBase in searchBases)
        {
            if (string.IsNullOrWhiteSpace(searchBase))
            {
                continue;
            }

            var searchRequest = new SearchRequest(
                searchBase,
                filter,
                SearchScope.Subtree,
                "distinguishedName",
                "displayName",
                "cn",
                "name",
                "sAMAccountName",
                "description")
            {
                SizeLimit = 2,
                TimeLimit = LdapOperationTimeout,
            };

            SearchResponse response;
            try
            {
                response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            }
            catch (LdapException)
            {
                continue;
            }

            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
            {
                continue;
            }

            if (TryMapGroupDirectoryInfo(response.Entries[0], out groupInfo))
            {
                return true;
            }
        }

        return false;
    }

    private static AdUserEffectiveGroupsResult EffectiveGroupsFailure(
        string messageKey,
        Guid userId,
        int maxDepth,
        AdDirectoryFailureKind? failureKind,
        IReadOnlyDictionary<string, object>? messageParams = null) =>
        new(
            false,
            messageKey,
            userId.ToString("D"),
            null,
            null,
            null,
            null,
            null,
            null,
            maxDepth,
            false,
            null,
            failureKind,
            messageParams);
}
