using ITAdmin.Application.Common.Models;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.UnitTests.Services;

public sealed class PermissionServiceTests
{
    [Fact]
    public async Task GetPermissionsAsync_ReturnsAuthoritativeModuleAndOrdersByModuleThenCode()
    {
        await using var context = CreateDbContext();
        context.PortalPermissions.AddRange(
            Permission("Users", "Users.View"),
            Permission("AuditLogs", "AuditLogs.View"),
            Permission("Users", "Users.Create"));
        await context.SaveChangesAsync();

        var service = new PermissionService(context);
        var result = await service.GetPermissionsAsync(
            new PermissionListQuery(null, true, 1, 20));

        Assert.Equal(
            ["AuditLogs:AuditLogs.View", "Users:Users.Create", "Users:Users.View"],
            result.Items.Select(item => $"{item.Module}:{item.Code}"));
    }

    [Fact]
    public async Task GetPermissionByIdAsync_ReturnsAuthoritativeModule()
    {
        await using var context = CreateDbContext();
        var permission = Permission("NotificationOutbox", "NotificationOutbox.Retry");
        context.PortalPermissions.Add(permission);
        await context.SaveChangesAsync();

        var service = new PermissionService(context);
        var result = await service.GetPermissionByIdAsync(permission.Id);

        Assert.NotNull(result);
        Assert.Equal("NotificationOutbox", result.Module);
    }

    private static PortalPermission Permission(string module, string code) =>
        new()
        {
            Module = module,
            Code = code,
            Description = $"Description for {code}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}
