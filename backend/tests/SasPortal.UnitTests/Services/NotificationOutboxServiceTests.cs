using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models.Notifications;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.Services;

public sealed class NotificationOutboxServiceTests
{
    [Fact]
    public async Task EnqueueAsync_CreatesPendingItem_WithMaskedRecipient()
    {
        await using var dbContext = CreateDbContext();
        var service = new NotificationOutboxService(dbContext);

        var result = await service.EnqueueAsync(
            new NotificationOutboxEnqueueRequest(
                NotificationChannels.Sms,
                null,
                "+905551234567",
                null,
                "Test body",
                "System",
                "GenericTest",
                null,
                null,
                null,
                0,
                null,
                "tester"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutboxId);

        var stored = await dbContext.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationOutboxStatuses.Pending, stored.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Equal(3, stored.MaxAttempts);
        Assert.Contains('*', stored.RecipientMasked);
        Assert.DoesNotContain("551234567", stored.RecipientMasked);
    }

    [Fact]
    public async Task RetryAsync_FailedItem_ResetsToPending()
    {
        await using var dbContext = CreateDbContext();
        var service = new NotificationOutboxService(dbContext);
        var id = await SeedFailedItemAsync(dbContext);

        var result = await service.RetryAsync(
            id,
            new NotificationOutboxActorRequest(Guid.NewGuid(), "tester", "127.0.0.1", "xunit"));

        Assert.True(result.IsSuccess);
        var stored = await dbContext.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationOutboxStatuses.Pending, stored.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Null(stored.LastErrorMessage);
    }

    [Fact]
    public async Task CancelAsync_PendingItem_SetsCancelled()
    {
        await using var dbContext = CreateDbContext();
        var service = new NotificationOutboxService(dbContext);

        var enqueue = await service.EnqueueAsync(
            new NotificationOutboxEnqueueRequest(
                NotificationChannels.Email,
                null,
                "user@example.com",
                "Subject",
                "Body",
                null,
                null,
                null,
                null,
                null,
                0,
                null,
                "tester"));
        dbContext.ChangeTracker.Clear();

        var result = await service.CancelAsync(
            enqueue.OutboxId!.Value,
            new NotificationOutboxActorRequest(Guid.NewGuid(), "tester", "127.0.0.1", "xunit"));

        Assert.True(result.IsSuccess);
        var stored = await dbContext.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationOutboxStatuses.Cancelled, stored.Status);
    }

    [Fact]
    public async Task RetryAsync_SentItem_ReturnsFailure()
    {
        await using var dbContext = CreateDbContext();
        var service = new NotificationOutboxService(dbContext);
        var entity = new Domain.Entities.NotificationOutbox
        {
            Channel = NotificationChannels.Sms,
            ProviderKey = NotificationProviderKeys.CustomHttp,
            Recipient = "+905551234567",
            RecipientMasked = "+905********67",
            Body = "body",
            Status = NotificationOutboxStatuses.Sent,
            MaxAttempts = 3,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await dbContext.NotificationOutboxItems.AddAsync(entity);
        await dbContext.SaveChangesAsync();

        var result = await service.RetryAsync(
            entity.Id,
            new NotificationOutboxActorRequest(null, "tester", null, null));

        Assert.False(result.IsSuccess);
    }

    private static async Task<Guid> SeedFailedItemAsync(AppDbContext dbContext)
    {
        var entity = new Domain.Entities.NotificationOutbox
        {
            Channel = NotificationChannels.Sms,
            ProviderKey = NotificationProviderKeys.CustomHttp,
            Recipient = "+905551234567",
            RecipientMasked = "+905********67",
            Body = "failed",
            Status = NotificationOutboxStatuses.Failed,
            AttemptCount = 3,
            MaxAttempts = 3,
            LastErrorMessage = "HTTP 500",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await dbContext.NotificationOutboxItems.AddAsync(entity);
        await dbContext.SaveChangesAsync();
        return entity.Id;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}
