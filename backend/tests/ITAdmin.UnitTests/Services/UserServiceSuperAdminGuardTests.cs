using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.Fakes;
using ITAdmin.UnitTests.TestInfrastructure;

namespace ITAdmin.UnitTests.Services;

public sealed class UserServiceSuperAdminGuardTests
{
    [Fact]
    public async Task UpdateUserRolesAsync_WhenOnlyActiveSuperAdmin_RemovingSuperAdminRoleFails()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var superAdminRole = await SeedRoleAsync(context, "Super Admin", SystemRoles.SuperAdmin);
        var user = await SeedPortalUserAsync(context, userName: "only.super", isActive: true);
        await AssignRoleAsync(context, user.Id, superAdminRole.Id);

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector(), Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance);
        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: user.Id,
            RoleIds: [],
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "The last active SuperAdmin user cannot lose the SuperAdmin role.",
            result.Message);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenTwoActiveSuperAdmins_RemovingSuperAdminFromOneSucceeds()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var superAdminRole = await SeedRoleAsync(context, "Super Admin", SystemRoles.SuperAdmin);
        var firstSuperAdmin = await SeedPortalUserAsync(context, userName: "super.one", isActive: true);
        var secondSuperAdmin = await SeedPortalUserAsync(context, userName: "super.two", isActive: true);
        await AssignRoleAsync(context, firstSuperAdmin.Id, superAdminRole.Id);
        await AssignRoleAsync(context, secondSuperAdmin.Id, superAdminRole.Id);

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector(), Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance);
        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: firstSuperAdmin.Id,
            RoleIds: [],
            ActorUserId: secondSuperAdmin.Id,
            ActorUserName: "super.two",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.User);
        Assert.DoesNotContain(SystemRoles.SuperAdmin, result.User.Roles);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenActorRemovesOwnSuperAdminAndAnotherActiveSuperAdminExists_Succeeds()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var superAdminRole = await SeedRoleAsync(context, "Super Admin", SystemRoles.SuperAdmin);
        var actor = await SeedPortalUserAsync(context, userName: "actor.super", isActive: true);
        var otherSuperAdmin = await SeedPortalUserAsync(context, userName: "other.super", isActive: true);
        await AssignRoleAsync(context, actor.Id, superAdminRole.Id);
        await AssignRoleAsync(context, otherSuperAdmin.Id, superAdminRole.Id);

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector(), Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance);
        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: actor.Id,
            RoleIds: [],
            ActorUserId: actor.Id,
            ActorUserName: actor.UserName,
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.User);
        Assert.DoesNotContain(SystemRoles.SuperAdmin, result.User.Roles);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_WhenActorRemovesOwnSuperAdminAsLastActiveSuperAdmin_Fails()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var superAdminRole = await SeedRoleAsync(context, "Super Admin", SystemRoles.SuperAdmin);
        var actor = await SeedPortalUserAsync(context, userName: "last.super", isActive: true);
        await AssignRoleAsync(context, actor.Id, superAdminRole.Id);

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector(), Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance);
        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: actor.Id,
            RoleIds: [],
            ActorUserId: actor.Id,
            ActorUserName: actor.UserName,
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "The last active SuperAdmin user cannot lose the SuperAdmin role.",
            result.Message);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_InactiveSuperAdminUser_DoesNotCountTowardAnotherActiveSuperAdmin()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var superAdminRole = await SeedRoleAsync(context, "Super Admin", SystemRoles.SuperAdmin);
        var activeSuperAdmin = await SeedPortalUserAsync(context, userName: "active.super", isActive: true);
        var inactiveSuperAdmin = await SeedPortalUserAsync(context, userName: "inactive.super", isActive: false);
        await AssignRoleAsync(context, activeSuperAdmin.Id, superAdminRole.Id);
        await AssignRoleAsync(context, inactiveSuperAdmin.Id, superAdminRole.Id);

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector(), Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance);
        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: activeSuperAdmin.Id,
            RoleIds: [],
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "The last active SuperAdmin user cannot lose the SuperAdmin role.",
            result.Message);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_DeletedSuperAdminUser_DoesNotCountTowardAnotherActiveSuperAdmin()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var superAdminRole = await SeedRoleAsync(context, "Super Admin", SystemRoles.SuperAdmin);
        var activeSuperAdmin = await SeedPortalUserAsync(context, userName: "active.super", isActive: true);
        var deletedSuperAdmin = await SeedPortalUserAsync(
            context,
            userName: "deleted.super",
            isActive: true,
            isDeleted: true);
        await AssignRoleAsync(context, activeSuperAdmin.Id, superAdminRole.Id);
        await AssignRoleAsync(context, deletedSuperAdmin.Id, superAdminRole.Id);

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector(), Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance);
        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: activeSuperAdmin.Id,
            RoleIds: [],
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "The last active SuperAdmin user cannot lose the SuperAdmin role.",
            result.Message);
    }

    [Fact]
    public async Task UpdateUserRolesAsync_InactiveSuperAdminUser_CanLoseSuperAdminRole()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var superAdminRole = await SeedRoleAsync(context, "Super Admin", SystemRoles.SuperAdmin);
        var inactiveSuperAdmin = await SeedPortalUserAsync(context, userName: "inactive.super", isActive: false);
        await AssignRoleAsync(context, inactiveSuperAdmin.Id, superAdminRole.Id);

        var service = new UserService(context, new FakeLdapService(), new FakeSecretProtector(), Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance);
        var result = await service.UpdateUserRolesAsync(new UpdateUserRolesRequest(
            UserId: inactiveSuperAdmin.Id,
            RoleIds: [],
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.DoesNotContain(SystemRoles.SuperAdmin, result.User.Roles);
    }

    private static async Task<PortalRole> SeedRoleAsync(AppDbContext context, string name, string code)
    {
        var role = new PortalRole
        {
            Name = name,
            Code = code,
            IsSystem = string.Equals(code, SystemRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase),
            IsActive = true,
            IsDeleted = false
        };

        await context.PortalRoles.AddAsync(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static async Task<PortalUser> SeedPortalUserAsync(
        AppDbContext context,
        string userName,
        bool isActive,
        bool isDeleted = false)
    {
        var user = new PortalUser
        {
            DirectorySource = "ActiveDirectory",
            DirectoryObjectId = Guid.NewGuid().ToString(),
            UserName = userName,
            DisplayName = userName,
            Email = $"{userName}@test.local",
            IsActive = isActive,
            IsDeleted = isDeleted
        };

        await context.PortalUsers.AddAsync(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task AssignRoleAsync(AppDbContext context, Guid userId, Guid roleId)
    {
        await context.PortalUserRoles.AddAsync(new PortalUserRole
        {
            PortalUserId = userId,
            PortalRoleId = roleId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await context.SaveChangesAsync();
    }
}
