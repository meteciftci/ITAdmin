using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdLdapDnHelperTests
{
    [Theory]
    [InlineData("CN=Domain Users,CN=Users,DC=example,DC=com", "Domain Users")]
    [InlineData("CN=BT Adminleri,OU=Groups,DC=example,DC=com", "BT Adminleri")]
    [InlineData("cn=escaped\\, group,OU=Groups,DC=example,DC=com", "escaped, group")]
    public void ParseCommonNameFromDistinguishedName_ReturnsFirstCnValue(
        string distinguishedName,
        string expected)
    {
        var result = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseCommonNameFromDistinguishedName_ReturnsNullForEmptyInput(string? distinguishedName)
    {
        Assert.Null(AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName));
    }

    [Fact]
    public void BuildGroupMemberships_DeduplicatesByDistinguishedName()
    {
        var memberships = AdLdapDnHelper.BuildGroupMemberships(
        [
            "CN=Domain Users,CN=Users,DC=example,DC=com",
            "cn=domain users,cn=users,dc=example,dc=com",
            "CN=VPN Users,OU=Groups,DC=example,DC=com",
        ]);

        Assert.Equal(2, memberships.Count);
        Assert.Equal("Domain Users", memberships[0].Name);
        Assert.Equal("VPN Users", memberships[1].Name);
    }

    [Fact]
    public void BuildGroupMemberships_SortsAlphabeticallyByName()
    {
        var memberships = AdLdapDnHelper.BuildGroupMemberships(
        [
            "CN=Zulu,OU=Groups,DC=example,DC=com",
            "CN=Alpha,OU=Groups,DC=example,DC=com",
        ]);

        Assert.Equal(["Alpha", "Zulu"], memberships.Select(static item => item.Name).ToArray());
    }
}
