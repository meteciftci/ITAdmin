using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models.Notifications;
using SasPortal.Application.Notifications;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.Services;

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

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
