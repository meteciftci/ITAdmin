using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.Services;

public sealed class SetupServiceTests
{
    [Fact]
    public void BuildLdapUserNameCandidates_WithDomainSlashUser_ReturnsOriginalThenSamAccountName()
    {
        var result = SetupService.BuildLdapUserNameCandidates(@"DOMAIN\user");
        Assert.Equal(new[] { @"DOMAIN\user", "user" }, result);
    }

    [Fact]
    public async Task CompleteSetupAsync_RejectsInvalidSetupKey()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("wrong-key", ["user"]);

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal("Invalid setup key.", result.Message);
    }

    [Fact]
    public async Task CompleteSetupAsync_RejectsWhenNoAdminUsersProvided()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", []);

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal("At least one admin user is required.", result.Message);
    }

    [Fact]
    public async Task CompleteSetupAsync_RejectsDuplicateAdminUsers()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = new CompleteSetupRequest(
            "setup-secret",
            CreateMinimalLdapSettings(),
            [
                new CompleteSetupAdminUser("user1", null, "11111111-1111-1111-1111-111111111111"),
                new CompleteSetupAdminUser("user2", null, "11111111-1111-1111-1111-111111111111"),
            ]);

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal("Duplicate admin user selection is not allowed.", result.Message);
    }

    [Fact]
    public async Task CompleteSetupAsync_AssignsSuperAdminRoleToAllSelectedAdminUsers()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        ldap.ResolveUserProfile = request => request.UserName switch
        {
            "admin1" => new LdapUserProfile("obj-1", "admin1", "Admin One", "admin1@corp.test"),
            "admin2" => new LdapUserProfile("obj-2", "admin2", "Admin Two", "admin2@corp.test"),
            _ => null,
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", ["admin1", "admin2"]);

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        var superAdminRole = await context.PortalRoles.SingleAsync(x => x.Code == "SuperAdmin");
        var adminUsers = await context.PortalUsers.ToListAsync();
        Assert.Equal(2, adminUsers.Count);
        Assert.All(adminUsers, user => Assert.Null(user.NationalIdEncrypted));

        foreach (var adminUser in adminUsers)
        {
            Assert.True(await context.PortalUserRoles.AnyAsync(
                x => x.PortalUserId == adminUser.Id && x.PortalRoleId == superAdminRole.Id));
        }
    }

    [Fact]
    public async Task CompleteSetupAsync_WorksWithoutAdminPassword()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        ldap.ResolveUserProfile = _ => new LdapUserProfile("obj-3", "plain", "Plain User", "plain@ad.test");

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", ["plain"]);

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        Assert.Equal(0, ldap.ValidateCallCount);
        Assert.True(ldap.ValidateBindCallCount > 0);
    }

    [Fact]
    public async Task CompleteSetupAsync_DoesNotPersistNationalIdApplicationSetting()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        ldap.ResolveUserProfile = _ => new LdapUserProfile("obj-4", "plain", "Plain User", "plain@ad.test");

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", ["plain"]);

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        Assert.False(await context.ApplicationSettings.AnyAsync(x => x.Key == "Directory:NationalIdAttribute"));
    }

    [Fact]
    public async Task CompleteSetupAsync_DoesNotPersistAdManagementSettings()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        ldap.ResolveUserProfile = _ => new LdapUserProfile("obj-5", "plain", "Plain User", null);

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", ["plain"]);

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        Assert.False(await context.AdManagementSettings.AnyAsync());
    }

    [Fact]
    public async Task CompleteSetupAsync_RepairsAuthoritativePermissionMetadata()
    {
        await using var context = CreateDbContext();
        context.PortalPermissions.Add(new PortalPermission
        {
            Module = "LegacyArea",
            Code = PermissionCodes.Users.View,
            Description = "Outdated description.",
            IsActive = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "test",
        });
        await context.SaveChangesAsync();

        var ldap = CreateSuccessfulLdapFake();
        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", ["admin"]);

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        var permission = await context.PortalPermissions.SingleAsync(
            item => item.Code == PermissionCodes.Users.View);
        Assert.Equal("Users", permission.Module);
        Assert.Equal("View users.", permission.Description);
        Assert.False(permission.IsActive);
        Assert.Equal("setup", permission.UpdatedBy);
    }

    [Fact]
    public async Task CompleteSetupAsync_WhenProfileLookupFails_ReturnsDirectoryFailureMessage()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        ldap.ResolveUserProfile = _ => null;

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", ["someuser"]);

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal(SetupService.AdminUserNotFoundInDirectoryMessage, result.Message);
    }

    [Fact]
    public async Task SearchAdminUsersAsync_ReturnsEmptyList_WhenSearchShorterThanMinimum()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        var service = CreateSetupService(context, ldap, "setup-secret");

        var result = await service.SearchAdminUsersAsync(
            new SearchSetupAdminUsersRequest(
                "setup-secret",
                CreateMinimalLdapSettings(),
                "a"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Users);
        Assert.Equal(0, ldap.SearchUsersCallCount);
    }

    [Fact]
    public async Task SearchAdminUsersAsync_ReturnsFailure_WhenSetupKeyInvalid()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        var service = CreateSetupService(context, ldap, "setup-secret");

        var result = await service.SearchAdminUsersAsync(
            new SearchSetupAdminUsersRequest(
                "wrong-key",
                CreateMinimalLdapSettings(),
                "admin"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid setup key.", result.ErrorMessage);
    }

    [Fact]
    public async Task SearchAdminUsersAsync_DoesNotReturnSecrets()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        ldap.SearchUsersResult =
        [
            new LdapUserLookupItem(
                "obj-7",
                "admin",
                "Admin User",
                "admin@corp.test",
                "CN=admin,OU=Users,DC=test,DC=local")
        ];

        var service = CreateSetupService(context, ldap, "setup-secret");
        var result = await service.SearchAdminUsersAsync(
            new SearchSetupAdminUsersRequest(
                "setup-secret",
                CreateMinimalLdapSettings(),
                "admin"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(result.Users);
        Assert.Equal("admin", user.UserName);
        Assert.DoesNotContain("bindpw", user.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteSetupAsync_PersistsEmptyUserSearchBase_NotBaseDn()
    {
        await using var context = CreateDbContext();
        var ldap = CreateSuccessfulLdapFake();
        ldap.ResolveUserProfile = _ => new LdapUserProfile("obj-empty-usb", "plain", "Plain User", null);

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("setup-secret", ["plain"]);

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        var ldapSetting = await context.LdapSettings.SingleAsync();
        Assert.Equal(string.Empty, ldapSetting.UserSearchBase);
        Assert.NotEqual(ldapSetting.BaseDn, ldapSetting.UserSearchBase);
    }


    private static FakeLdapService CreateSuccessfulLdapFake()
    {
        return new FakeLdapService
        {
            ValidateBindResult = new(true, "bind ok"),
            ValidateSearchBasesResult = new(true, "bases ok"),
            ResolveUserProfile = request => new LdapUserProfile(
                "obj-default",
                request.UserName,
                request.UserName,
                $"{request.UserName}@corp.test"),
        };
    }

    private static SetupService CreateSetupService(
        AppDbContext context,
        FakeLdapService ldap,
        string setupKeyPlaintext,
        bool includeSetupKeyHash = true,
        string? configuredSetupKeyHash = null)
    {
        var values = new Dictionary<string, string?>();
        if (includeSetupKeyHash)
        {
            values[SetupKeyHashValidator.ConfigurationKey] =
                configuredSetupKeyHash ?? SetupKeyHashValidator.ComputeConfiguredHash(setupKeyPlaintext);
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new SetupService(
            context,
            ldap,
            new FakeSecretProtector(),
            configuration,
            new SetupKeyHashValidator(),
            NullLogger<SetupService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static CompleteSetupLdapSettings CreateMinimalLdapSettings() =>
        new(
            Name: "Default LDAP",
            Host: "dc01.test",
            BaseDn: "DC=test,DC=local",
            UserSearchFilter: "(&(objectClass=user)(sAMAccountName={0}))",
            BindUserName: "bind",
            BindUserDomain: null,
            BindPassword: "bindpw");

    private static CompleteSetupRequest CreateMinimalCompleteRequest(
        string setupKey,
        IReadOnlyList<string> adminUserNames)
    {
        return new CompleteSetupRequest(
            setupKey,
            CreateMinimalLdapSettings(),
            adminUserNames
                .Select(name => new CompleteSetupAdminUser(name, null, null))
                .ToList());
    }
}
