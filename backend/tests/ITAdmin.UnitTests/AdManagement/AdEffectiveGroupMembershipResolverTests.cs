using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdEffectiveGroupMembershipResolverTests
{
    private static readonly AdEffectiveGroupMembershipUserContext SampleUser = new(
        "11111111-1111-1111-1111-111111111111",
        "CN=Mete TEST,OU=Users,DC=example,DC=com",
        "mete.test",
        "mete.test@example.com",
        "Mete TEST");

    [Theory]
    [InlineData(null, 5)]
    [InlineData(3, 3)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(0, 1)]
    [InlineData(15, 10)]
    public void NormalizeMaxDepth_ClampsToAllowedRange(int? input, int expected)
    {
        Assert.Equal(expected, AdEffectiveGroupMembershipResolver.NormalizeMaxDepth(input));
    }

    [Fact]
    public void Build_ReturnsDirectGroupsAtDepthOne()
    {
        var directDn = "CN=BilgiIslem_Users,OU=Groups,DC=example,DC=com";
        var result = BuildWithGraph(
            [directDn],
            new Dictionary<string, string[]>
            {
                [directDn] = [],
            });

        Assert.Single(result.DirectGroups);
        Assert.Equal("BilgiIslem_Users", result.DirectGroups[0].Name);
        Assert.Empty(result.EffectiveGroups);
    }

    [Fact]
    public void Build_ReturnsNestedGroupWithPathStartingAtDepthTwo()
    {
        var directDn = "CN=BilgiIslem_Users,OU=Groups,DC=example,DC=com";
        var nestedDn = "CN=VPN_Users,OU=Groups,DC=example,DC=com";

        var result = BuildWithGraph(
            [directDn],
            new Dictionary<string, string[]>
            {
                [directDn] = [nestedDn],
                [nestedDn] = [],
            });

        Assert.Single(result.EffectiveGroups);
        var nested = result.EffectiveGroups[0];
        Assert.Equal(2, nested.Depth);
        Assert.False(nested.IsDirect);
        Assert.Equal("VPN_Users", nested.Name);
        Assert.Equal(3, nested.Path.Count);
        Assert.Equal(AdMembershipPathNodeTypes.User, nested.Path[0].Type);
        Assert.Equal("Mete TEST", nested.Path[0].Name);
        Assert.Equal(AdMembershipPathNodeTypes.Group, nested.Path[1].Type);
        Assert.Equal("BilgiIslem_Users", nested.Path[1].Name);
        Assert.Equal(AdMembershipPathNodeTypes.Group, nested.Path[2].Type);
        Assert.Equal("VPN_Users", nested.Path[2].Name);
    }

    [Fact]
    public void Build_DoesNotLoopInfinitelyOnCircularGroupReferences()
    {
        var directDn = "CN=GroupA,OU=Groups,DC=example,DC=com";
        var groupBDn = "CN=GroupB,OU=Groups,DC=example,DC=com";

        var result = BuildWithGraph(
            [directDn],
            new Dictionary<string, string[]>
            {
                [directDn] = [groupBDn],
                [groupBDn] = [directDn],
            },
            maxDepth: 10);

        Assert.Single(result.EffectiveGroups);
        Assert.Equal(2, result.EffectiveGroups[0].Depth);
    }

    [Fact]
    public void Build_ReturnsEachEffectiveGroupOnlyOnce()
    {
        var sharedParentDn = "CN=SharedParent,OU=Groups,DC=example,DC=com";

        var result = BuildWithGraph(
            ["CN=GroupA,OU=Groups,DC=example,DC=com", "CN=GroupB,OU=Groups,DC=example,DC=com"],
            new Dictionary<string, string[]>
            {
                ["CN=GroupA,OU=Groups,DC=example,DC=com"] = [sharedParentDn],
                ["CN=GroupB,OU=Groups,DC=example,DC=com"] = [sharedParentDn],
                [sharedParentDn] = [],
            });

        Assert.Single(
            result.EffectiveGroups,
            item => AdLdapDnHelper.AreDistinguishedNamesEqual(item.DistinguishedName, sharedParentDn));
    }

    [Fact]
    public void Build_StopsTraversalBeyondMaxDepth()
    {
        var level1 = "CN=Level1,OU=Groups,DC=example,DC=com";
        var level2 = "CN=Level2,OU=Groups,DC=example,DC=com";
        var level3 = "CN=Level3,OU=Groups,DC=example,DC=com";
        var level4 = "CN=Level4,OU=Groups,DC=example,DC=com";

        var result = BuildWithGraph(
            [level1],
            new Dictionary<string, string[]>
            {
                [level1] = [level2],
                [level2] = [level3],
                [level3] = [level4],
                [level4] = [],
            },
            maxDepth: 3);

        Assert.Equal(2, result.EffectiveGroups.Count);
        Assert.DoesNotContain(
            result.EffectiveGroups,
            item => AdLdapDnHelper.AreDistinguishedNamesEqual(item.DistinguishedName, level4));
    }

    [Fact]
    public void Build_SetsTruncatedWhenResultLimitExceeded()
    {
        var directDn = "CN=Direct,OU=Groups,DC=example,DC=com";
        var parents = Enumerable
            .Range(1, AdEffectiveGroupMembershipLimits.MaxResultCount + 5)
            .Select(index => $"CN=Parent{index},OU=Groups,DC=example,DC=com")
            .ToArray();

        var graph = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [directDn] = parents,
        };

        foreach (var parent in parents)
        {
            graph[parent] = [];
        }

        var result = BuildWithGraph([directDn], graph, maxDepth: 5);

        Assert.True(result.Truncated);
        Assert.Equal(AdEffectiveGroupTruncatedReasons.ResultLimitExceeded, result.TruncatedReason);
        Assert.Equal(AdEffectiveGroupMembershipLimits.MaxResultCount, result.EffectiveGroups.Count);
    }

    [Fact]
    public void Build_ParsesEscapedCommaInDistinguishedNameForFallbackName()
    {
        var escapedGroupDn = "CN=escaped\\, group,OU=Groups,DC=example,DC=com";

        var result = BuildWithGraph(
            [escapedGroupDn],
            new Dictionary<string, string[]>
            {
                [escapedGroupDn] = [],
            });

        Assert.Equal("escaped, group", result.DirectGroups[0].Name);
    }

    private static AdEffectiveGroupMembershipBuildResult BuildWithGraph(
        IReadOnlyList<string> directGroupDns,
        IReadOnlyDictionary<string, string[]> parentMap,
        int maxDepth = 5)
    {
        AdEffectiveGroupResolvedGroup? ResolveGroup(string groupDn)
        {
            if (!parentMap.ContainsKey(groupDn))
            {
                return null;
            }

            var name = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(groupDn) ?? groupDn;
            return new AdEffectiveGroupResolvedGroup(groupDn, name, name, name, null);
        }

        IReadOnlyList<string> GetParents(string groupDn) =>
            parentMap.TryGetValue(groupDn, out var parents) ? parents : [];

        return AdEffectiveGroupMembershipResolver.Build(
            SampleUser,
            directGroupDns,
            ResolveGroup,
            GetParents,
            maxDepth);
    }
}
