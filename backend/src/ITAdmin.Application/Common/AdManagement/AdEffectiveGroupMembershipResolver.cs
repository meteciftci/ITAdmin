using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdEffectiveGroupMembershipLimits
{
    public const int DefaultMaxDepth = 5;
    public const int MinMaxDepth = 1;
    public const int MaxMaxDepth = 10;
    public const int MaxResultCount = 500;
}

public static class AdEffectiveGroupMembershipResolver
{
    public static int NormalizeMaxDepth(int? maxDepth) =>
        Math.Clamp(maxDepth ?? AdEffectiveGroupMembershipLimits.DefaultMaxDepth,
            AdEffectiveGroupMembershipLimits.MinMaxDepth,
            AdEffectiveGroupMembershipLimits.MaxMaxDepth);

    public static AdEffectiveGroupMembershipBuildResult Build(
        AdEffectiveGroupMembershipUserContext user,
        IReadOnlyList<string> directGroupDns,
        Func<string, AdEffectiveGroupResolvedGroup?> resolveGroup,
        Func<string, IReadOnlyList<string>> getParentGroupDns,
        int maxDepth)
    {
        var userNode = CreateUserPathNode(user);
        var directGroups = new List<AdEffectiveGroupSummaryItem>();
        var effectiveGroups = new List<AdEffectiveGroupNestedItem>();
        var seenEffectiveDns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var truncated = false;
        string? truncatedReason = null;

        var queue = new Queue<TraversalState>();

        foreach (var groupDn in directGroupDns)
        {
            if (string.IsNullOrWhiteSpace(groupDn))
            {
                continue;
            }

            var resolved = resolveGroup(groupDn.Trim());
            var groupItem = resolved is null
                ? CreateFallbackGroupSummary(groupDn.Trim())
                : ToSummaryItem(resolved);

            directGroups.Add(groupItem);

            var groupNode = resolved is null
                ? CreateFallbackGroupPathNode(groupDn.Trim())
                : ToGroupPathNode(resolved);

            queue.Enqueue(new TraversalState(
                groupItem.DistinguishedName,
                [userNode, groupNode],
                Depth: 1));
        }

        directGroups.Sort(GroupSummaryComparer.Instance);

        while (queue.Count > 0)
        {
            var state = queue.Dequeue();
            if (state.Depth >= maxDepth)
            {
                continue;
            }

            var parentDns = getParentGroupDns(state.GroupDn);
            foreach (var parentDn in parentDns)
            {
                if (string.IsNullOrWhiteSpace(parentDn))
                {
                    continue;
                }

                var normalizedParentDn = parentDn.Trim();
                if (state.Path.Any(node =>
                        AdLdapDnHelper.AreDistinguishedNamesEqual(node.DistinguishedName, normalizedParentDn)))
                {
                    continue;
                }

                var nextDepth = state.Depth + 1;
                if (nextDepth > maxDepth)
                {
                    continue;
                }

                var resolvedParent = resolveGroup(normalizedParentDn);
                var parentNode = resolvedParent is null
                    ? CreateFallbackGroupPathNode(normalizedParentDn)
                    : ToGroupPathNode(resolvedParent);

                if (nextDepth >= 2 && seenEffectiveDns.Add(normalizedParentDn))
                {
                    if (effectiveGroups.Count >= AdEffectiveGroupMembershipLimits.MaxResultCount)
                    {
                        truncated = true;
                        truncatedReason = AdEffectiveGroupTruncatedReasons.ResultLimitExceeded;
                        return new AdEffectiveGroupMembershipBuildResult(
                            directGroups,
                            effectiveGroups,
                            maxDepth,
                            truncated,
                            truncatedReason);
                    }

                    var path = state.Path
                        .Append(parentNode)
                        .ToList();

                    effectiveGroups.Add(new AdEffectiveGroupNestedItem(
                        resolvedParent?.DistinguishedName ?? normalizedParentDn,
                        resolvedParent?.DisplayName,
                        resolvedParent?.Name
                            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(normalizedParentDn)
                            ?? normalizedParentDn,
                        resolvedParent?.SamAccountName,
                        resolvedParent?.Description,
                        nextDepth,
                        IsDirect: false,
                        path));
                }

                if (nextDepth < maxDepth)
                {
                    queue.Enqueue(new TraversalState(
                        normalizedParentDn,
                        state.Path.Append(parentNode).ToList(),
                        nextDepth));
                }
            }
        }

        effectiveGroups.Sort(EffectiveGroupComparer.Instance);

        return new AdEffectiveGroupMembershipBuildResult(
            directGroups,
            effectiveGroups,
            maxDepth,
            truncated,
            truncatedReason);
    }

    private static AdMembershipPathNode CreateUserPathNode(AdEffectiveGroupMembershipUserContext user)
    {
        var name = !string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.DisplayName.Trim()
            : !string.IsNullOrWhiteSpace(user.SamAccountName)
                ? user.SamAccountName.Trim()
                : user.DistinguishedName;

        return new AdMembershipPathNode(
            AdMembershipPathNodeTypes.User,
            name,
            user.DisplayName,
            user.SamAccountName,
            user.DistinguishedName);
    }

    private static AdMembershipPathNode ToGroupPathNode(AdEffectiveGroupResolvedGroup group) =>
        new(
            AdMembershipPathNodeTypes.Group,
            group.Name,
            group.DisplayName,
            group.SamAccountName,
            group.DistinguishedName);

    private static AdMembershipPathNode CreateFallbackGroupPathNode(string distinguishedName)
    {
        var name = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName) ?? distinguishedName;
        return new AdMembershipPathNode(
            AdMembershipPathNodeTypes.Group,
            name,
            null,
            null,
            distinguishedName);
    }

    private static AdEffectiveGroupSummaryItem ToSummaryItem(AdEffectiveGroupResolvedGroup group) =>
        new(
            group.DistinguishedName,
            group.DisplayName,
            group.Name,
            group.SamAccountName,
            group.Description);

    private static AdEffectiveGroupSummaryItem CreateFallbackGroupSummary(string distinguishedName)
    {
        var name = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName) ?? distinguishedName;
        return new AdEffectiveGroupSummaryItem(
            distinguishedName,
            null,
            name,
            null,
            null);
    }

    private sealed record TraversalState(
        string GroupDn,
        IReadOnlyList<AdMembershipPathNode> Path,
        int Depth);

    private sealed class GroupSummaryComparer : IComparer<AdEffectiveGroupSummaryItem>
    {
        public static GroupSummaryComparer Instance { get; } = new();

        public int Compare(AdEffectiveGroupSummaryItem? left, AdEffectiveGroupSummaryItem? right)
        {
            if (left is null || right is null)
            {
                return 0;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class EffectiveGroupComparer : IComparer<AdEffectiveGroupNestedItem>
    {
        public static EffectiveGroupComparer Instance { get; } = new();

        public int Compare(AdEffectiveGroupNestedItem? left, AdEffectiveGroupNestedItem? right)
        {
            if (left is null || right is null)
            {
                return 0;
            }

            var depthCompare = left.Depth.CompareTo(right.Depth);
            return depthCompare != 0
                ? depthCompare
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}

public sealed record AdEffectiveGroupMembershipUserContext(
    string UserId,
    string DistinguishedName,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName);

public sealed record AdEffectiveGroupResolvedGroup(
    string DistinguishedName,
    string? DisplayName,
    string Name,
    string? SamAccountName,
    string? Description);

public sealed record AdEffectiveGroupMembershipBuildResult(
    IReadOnlyList<AdEffectiveGroupSummaryItem> DirectGroups,
    IReadOnlyList<AdEffectiveGroupNestedItem> EffectiveGroups,
    int MaxDepth,
    bool Truncated,
    string? TruncatedReason);
