using System.Reflection;
using System.Text.Json;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdGroupMemberManagementTests
{
    private static readonly Guid GroupId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

    [Fact]
    public void GroupsManageMembersPermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Groups.ManageMembers", AdManagementPermissions.GroupsManageMembers);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsGroupsManageMembers()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.GroupsManageMembers, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void GroupMemberOperationTypes_AreDefined()
    {
        Assert.Equal("GroupMemberAdd", AdManagementOperationTypes.GroupMemberAdd);
        Assert.Equal("GroupMemberRemove", AdManagementOperationTypes.GroupMemberRemove);
    }

    [Fact]
    public void GetGroupMembersEndpoint_RequiresGroupsViewPermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.GetGroupMembers));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsView,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void SearchGroupMemberCandidatesEndpoint_RequiresGroupsManageMembersPermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.SearchGroupMemberCandidates));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsManageMembers,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void AddGroupMemberEndpoint_RequiresGroupsManageMembersPermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.AddGroupMember));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsManageMembers,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void RemoveGroupMemberEndpoint_RequiresGroupsManageMembersPermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.RemoveGroupMember));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsManageMembers,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void GroupMemberAdd_RequestSummary_ContainsGroupAndMemberIntent()
    {
        var json = AdOperationLogSnapshotBuilder.BuildGroupMemberOperationRequestSummary(
            AdManagementOperationTypes.GroupMemberAdd,
            GroupId.ToString("D"),
            "VPN Users",
            "vpn-users",
            "CN=VPN Users,DC=example,DC=com",
            "user",
            "mete.ciftci",
            "mete.ciftci",
            "CN=Mete,DC=example,DC=com");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdManagementOperationTypes.GroupMemberAdd, root.GetProperty("operation").GetString());
        Assert.Equal(GroupId.ToString("D"), root.GetProperty("groupId").GetString());
        Assert.Equal("VPN Users", root.GetProperty("groupName").GetString());
        Assert.Equal("user", root.GetProperty("memberType").GetString());
        Assert.Equal("CN=Mete,DC=example,DC=com", root.GetProperty("memberDistinguishedName").GetString());
    }

    [Fact]
    public void GroupMemberAdd_BeforeSnapshot_ContainsDirectMemberFalse()
    {
        var group = CreateGroupDetail();
        var member = CreateMemberSnapshot();

        var json = AdOperationLogSnapshotBuilder.BuildGroupMemberOperationBeforeSnapshot(
            AdManagementOperationTypes.GroupMemberAdd,
            group,
            member,
            isDirectMember: false);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdManagementOperationTypes.GroupMemberAdd, root.GetProperty("operation").GetString());
        Assert.Equal("VPN Users", root.GetProperty("group").GetProperty("name").GetString());
        Assert.Equal("mete.ciftci", root.GetProperty("member").GetProperty("samAccountName").GetString());
        Assert.False(root.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void GroupMemberAdd_AfterSnapshot_ContainsDirectMemberTrue()
    {
        var group = CreateGroupDetail();
        var member = CreateMemberSnapshot();

        var json = AdOperationLogSnapshotBuilder.BuildGroupMemberOperationAfterSnapshot(
            AdManagementOperationTypes.GroupMemberAdd,
            group,
            member,
            isDirectMember: true);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void GroupMemberRemove_BeforeSnapshot_ContainsDirectMemberTrue()
    {
        var group = CreateGroupDetail();
        var member = CreateMemberSnapshot();

        var json = AdOperationLogSnapshotBuilder.BuildGroupMemberOperationBeforeSnapshot(
            AdManagementOperationTypes.GroupMemberRemove,
            group,
            member,
            isDirectMember: true);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void GroupMemberRemove_AfterSnapshot_ContainsDirectMemberFalse()
    {
        var group = CreateGroupDetail();
        var member = CreateMemberSnapshot();

        var json = AdOperationLogSnapshotBuilder.BuildGroupMemberOperationAfterSnapshot(
            AdManagementOperationTypes.GroupMemberRemove,
            group,
            member,
            isDirectMember: false);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void GroupMemberAdd_FailureDiagnostic_HasExtractableCode()
    {
        var json = AdGroupMemberOperationDiagnosticBuilder.BuildPreflightJson(
            AdManagementOperationTypes.GroupMemberAdd,
            "Preflight",
            AdUserUpdateNormalizedReasons.AlreadyMember,
            "The member is already a direct member of this group.",
            GroupId);

        var code = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(json);
        Assert.Equal(AdOperationDiagnosticCodes.GroupMemberAddPreflightFailed, code);
    }

    [Fact]
    public void BuildSecurityGroupMemberCandidateSearchFilter_IncludesSecurityEnabledBit()
    {
        var filter = AdLdapGroupFilterHelper.BuildSecurityGroupMemberCandidateSearchFilter("vpn");
        Assert.Contains("groupType:1.2.840.113556.1.4.803:=2147483648", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildComputerMemberCandidateSearchFilter_IncludesComputerCategory()
    {
        var filter = AdLdapGroupFilterHelper.BuildComputerMemberCandidateSearchFilter("pc01");
        Assert.Contains("(objectCategory=computer)", filter, StringComparison.Ordinal);
        Assert.Contains("dNSHostName=*pc01*", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUserMemberCandidateSearchFilter_ExcludesDeletedUsers()
    {
        var filter = AdLdapGroupFilterHelper.BuildUserMemberCandidateSearchFilter("mete");
        Assert.Contains("(!(isDeleted=TRUE))", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void MemberListLimits_ArePositive()
    {
        Assert.True(AdGroupDirectoryLimits.MemberListDefaultPageSize > 0);
        Assert.True(AdGroupDirectoryLimits.MemberListMaxPageSize >= AdGroupDirectoryLimits.MemberListDefaultPageSize);
        Assert.True(AdGroupDirectoryLimits.MemberCandidateMaxPageSize > 0);
    }

    private static AdGroupDetail CreateGroupDetail() =>
        new(
            GroupId.ToString("D"),
            "CN=VPN Users,DC=example,DC=com",
            "VPN Users",
            "VPN Users",
            "VPN Users",
            "vpn-users",
            "VPN access",
            "Global",
            true,
            -2147483646,
            null,
            null,
            null,
            null,
            1,
            0,
            [],
            [],
            false,
            false);

    private static AdGroupMemberSnapshotInfo CreateMemberSnapshot() =>
        new(
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "User",
            "Mete Çiftçi",
            "mete.ciftci",
            "mete.ciftci",
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            null,
            null,
            "CN=Mete,DC=example,DC=com");
}
