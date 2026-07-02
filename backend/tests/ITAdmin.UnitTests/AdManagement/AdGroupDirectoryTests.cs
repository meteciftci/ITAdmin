using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdGroupDirectoryTests
{
    [Fact]
    public void GroupsViewPermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Groups.View", AdManagementPermissions.GroupsView);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsGroupsView()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        var containsGroupsView = permissions.Cast<object>().Any(item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.GroupsView, StringComparison.Ordinal);
        });

        Assert.True(containsGroupsView);
    }

    [Fact]
    public void ListGroupsEndpoint_RequiresGroupsViewPermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.ListGroups));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsView,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void GetGroupByIdEndpoint_RequiresGroupsViewPermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.GetGroupById));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsView,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void BuildSecurityGroupSearchFilter_IncludesSecurityEnabledBit()
    {
        var filter = AdLdapGroupFilterHelper.BuildSecurityGroupSearchFilter("vpn");

        Assert.Contains("groupType:1.2.840.113556.1.4.803:=2147483648", filter, StringComparison.Ordinal);
        Assert.Contains("(objectCategory=group)", filter, StringComparison.Ordinal);
        Assert.Contains("(objectClass=group)", filter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("displayName")]
    [InlineData("name")]
    [InlineData("cn")]
    [InlineData("sAMAccountName")]
    [InlineData("description")]
    [InlineData("distinguishedName")]
    public void BuildSecurityGroupSearchFilter_IncludesSearchField(string fieldName)
    {
        var filter = AdLdapGroupFilterHelper.BuildSecurityGroupSearchFilter("test");

        Assert.Contains($"{fieldName}=*test*", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSecurityGroupSearchFilter_EscapesSpecialCharacters()
    {
        var filter = AdLdapGroupFilterHelper.BuildSecurityGroupSearchFilter("(vpn*)");

        Assert.Contains("\\28vpn\\2a\\29", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("(vpn*)", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSecurityGroupObjectGuidFilter_IncludesSecurityEnabledBit()
    {
        var objectGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var filter = AdLdapGroupFilterHelper.BuildSecurityGroupObjectGuidFilter(objectGuid);

        Assert.Contains("groupType:1.2.840.113556.1.4.803:=2147483648", filter, StringComparison.Ordinal);
        Assert.Contains("objectGUID=", filter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-2147483646, true, AdGroupScope.Global)]
    [InlineData(-2147483644, true, AdGroupScope.DomainLocal)]
    [InlineData(-2147483640, true, AdGroupScope.Universal)]
    [InlineData(2, false, AdGroupScope.Global)]
    [InlineData(4, false, AdGroupScope.DomainLocal)]
    [InlineData(8, false, AdGroupScope.Universal)]
    [InlineData(0, false, AdGroupScope.Unknown)]
    public void ParseGroupType_ResolvesSecurityAndScope(
        int groupTypeRaw,
        bool expectedSecurityEnabled,
        AdGroupScope expectedScope)
    {
        var parsed = AdGroupTypeHelper.Parse(groupTypeRaw);

        Assert.Equal(expectedSecurityEnabled, parsed.SecurityEnabled);
        Assert.Equal(expectedScope, parsed.Scope);
        Assert.Equal(AdGroupTypeHelper.ScopeToCode(expectedScope), AdGroupTypeHelper.ScopeToCode(parsed.Scope));
    }

    [Fact]
    public void MemberDisplayLimits_ArePositive()
    {
        Assert.True(AdGroupDirectoryLimits.MemberDisplayLimit > 0);
        Assert.True(AdGroupDirectoryLimits.MemberOfDisplayLimit > 0);
    }

    [Fact]
    public void GroupsCreatePermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Groups.Create", AdManagementPermissions.GroupsCreate);
        Assert.Equal("AdManagement.Groups.Update", AdManagementPermissions.GroupsUpdate);
        Assert.Equal("AdManagement.Groups.Delete", AdManagementPermissions.GroupsDelete);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsGroupsCreateAndUpdate()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.GroupsCreate, StringComparison.Ordinal);
        });
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.GroupsUpdate, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CreateGroupEndpoint_RequiresGroupsCreatePermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.CreateGroup));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsCreate,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void UpdateGroupEndpoint_RequiresGroupsUpdatePermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.UpdateGroup));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsUpdate,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsGroupsDelete()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.GroupsDelete, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void DeleteGroupEndpoint_RequiresGroupsDeletePermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.DeleteGroup));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsDelete,
            permissionAttribute?.Policy);
    }
}
