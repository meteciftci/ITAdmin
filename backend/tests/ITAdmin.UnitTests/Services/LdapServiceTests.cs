using ITAdmin.Application.Common.Models;
using Microsoft.Extensions.Logging.Abstractions;
using ITAdmin.Infrastructure.Services;

namespace ITAdmin.UnitTests.Services;

public sealed class LdapServiceTests
{
    private const string Host = "dc01.test";
    private const string BaseDn = "DC=test,DC=local";
    private const string UserSearchFilter = "(&(objectClass=user)(sAMAccountName={0}))";

    [Fact]
    public async Task ValidateAsync_WhenHostMissing_FailsWithoutConnecting()
    {
        var service = new LdapService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LdapService>.Instance);
        var request = new LdapValidationRequest
        {
            Host = string.Empty,
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
    public async Task ValidateBindAsync_WhenHostMissing_FailsWithoutConnecting()
    {
        var service = new LdapService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LdapService>.Instance);
        var request = new LdapBindValidationRequest
        {
            Host = string.Empty,
            BindUserName = "bind",
            BindPassword = "bindpw",
        };

        var result = await service.ValidateBindAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateSearchBasesAsync_WhenHostMissing_FailsWithoutConnecting()
    {
        var service = new LdapService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LdapService>.Instance);
        var request = new LdapSearchBasesValidationRequest
        {
            Host = string.Empty,
            BaseDn = BaseDn,
            BindUserName = "bind",
            BindPassword = "bindpw",
        };

        var result = await service.ValidateSearchBasesAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenHostMissing_ReturnsNullWithoutConnecting()
    {
        var service = new LdapService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LdapService>.Instance);
        var request = new LdapUserProfileRequest(
            Host: string.Empty,
            BaseDn: BaseDn,
            UserSearchBase: string.Empty,
            UserSearchFilter: UserSearchFilter,
            BindUserName: "bind",
            BindUserDomain: null,
            BindPassword: "bindpw",
            UserName: "admin");

        var result = await service.GetUserProfileAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserProfileByObjectIdAsync_WhenHostMissing_ReturnsNullWithoutConnecting()
    {
        var service = new LdapService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LdapService>.Instance);
        var request = new LdapUserProfileByObjectIdRequest(
            Host: string.Empty,
            BaseDn: BaseDn,
            UserSearchBase: string.Empty,
            BindUserName: "bind",
            BindUserDomain: null,
            BindPassword: "bindpw",
            DirectoryObjectId: Guid.NewGuid().ToString("D"));

        var result = await service.GetUserProfileByObjectIdAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchUsersAsync_WhenHostMissing_ReturnsEmptyWithoutConnecting()
    {
        var service = new LdapService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LdapService>.Instance);
        var request = new LdapUserLookupRequest(
            Host: string.Empty,
            BaseDn: BaseDn,
            UserSearchBase: string.Empty,
            BindUserName: "bind",
            BindUserDomain: null,
            BindPassword: "bindpw",
            Search: "admin",
            MaxResults: 10);

        var result = await service.SearchUsersAsync(request);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ValidateSearchBasesAsync_WhenUserSearchBaseEmpty_SkipsUserSearchBaseValidation()
    {
        var service = new LdapService(NullLogger<LdapService>.Instance);
        var request = new LdapSearchBasesValidationRequest
        {
            Host = string.Empty,
            BaseDn = BaseDn,
            UserSearchBase = string.Empty,
            BindUserName = "bind",
            BindPassword = "bindpw",
        };

        var result = await service.ValidateSearchBasesAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenUserSearchBaseEmpty_UsesBaseDnFallbackWithoutConnecting()
    {
        var service = new LdapService(NullLogger<LdapService>.Instance);
        var request = new LdapUserProfileRequest(
            Host: string.Empty,
            BaseDn: BaseDn,
            UserSearchBase: string.Empty,
            UserSearchFilter: UserSearchFilter,
            BindUserName: "bind",
            BindUserDomain: null,
            BindPassword: "bindpw",
            UserName: "admin");

        var result = await service.GetUserProfileAsync(request);

        Assert.Null(result);
    }
}
