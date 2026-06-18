using SasPortal.Application.Common.Models;
using SasPortal.Infrastructure.Services;

namespace SasPortal.UnitTests.Services;

public sealed class LdapServiceTests
{
    [Fact]
    public async Task ValidateAsync_WhenUseSslDisabled_FailsWithoutConnecting()
    {
        var service = new LdapService();
        var request = new LdapValidationRequest
        {
            Host = "dc01.test",
            Port = 389,
            UseSsl = false,
            BaseDn = "DC=test,DC=local",
            UserSearchFilter = "(&(objectClass=user)(sAMAccountName={0}))",
            BindUserName = "bind",
            BindPassword = "bindpw",
            TestUserName = "admin",
            TestPassword = "adminpw",
        };

        var result = await service.ValidateAsync(request);

        Assert.False(result.IsValid);
    }
}
