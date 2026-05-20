using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdLdapDnHelperSubtreeTests
{
    [Theory]
    [InlineData("OU=Child,OU=Users,DC=example,DC=com", "OU=Users,DC=example,DC=com", true)]
    [InlineData("OU=Users,DC=example,DC=com", "OU=Users,DC=example,DC=com", true)]
    [InlineData("OU=Other,DC=example,DC=com", "OU=Users,DC=example,DC=com", false)]
    public void IsEqualOrDescendantOf_ValidatesOuHierarchy(
        string child,
        string ancestor,
        bool expected)
    {
        Assert.Equal(expected, AdLdapDnHelper.IsEqualOrDescendantOf(child, ancestor));
    }
}
