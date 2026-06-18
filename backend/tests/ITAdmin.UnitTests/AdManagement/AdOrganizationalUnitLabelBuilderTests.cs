using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdOrganizationalUnitLabelBuilderTests
{
    [Fact]
    public void Build_PrefersDisplayNameThenOuThenNameThenParsedDn()
    {
        const string dn = "OU=BT,OU=Users,DC=example,DC=com";

        Assert.Equal(
            "BT Display",
            AdOrganizationalUnitLabelBuilder.Build(dn, "BT Display", "BT Name", "BT OU"));

        Assert.Equal(
            "BT OU",
            AdOrganizationalUnitLabelBuilder.Build(dn, null, "BT Name", "BT OU"));

        Assert.Equal(
            "BT Name",
            AdOrganizationalUnitLabelBuilder.Build(dn, null, "BT Name", null));

        Assert.Equal(
            "BT",
            AdOrganizationalUnitLabelBuilder.Build(dn, null, null, null));
    }
}
