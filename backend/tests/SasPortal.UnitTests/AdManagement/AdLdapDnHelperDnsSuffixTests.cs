using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdLdapDnHelperDnsSuffixTests
{
    [Fact]
    public void ConvertNamingContextToDnsSuffix_ConvertsDcComponents()
    {
        var suffix = AdLdapDnHelper.ConvertNamingContextToDnsSuffix("DC=corp,DC=example,DC=com");

        Assert.Equal("corp.example.com", suffix);
    }
}
