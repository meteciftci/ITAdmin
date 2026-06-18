using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdManagementControllerEffectiveGroupsTests
{
    [Fact]
    public void GetUserEffectiveGroups_UsesUsersGroupsViewPermission()
    {
        var method = typeof(AdManagementController)
            .GetMethod(
                nameof(AdManagementController.GetUserEffectiveGroups),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        Assert.NotNull(method);

        var permissionAttribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permissionAttribute);
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.UsersGroupsView,
            permissionAttribute!.Policy);

        var httpGet = method.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGet);
        Assert.Equal("users/{id}/effective-groups", httpGet!.Template);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void EffectiveGroupMaxDepthLimits_MatchResolverConstants(int outOfRangeValue)
    {
        Assert.True(outOfRangeValue < AdEffectiveGroupMembershipLimits.MinMaxDepth
            || outOfRangeValue > AdEffectiveGroupMembershipLimits.MaxMaxDepth);
        Assert.Equal(1, AdEffectiveGroupMembershipLimits.MinMaxDepth);
        Assert.Equal(10, AdEffectiveGroupMembershipLimits.MaxMaxDepth);
        Assert.Equal(5, AdEffectiveGroupMembershipLimits.DefaultMaxDepth);
        Assert.Equal(500, AdEffectiveGroupMembershipLimits.MaxResultCount);
    }
}
