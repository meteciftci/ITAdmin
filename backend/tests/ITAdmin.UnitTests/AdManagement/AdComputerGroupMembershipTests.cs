using System.Reflection;
using System.Text.Json;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdComputerGroupMembershipTests
{
    private static readonly Guid ComputerId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");

    [Fact]
    public void ComputersGroupsPermissionConstants_AreDefined()
    {
        Assert.Equal("AdManagement.Computers.Groups.View", AdManagementPermissions.ComputersGroupsView);
        Assert.Equal("AdManagement.Computers.Groups.Add", AdManagementPermissions.ComputersGroupsAdd);
        Assert.Equal("AdManagement.Computers.Groups.Remove", AdManagementPermissions.ComputersGroupsRemove);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsComputerGroupPermissions()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersGroupsView, StringComparison.Ordinal);
        });
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersGroupsAdd, StringComparison.Ordinal);
        });
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersGroupsRemove, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ComputerGroupOperationTypes_AreDefined()
    {
        Assert.Equal("ComputerGroupAdd", AdManagementOperationTypes.ComputerGroupAdd);
        Assert.Equal("ComputerGroupRemove", AdManagementOperationTypes.ComputerGroupRemove);
    }

    [Fact]
    public void GetComputerGroupsEndpoint_RequiresComputersGroupsViewPermission()
    {
        var method = typeof(AdComputersController).GetMethod(nameof(AdComputersController.GetComputerGroups));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersGroupsView,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void SearchComputerGroupCandidatesEndpoint_RequiresComputersGroupsAddPermission()
    {
        var method = typeof(AdComputersController).GetMethod(nameof(AdComputersController.SearchComputerGroupCandidates));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersGroupsAdd,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void AddComputerToGroupEndpoint_RequiresComputersGroupsAddPermission()
    {
        var method = typeof(AdComputersController).GetMethod(nameof(AdComputersController.AddComputerToGroup));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersGroupsAdd,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void RemoveComputerFromGroupEndpoint_RequiresComputersGroupsRemovePermission()
    {
        var method = typeof(AdComputersController).GetMethod(nameof(AdComputersController.RemoveComputerFromGroup));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersGroupsRemove,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void ComputerGroupAdd_RequestSummary_ContainsOperationComputerAndGroupIntent()
    {
        var json = AdOperationLogSnapshotBuilder.BuildComputerGroupMembershipRequestSummary(
            AdManagementOperationTypes.ComputerGroupAdd,
            ComputerId,
            "CN=VPN Users,DC=example,DC=com",
            "VPN Users");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdManagementOperationTypes.ComputerGroupAdd, root.GetProperty("operation").GetString());
        Assert.Equal(ComputerId.ToString("D"), root.GetProperty("computerId").GetString());
        Assert.Equal("CN=VPN Users,DC=example,DC=com", root.GetProperty("groupDistinguishedName").GetString());
    }

    [Fact]
    public void ComputerGroupAdd_BeforeSnapshot_ContainsDirectMemberFalse()
    {
        var json = AdOperationLogSnapshotBuilder.BuildComputerGroupMembershipBeforeSnapshot(
            AdManagementOperationTypes.ComputerGroupAdd,
            ComputerId.ToString("D"),
            "PC01$",
            "PC01",
            "CN=PC01,OU=Computers,DC=example,DC=com",
            "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "VPN Users",
            "VPN Users",
            "vpn-users",
            "CN=VPN Users,DC=example,DC=com",
            isDirectMember: false);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdManagementOperationTypes.ComputerGroupAdd, root.GetProperty("operation").GetString());
        Assert.Equal("PC01$", root.GetProperty("computer").GetProperty("samAccountName").GetString());
        Assert.Equal("VPN Users", root.GetProperty("group").GetProperty("name").GetString());
        Assert.False(root.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void ComputerGroupAdd_AfterSnapshot_ContainsDirectMemberTrue()
    {
        var json = AdOperationLogSnapshotBuilder.BuildComputerGroupMembershipAfterSnapshot(
            AdManagementOperationTypes.ComputerGroupAdd,
            ComputerId.ToString("D"),
            "PC01$",
            "PC01",
            "CN=PC01,OU=Computers,DC=example,DC=com",
            "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "VPN Users",
            "VPN Users",
            "vpn-users",
            "CN=VPN Users,DC=example,DC=com",
            isDirectMember: true);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void ComputerGroupRemove_BeforeSnapshot_ContainsDirectMemberTrue()
    {
        var json = AdOperationLogSnapshotBuilder.BuildComputerGroupMembershipBeforeSnapshot(
            AdManagementOperationTypes.ComputerGroupRemove,
            ComputerId.ToString("D"),
            "PC01$",
            "PC01",
            "CN=PC01,OU=Computers,DC=example,DC=com",
            "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "VPN Users",
            "VPN Users",
            "vpn-users",
            "CN=VPN Users,DC=example,DC=com",
            isDirectMember: true);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void ComputerGroupRemove_AfterSnapshot_ContainsDirectMemberFalse()
    {
        var json = AdOperationLogSnapshotBuilder.BuildComputerGroupMembershipAfterSnapshot(
            AdManagementOperationTypes.ComputerGroupRemove,
            ComputerId.ToString("D"),
            "PC01$",
            "PC01",
            "CN=PC01,OU=Computers,DC=example,DC=com",
            "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "VPN Users",
            "VPN Users",
            "vpn-users",
            "CN=VPN Users,DC=example,DC=com",
            isDirectMember: false);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void ComputerGroupAdd_FailureDiagnostic_UsesComputerGroupAddFailedCode()
    {
        var json = AdOperationErrorDiagnosticBuilder.BuildGroupMembershipFailureJson(
            AdManagementOperationTypes.ComputerGroupAdd,
            "ModifyGroupMembership",
            ComputerId,
            "CN=PC01,OU=Computers,DC=example,DC=com");

        var extractedCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(json);
        Assert.Equal(AdOperationDiagnosticCodes.ComputerGroupAddFailed, extractedCode);
    }

    [Theory]
    [InlineData(516, 0x2000 | 0x04000000, true, true)]
    [InlineData(515, 4096, false, false)]
    public void IsProtectedComputer_BlocksDomainControllerAndCriticalSignals(
        int primaryGroupId,
        int userAccountControl,
        bool isCriticalSystemObject,
        bool expectedProtected)
    {
        var isProtected = AdComputerAccountGuard.IsProtectedComputer(
            primaryGroupId,
            userAccountControl,
            isCriticalSystemObject);

        Assert.Equal(expectedProtected, isProtected);
    }

    [Fact]
    public void BuildSecurityGroupSearchFilter_IncludesSecurityEnabledBit()
    {
        var filter = AdLdapGroupFilterHelper.BuildSecurityGroupSearchFilter("vpn");
        Assert.Contains("groupType:1.2.840.113556.1.4.803:=2147483648", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildComputerObjectGuidFilter_IncludesComputerObjectClasses()
    {
        var filter = AdLdapComputerFilterHelper.BuildComputerObjectGuidFilter(ComputerId);
        Assert.Contains("objectCategory=computer", filter, StringComparison.Ordinal);
        Assert.Contains("objectClass=computer", filter, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
