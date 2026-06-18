using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.Notifications;
using ITAdmin.Application.Notifications;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.Services;

public sealed class NotificationTemplateServiceTests
{
    private static readonly StaticNotificationTemplateCatalogProvider CatalogProvider = new();

    [Fact]
    public async Task CreateAsync_CatalogOutsideModuleEvent_ReturnsFailure()
    {
        await using var dbContext = CreateDbContext();
        var service = new NotificationTemplateService(dbContext, CatalogProvider);

        var result = await service.CreateAsync(
            new CreateNotificationTemplateRequest(
                "Unknown",
                "UserCreated",
                NotificationChannels.Sms,
                "Test",
                true,
                null,
                "Body",
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("catalog", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_UnsupportedChannel_ReturnsFailure()
    {
        await using var dbContext = CreateDbContext();
        var service = new NotificationTemplateService(dbContext, CatalogProvider);

        var result = await service.CreateAsync(
            new CreateNotificationTemplateRequest(
                "System",
                "GenericNotification",
                "InvalidChannel",
                "Test",
                true,
                null,
                "Body",
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_ValidCatalogEvent_Succeeds()
    {
        await using var dbContext = CreateDbContext();
        var service = new NotificationTemplateService(dbContext, CatalogProvider);

        var result = await service.CreateAsync(
            new CreateNotificationTemplateRequest(
                "System",
                "GenericNotification",
                NotificationChannels.Sms,
                "Welcome SMS",
                true,
                null,
                "Hello {{message}}",
                null,
                null,
                "tester",
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Template);
        Assert.Equal("System", result.Template.ModuleKey);
        Assert.Equal("GenericNotification", result.Template.EventKey);
    }

    [Fact]
    public async Task UpdateStatusAsync_TogglesIsEnabled_WritesAuditSafeDiff()
    {
        await using var dbContext = CreateDbContext();
        var service = new NotificationTemplateService(dbContext, CatalogProvider);

        var createResult = await service.CreateAsync(
            new CreateNotificationTemplateRequest(
                "AdManagement",
                "UserCreated",
                NotificationChannels.Sms,
                "User created SMS",
                true,
                null,
                "Hello {{displayName}}",
                null,
                null,
                "tester",
                null,
                null),
            CancellationToken.None);

        Assert.True(createResult.IsSuccess);
        Assert.NotNull(createResult.Template);

        var updateResult = await service.UpdateStatusAsync(
            createResult.Template.Id,
            new UpdateNotificationTemplateStatusRequest(false, null, "tester", null, null),
            CancellationToken.None);

        Assert.True(updateResult.IsSuccess);
        Assert.NotNull(updateResult.Template);
        Assert.False(updateResult.Template.IsEnabled);

        var audit = await dbContext.AuditLogs
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync();
        Assert.Contains("IsEnabled", audit.Description, StringComparison.Ordinal);
        Assert.Contains("false", audit.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
