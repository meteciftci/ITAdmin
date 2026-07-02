using System.Reflection;
using System.Text.Json;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdGroupOuMoveTests
{
    [Fact]
    public void GroupsMoveOuPermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Groups.MoveOu", AdManagementPermissions.GroupsMoveOu);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsGroupsMoveOu()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        var containsMoveOu = permissions.Cast<object>().Any(item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.GroupsMoveOu, StringComparison.Ordinal);
        });

        Assert.True(containsMoveOu);
    }

    [Fact]
    public void MoveGroupOuEndpoint_RequiresGroupsMoveOuPermission()
    {
        var method = typeof(AdGroupsController).GetMethod(nameof(AdGroupsController.MoveGroupOu));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.GroupsMoveOu,
            permissionAttribute?.Policy);
    }

    [Theory]
    [InlineData("OU=Child,OU=Groups,DC=corp,DC=local", "OU=Groups,DC=corp,DC=local", true)]
    [InlineData("OU=Groups,DC=corp,DC=local", "OU=Groups,DC=corp,DC=local", true)]
    [InlineData("OU=Users,DC=corp,DC=local", "OU=Groups,DC=corp,DC=local", false)]
    [InlineData("OU=Servers,OU=Computers,DC=corp,DC=local", "OU=Groups,DC=corp,DC=local", false)]
    public void TargetOu_MustBeGroupsSearchBaseOrDescendant(
        string targetOu,
        string groupsSearchBase,
        bool expectedAllowed)
    {
        var isAllowed = AdLdapDnHelper.IsEqualOrDescendantOf(targetOu, groupsSearchBase);
        Assert.Equal(expectedAllowed, isAllowed);
    }

    [Fact]
    public void SameParentOu_IsDetectedForGroups()
    {
        var groupDn = "CN=VPN Users,OU=Source,OU=Groups,DC=corp,DC=local";
        var targetOu = "OU=Source,OU=Groups,DC=corp,DC=local";

        var parentOu = AdLdapDnHelper.GetParentDistinguishedName(groupDn);

        Assert.True(AdLdapDnHelper.AreDistinguishedNamesEqual(parentOu, targetOu));
    }

    [Fact]
    public void GroupOuMove_RequestSummary_ContainsOperationGroupAndTargetOu()
    {
        var groupId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        const string sourceDn = "CN=VPN Users,OU=Source,OU=Groups,DC=corp,DC=local";
        const string targetOu = "OU=Target,OU=Groups,DC=corp,DC=local";

        var json = AdOperationLogSnapshotBuilder.BuildGroupOuMoveRequestSummary(groupId, sourceDn, targetOu);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(AdManagementOperationTypes.GroupMoveOu, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal(groupId.ToString("D"), document.RootElement.GetProperty("groupId").GetString());
        Assert.Equal(sourceDn, document.RootElement.GetProperty("sourceDistinguishedName").GetString());
        Assert.Equal(targetOu, document.RootElement.GetProperty("targetOuDistinguishedName").GetString());
    }

    [Fact]
    public void GroupOuMove_SuccessSnapshots_ContainGroupAndOu()
    {
        var group = new Application.Common.Models.AdGroupDetail(
            "550e8400-e29b-41d4-a716-446655440000",
            "CN=VPN Users,OU=Source,OU=Groups,DC=corp,DC=local",
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
            0,
            0,
            [],
            [],
            false,
            false);
        const string beforeOu = "OU=Source,OU=Groups,DC=corp,DC=local";
        const string afterOu = "OU=Target,OU=Groups,DC=corp,DC=local";

        var beforeJson = AdOperationLogSnapshotBuilder.BuildGroupOuMoveBeforeSnapshot(group, beforeOu);
        var afterGroup = group with { DistinguishedName = "CN=VPN Users,OU=Target,OU=Groups,DC=corp,DC=local" };
        var afterJson = AdOperationLogSnapshotBuilder.BuildGroupOuMoveAfterSnapshot(afterGroup, afterOu);

        using var beforeDocument = JsonDocument.Parse(beforeJson);
        using var afterDocument = JsonDocument.Parse(afterJson);

        Assert.Equal(
            group.DistinguishedName,
            beforeDocument.RootElement.GetProperty("group").GetProperty("distinguishedName").GetString());
        Assert.Equal(
            beforeOu,
            beforeDocument.RootElement.GetProperty("ou").GetProperty("distinguishedName").GetString());
        Assert.Equal(
            afterGroup.DistinguishedName,
            afterDocument.RootElement.GetProperty("group").GetProperty("distinguishedName").GetString());
        Assert.Equal(
            afterOu,
            afterDocument.RootElement.GetProperty("ou").GetProperty("distinguishedName").GetString());
    }

    [Fact]
    public void GroupOuMove_FailureDiagnostic_UsesAdGroupOuMoveFailedCode()
    {
        var groupId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var diagnosticJson = AdOperationErrorDiagnosticBuilder.BuildGroupOuMoveFailureJson(
            "MoveGroup",
            groupId,
            "CN=VPN Users,OU=Source,DC=corp,DC=local",
            normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject);

        var extractedCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson);

        Assert.Equal(AdOperationDiagnosticCodes.GroupOuMoveFailed, extractedCode);
        using var document = JsonDocument.Parse(diagnosticJson);
        Assert.Equal(AdOperationDiagnosticCodes.GroupOuMoveFailed, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(AdManagementOperationTypes.GroupMoveOu, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal("MoveGroup", document.RootElement.GetProperty("step").GetString());
        Assert.False(document.RootElement.GetProperty("partialUpdate").GetBoolean());
        Assert.Equal("NotRequired", document.RootElement.GetProperty("rollbackStatus").GetString());
    }

    [Fact]
    public void ResolveDefaultCode_GroupOuMove_ReturnsAdGroupOuMoveFailed()
    {
        Assert.Equal(
            AdOperationDiagnosticCodes.GroupOuMoveFailed,
            AdOperationErrorDiagnosticBuilder.ResolveDefaultCode(AdManagementOperationTypes.GroupMoveOu));
    }
}
