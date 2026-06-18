using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Domain.Enums;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.Fakes;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.Services;

public sealed class UserServiceAuditTests
{
    [Fact]
    public async Task CreateUserAsync_WritesReadableAuditDescription_AndDoesNotExposeNationalId()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedActiveLdapSettingAsync(context);
        await SeedNationalIdAttributeAsync(context);

        var ldapService = new FakeLdapService
        {
            UserProfileByObjectIdResult = new LdapUserProfile(
                Guid.NewGuid().ToString(),
                "mete.user",
                "Mete User",
                "mete.user@test.local",
                "12345678901")
        };

        var service = new UserService(context, ldapService, new FakeSecretProtector());
        var result = await service.CreateUserAsync(new CreateUserRequest(
            DirectoryObjectId: Guid.NewGuid().ToString(),
            IsActive: true,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var user = Assert.Single(context.PortalUsers);
        var audit = Assert.Single(context.AuditLogs);

        Assert.Equal("Create", audit.Action);
        Assert.Equal("PortalUser", audit.EntityName);
        Assert.Equal(user.Id.ToString(), audit.EntityId);
        Assert.Equal("Portal user created: mete.user (Mete User), email: mete.user@test.local.", audit.Description);
        Assert.Equal("tester", audit.ActorUserName);
        Assert.Equal("10.20.30.40", audit.IpAddress);
        Assert.Equal("xunit-agent", audit.UserAgent);
        Assert.DoesNotContain("12345678901", audit.Description ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("protected:12345678901", audit.Description ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_TruncatesAuditIpAddressAndUserAgent()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        await SeedActiveLdapSettingAsync(context);
        await SeedNationalIdAttributeAsync(context);

        var ldapService = new FakeLdapService
        {
            UserProfileByObjectIdResult = new LdapUserProfile(
                Guid.NewGuid().ToString(),
                "mete.user",
                "Mete User",
                "mete.user@test.local",
                "12345678901")
        };

        var service = new UserService(context, ldapService, new FakeSecretProtector());
        var longIp = $"  {new string('9', 70)}  ";
        var longAgent = $"  {new string('a', 1030)}  ";

        var result = await service.CreateUserAsync(new CreateUserRequest(
            DirectoryObjectId: Guid.NewGuid().ToString(),
            IsActive: true,
            ActorUserName: "tester",
            ActorIpAddress: longIp,
            ActorUserAgent: longAgent));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.NotNull(audit.IpAddress);
        Assert.NotNull(audit.UserAgent);
        Assert.Equal(64, audit.IpAddress!.Length);
        Assert.Equal(1024, audit.UserAgent!.Length);
        Assert.Equal(new string('9', 64), audit.IpAddress);
        Assert.Equal(new string('a', 1024), audit.UserAgent);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_WhenStatusChanges_WritesActivationAuditDescription()
    {
        var actorId = Guid.NewGuid();
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var user = await SeedPortalUserAsync(context, isActive: false);
        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector());

        var result = await service.UpdateUserStatusAsync(new UpdateUserStatusRequest(
            UserId: user.Id,
            IsActive: true,
            ActorUserId: actorId,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal("Update", audit.Action);
        Assert.Equal("PortalUser", audit.EntityName);
        Assert.Equal($"Portal user activated: mete.user (Mete User). Status: Passive -> Active.", audit.Description);
        Assert.Equal(actorId, audit.ActorUserId);
        Assert.Equal("tester", audit.ActorUserName);
        Assert.Equal("10.20.30.40", audit.IpAddress);
        Assert.Equal("xunit-agent", audit.UserAgent);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_WhenNoStatusChange_DoesNotWriteAuditLog()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var user = await SeedPortalUserAsync(context, isActive: true);
        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector());

        var result = await service.UpdateUserStatusAsync(new UpdateUserStatusRequest(
            UserId: user.Id,
            IsActive: true,
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenRolesAddedAndRemoved_WritesRoleDiffAuditDescription()
    {
        var actorId = Guid.NewGuid();
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var user = await SeedPortalUserAsync(context, isActive: true);
        var userRole = await SeedRoleAsync(context, "User", "User");
        var adminRole = await SeedRoleAsync(context, "Admin", "Admin");
        var auditorRole = await SeedRoleAsync(context, "Auditor", "Auditor");

        await context.PortalUserRoles.AddAsync(new PortalUserRole
        {
            PortalUserId = user.Id,
            PortalRoleId = userRole.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await context.SaveChangesAsync();

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector());

        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: user.Id,
            RoleIds: [auditorRole.Id, adminRole.Id],
            ActorUserId: actorId,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal(
            "Portal user roles updated: mete.user (Mete User). Added roles: Admin, Auditor. Removed roles: User.",
            audit.Description);
        Assert.Equal(actorId, audit.ActorUserId);
        Assert.Equal("tester", audit.ActorUserName);
        Assert.Equal("10.20.30.40", audit.IpAddress);
        Assert.Equal("xunit-agent", audit.UserAgent);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenNoRoleChanges_WritesNoRoleChangesAuditDescription()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var user = await SeedPortalUserAsync(context, isActive: true);
        var adminRole = await SeedRoleAsync(context, "Admin", "Admin");

        await context.PortalUserRoles.AddAsync(new PortalUserRole
        {
            PortalUserId = user.Id,
            PortalRoleId = adminRole.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await context.SaveChangesAsync();

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector());

        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: user.Id,
            RoleIds: [adminRole.Id],
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal("Portal user roles updated: mete.user (Mete User). No role changes.", audit.Description);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenRoleIdsNull_ReturnsFailure_AndDoesNotWriteAudit()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var user = await SeedPortalUserAsync(context, isActive: true);
        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector());

        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: user.Id,
            RoleIds: null!,
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.False(result.IsSuccess);
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    private static async Task SeedActiveLdapSettingAsync(AppDbContext context)
    {
        await context.LdapSettings.AddAsync(new LdapSetting
        {
            Name = "Primary LDAP",
            Host = "ldap.test.local",
            BaseDn = "DC=test,DC=local",
            UserSearchBase = "OU=Users,DC=test,DC=local",
            UserSearchFilter = "(sAMAccountName={0})",
            BindUserName = "svc-ldap",
            BindUserDomain = "TEST",
            EncryptedBindPassword = "protected:bind-secret",
            IsActive = true,
            IsDeleted = false
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedNationalIdAttributeAsync(AppDbContext context)
    {
        await context.ApplicationSettings.AddAsync(new ApplicationSetting
        {
            Key = "Directory:NationalIdAttribute",
            Value = "employeeId",
            ValueType = SettingValueType.String,
            IsActive = true,
            IsDeleted = false
        });
        await context.SaveChangesAsync();
    }

    private static async Task<PortalUser> SeedPortalUserAsync(AppDbContext context, bool isActive)
    {
        var user = new PortalUser
        {
            DirectorySource = "ActiveDirectory",
            DirectoryObjectId = Guid.NewGuid().ToString(),
            UserName = "mete.user",
            DisplayName = "Mete User",
            Email = "mete.user@test.local",
            IsActive = isActive,
            IsDeleted = false
        };

        await context.PortalUsers.AddAsync(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<PortalRole> SeedRoleAsync(AppDbContext context, string name, string code)
    {
        var role = new PortalRole
        {
            Name = name,
            Code = code,
            IsSystem = false,
            IsActive = true,
            IsDeleted = false
        };

        await context.PortalRoles.AddAsync(role);
        await context.SaveChangesAsync();
        return role;
    }
}
