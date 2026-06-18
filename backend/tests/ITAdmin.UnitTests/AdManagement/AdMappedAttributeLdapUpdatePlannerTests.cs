using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdMappedAttributeLdapUpdatePlannerTests
{
    [Fact]
    public void ResolveAction_ReturnsSkip_WhenClearRequestedButAttributeAbsentInAd()
    {
        var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(null, Array.Empty<string>());

        Assert.Equal(AdMappedAttributeLdapAction.Skip, action);
    }

    [Fact]
    public void ResolveAction_ReturnsDelete_WhenClearRequestedAndAttributeExistsInAd()
    {
        var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(
            null,
            ["existing"]);

        Assert.Equal(AdMappedAttributeLdapAction.Delete, action);
    }

    [Fact]
    public void ResolveAction_ReturnsSkip_WhenValueUnchanged()
    {
        var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(
            "same",
            ["same"]);

        Assert.Equal(AdMappedAttributeLdapAction.Skip, action);
    }

    [Fact]
    public void ResolveAction_ReturnsReplace_WhenValueChanged()
    {
        var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(
            "new",
            ["old"]);

        Assert.Equal(AdMappedAttributeLdapAction.Replace, action);
    }

    [Fact]
    public void ResolveAction_ReturnsReplace_WhenValueAddedToEmptyAdAttribute()
    {
        var action = AdMappedAttributeLdapUpdatePlanner.ResolveAction(
            "new",
            Array.Empty<string>());

        Assert.Equal(AdMappedAttributeLdapAction.Replace, action);
    }
}
