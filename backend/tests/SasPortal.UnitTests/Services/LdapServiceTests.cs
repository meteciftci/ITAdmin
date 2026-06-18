using SasPortal.Application.Common.Models;
using SasPortal.Infrastructure.Services;

namespace SasPortal.UnitTests.Services;

public sealed class LdapServiceTests
{
    private const string Host = "dc01.test";
    private const string BaseDn = "DC=test,DC=local";
    private const string UserSearchFilter = "(&(objectClass=user)(sAMAccountName={0}))";

    [Fact]
    public async Task ValidateAsync_WhenUseSslDisabled_FailsWithoutConnecting()
    {
        var service = new LdapService();
        var request = new LdapValidationRequest
        {
            Host = Host,
            Port = 389,
            UseSsl = false,
            BaseDn = BaseDn,
            UserSearchFilter = UserSearchFilter,
            BindUserName = "bind",
            BindPassword = "bindpw",
            TestUserName = "admin",
            TestPassword = "adminpw",
        };

        var result = await service.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateBindAsync_WhenUseSslDisabled_FailsWithoutConnecting()
    {
        var service = new LdapService();
        var request = new LdapBindValidationRequest
        {
            Host = Host,
            Port = 389,
            UseSsl = false,
            BindUserName = "bind",
            BindPassword = "bindpw",
        };

        var result = await service.ValidateBindAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateSearchBasesAsync_WhenUseSslDisabled_FailsWithoutConnecting()
    {
        var service = new LdapService();
        var request = new LdapSearchBasesValidationRequest
        {
            Host = Host,
            Port = 389,
            UseSsl = false,
            BaseDn = BaseDn,
            BindUserName = "bind",
            BindPassword = "bindpw",
        };

        var result = await service.ValidateSearchBasesAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenUseSslDisabled_ReturnsNullWithoutConnecting()
    {
        var service = new LdapService();
        var request = new LdapUserProfileRequest(
            Host: Host,
            Port: 389,
            UseSsl: false,
            BaseDn: BaseDn,
            UserSearchBase: string.Empty,
            UserSearchFilter: UserSearchFilter,
            BindUserName: "bind",
            BindUserDomain: null,
            BindPassword: "bindpw",
            UserName: "admin",
            NationalIdAttribute: null);

        var result = await service.GetUserProfileAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserProfileByObjectIdAsync_WhenUseSslDisabled_ReturnsNullWithoutConnecting()
    {
        var service = new LdapService();
        var request = new LdapUserProfileByObjectIdRequest(
            Host: Host,
            Port: 389,
            UseSsl: false,
            BaseDn: BaseDn,
            UserSearchBase: string.Empty,
            BindUserName: "bind",
            BindUserDomain: null,
            BindPassword: "bindpw",
            DirectoryObjectId: Guid.NewGuid().ToString("D"),
            NationalIdAttribute: null);

        var result = await service.GetUserProfileByObjectIdAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchUsersAsync_WhenUseSslDisabled_ReturnsEmptyWithoutConnecting()
    {
        var service = new LdapService();
        var request = new LdapUserLookupRequest(
            Host: Host,
            Port: 389,
            UseSsl: false,
            BaseDn: BaseDn,
            UserSearchBase: string.Empty,
            BindUserName: "bind",
            BindUserDomain: null,
            BindPassword: "bindpw",
            Search: "admin",
            MaxResults: 10,
            NationalIdAttribute: null);

        var result = await service.SearchUsersAsync(request);

        Assert.Empty(result);
    }
}
