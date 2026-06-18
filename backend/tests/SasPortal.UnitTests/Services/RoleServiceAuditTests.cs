using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.Services;

public sealed class RoleServiceAuditTests
{
    [Fact]
    public async Task CreateRoleAsync_WritesReadableAuditDescription()
    {
        await using var context = CreateInMemoryDbContext();
        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);

        var result = await service.CreateRoleAsync(new CreateRoleRequest(
            Name: "Helpdesk",
            Code: "Helpdesk",
            Description: "Helpdesk role",
            IsActive: true,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var role = Assert.Single(context.PortalRoles);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal("Create", audit.Action);
        Assert.Equal("PortalRole", audit.EntityName);
        Assert.Equal(role.Id.ToString(), audit.EntityId);
        Assert.Equal("Portal role created: Helpdesk (Helpdesk). Status: Active. Description provided.", audit.Description);
        Assert.Equal("tester", audit.ActorUserName);
        Assert.Equal("10.20.30.40", audit.IpAddress);
        Assert.Equal("xunit-agent", audit.UserAgent);
        Assert.DoesNotContain("Helpdesk role", audit.Description ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRoleAsync_TruncatesAuditIpAddressAndUserAgent()
    {
        await using var context = CreateInMemoryDbContext();
        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);

        var longIp = $"  {new string('1', 70)}  ";
        var longAgent = $"  {new string('b', 1030)}  ";

        var result = await service.CreateRoleAsync(new CreateRoleRequest(
            Name: "Helpdesk",
            Code: "Helpdesk",
            Description: "Helpdesk role",
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
        Assert.Equal(new string('1', 64), audit.IpAddress);
        Assert.Equal(new string('b', 1024), audit.UserAgent);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenFieldsChange_WritesChangedFieldsAuditDescription()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var role = await SeedRoleAsync(context, "Helpdesk", "Helpdesk", "old", true);
        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);

        var result = await service.UpdateRoleAsync(new UpdateRoleRequest(
            RoleId: role.Id,
            Name: "Helpdesk Admin",
            Description: "new",
            IsActive: false,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal(
            "Portal role updated: Helpdesk Admin (Helpdesk). Name: \"Helpdesk\" -> \"Helpdesk Admin\". Description changed. Status: Active -> Passive.",
            audit.Description);
        Assert.DoesNotContain("old", audit.Description ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("new", audit.Description ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("tester", audit.ActorUserName);
        Assert.Equal("10.20.30.40", audit.IpAddress);
        Assert.Equal("xunit-agent", audit.UserAgent);
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenNoFieldChanges_WritesNoFieldChangesAuditDescription()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var role = await SeedRoleAsync(context, "Helpdesk", "Helpdesk", "same", true);
        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);

        var result = await service.UpdateRoleAsync(new UpdateRoleRequest(
            RoleId: role.Id,
            Name: "Helpdesk",
            Description: "same",
            IsActive: true,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal("Portal role updated: Helpdesk (Helpdesk). No field changes.", audit.Description);
    }

    [Fact]
    public async Task UpdateRoleStatusAsync_WhenDeactivated_WritesStatusAuditDescription()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var role = await SeedRoleAsync(context, "Helpdesk", "Helpdesk", null, true);
        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);

        var result = await service.UpdateRoleStatusAsync(new UpdateRoleStatusRequest(
            RoleId: role.Id,
            IsActive: false,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal("Portal role deactivated: Helpdesk (Helpdesk). Status: Active -> Passive.", audit.Description);
    }

    [Fact]
    public async Task UpdateRoleStatusAsync_WhenActivated_WritesStatusAuditDescription()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var role = await SeedRoleAsync(context, "Helpdesk", "Helpdesk", null, false);
        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);

        var result = await service.UpdateRoleStatusAsync(new UpdateRoleStatusRequest(
            RoleId: role.Id,
            IsActive: true,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal("Portal role activated: Helpdesk (Helpdesk). Status: Passive -> Active.", audit.Description);
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenPermissionsAddedAndRemoved_WritesPermissionDiffAuditDescription()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var role = await SeedRoleAsync(context, "Helpdesk", "Helpdesk", null, true);
        var usersView = await SeedPermissionAsync(context, "Users", "Users.View");
        var auditLogsView = await SeedPermissionAsync(context, "AuditLogs", "AuditLogs.View");
        var rolesView = await SeedPermissionAsync(context, "Roles", "Roles.View");

        await context.PortalRolePermissions.AddAsync(new PortalRolePermission
        {
            PortalRoleId = role.Id,
            PortalPermissionId = usersView.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await context.SaveChangesAsync();

        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);
        var result = await service.UpdateRolePermissionsAsync(new UpdateRolePermissionsRequest(
            RoleId: role.Id,
            PermissionIds: [rolesView.Id, auditLogsView.Id],
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal(
            "Portal role permissions updated: Helpdesk (Helpdesk). Added permissions: AuditLogs.View, Roles.View. Removed permissions: Users.View.",
            audit.Description);
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenPermissionsCleared_WritesRemovedPermissionsAuditDescription()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var role = await SeedRoleAsync(context, "Helpdesk", "Helpdesk", null, true);
        var usersView = await SeedPermissionAsync(context, "Users", "Users.View");
        var rolesView = await SeedPermissionAsync(context, "Roles", "Roles.View");

        await context.PortalRolePermissions.AddRangeAsync(
            new PortalRolePermission
            {
                PortalRoleId = role.Id,
                PortalPermissionId = usersView.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            },
            new PortalRolePermission
            {
                PortalRoleId = role.Id,
                PortalPermissionId = rolesView.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });
        await context.SaveChangesAsync();

        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);
        var result = await service.UpdateRolePermissionsAsync(new UpdateRolePermissionsRequest(
            RoleId: role.Id,
            PermissionIds: [],
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal(
            "Portal role permissions updated: Helpdesk (Helpdesk). Removed permissions: Roles.View, Users.View.",
            audit.Description);
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenNoPermissionChanges_WritesNoPermissionChangesAuditDescription()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var role = await SeedRoleAsync(context, "Helpdesk", "Helpdesk", null, true);
        var usersView = await SeedPermissionAsync(context, "Users", "Users.View");

        await context.PortalRolePermissions.AddAsync(new PortalRolePermission
        {
            PortalRoleId = role.Id,
            PortalPermissionId = usersView.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await context.SaveChangesAsync();

        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);
        var result = await service.UpdateRolePermissionsAsync(new UpdateRolePermissionsRequest(
            RoleId: role.Id,
            PermissionIds: [usersView.Id],
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal("Portal role permissions updated: Helpdesk (Helpdesk). No permission changes.", audit.Description);
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_WhenPermissionIdsNull_ReturnsFailure_AndDoesNotWriteAudit()
    {
        var (connection, dbContext) = await SqliteTestDbContextFactory.CreateAsync();
        using var _ = connection;
        await using var context = dbContext;

        var role = await SeedRoleAsync(context, "Helpdesk", "Helpdesk", null, true);
        var service = new RoleService(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance);

        var result = await service.UpdateRolePermissionsAsync(new UpdateRolePermissionsRequest(
            RoleId: role.Id,
            PermissionIds: null!,
            ActorUserName: "tester",
            ActorIpAddress: "10.20.30.40",
            ActorUserAgent: "xunit-agent"));

        Assert.False(result.IsSuccess);
        Assert.Empty(await context.AuditLogs.ToListAsync());
    }

    private static async Task<PortalRole> SeedRoleAsync(
        SasPortal.Persistence.Context.AppDbContext context,
        string name,
        string code,
        string? description,
        bool isActive)
    {
        var role = new PortalRole
        {
            Name = name,
            Code = code,
            Description = description,
            IsSystem = false,
            IsActive = isActive,
            IsDeleted = false
        };

        await context.PortalRoles.AddAsync(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static async Task<PortalPermission> SeedPermissionAsync(
        SasPortal.Persistence.Context.AppDbContext context,
        string module,
        string code)
    {
        var permission = new PortalPermission
        {
            Module = module,
            Code = code,
            IsActive = true,
            IsDeleted = false
        };

        await context.PortalPermissions.AddAsync(permission);
        await context.SaveChangesAsync();
        return permission;
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}
