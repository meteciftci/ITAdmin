using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdGroupUpdateChangePlanBuilderTests
{
    private static readonly Guid GroupId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

    [Fact]
    public void Build_PlanIncludesRenameWhenTechnicalNameChanges()
    {
        var request = new UpdateAdGroupRequest(
            GroupId,
            "Display",
            "new-cn",
            "group.sam",
            "desc",
            null,
            null,
            null,
            null);

        var currentDn = "CN=old-cn,OU=Groups,DC=example,DC=com";
        var currentScalars = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["displayName"] = "Display",
            ["sAMAccountName"] = "group.sam",
            ["description"] = "desc",
        };

        var plan = AdGroupUpdateChangePlanBuilder.Build(request, currentScalars, currentDn);

        Assert.True(plan.RequiresRename);
        Assert.NotNull(plan.RenameChange);
        Assert.Equal("old-cn", plan.RenameChange!.CurrentCommonName);
        Assert.Equal("new-cn", plan.RenameChange.RequestedCommonName);
    }

    [Fact]
    public void Build_PlanDeletesDescriptionWhenCleared()
    {
        var request = new UpdateAdGroupRequest(
            GroupId,
            "Display",
            "group-cn",
            "group.sam",
            string.Empty,
            null,
            null,
            null,
            null);

        var currentDn = "CN=group-cn,OU=Groups,DC=example,DC=com";
        var currentScalars = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["displayName"] = "Display",
            ["sAMAccountName"] = "group.sam",
            ["description"] = "old description",
        };

        var plan = AdGroupUpdateChangePlanBuilder.Build(request, currentScalars, currentDn);

        var descriptionChange = plan.ScalarChanges.Single(change =>
            string.Equals(change.AttributeName, "description", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(AdUserUpdateScalarChangeKind.Delete, descriptionChange.ChangeKind);
    }

    [Fact]
    public void BuildSecurityGroupType_ReturnsSecurityEnabledScopeValues()
    {
        Assert.Equal(-2147483646, AdGroupTypeHelper.BuildSecurityGroupType(AdGroupScope.Global));
        Assert.Equal(-2147483644, AdGroupTypeHelper.BuildSecurityGroupType(AdGroupScope.DomainLocal));
        Assert.Equal(-2147483640, AdGroupTypeHelper.BuildSecurityGroupType(AdGroupScope.Universal));
    }
}
