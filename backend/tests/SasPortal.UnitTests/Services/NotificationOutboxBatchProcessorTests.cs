using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Options;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.Fakes;

namespace SasPortal.UnitTests.Services;

public sealed class NotificationOutboxBatchProcessorTests
{
    [Fact]
    public async Task ProcessBatchAsync_Success_MarksItemSent()
    {
        await using var dbContext = CreateDbContext();
        await SeedPendingSmsAsync(dbContext);
        var processor = CreateProcessor(dbContext, smsSuccess: true);

        var processed = await processor.ProcessBatchAsync();

        Assert.Equal(1, processed);
        var stored = await dbContext.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationOutboxStatuses.Sent, stored.Status);
        Assert.NotNull(stored.SentAt);
    }

    [Fact]
    public async Task ProcessBatchAsync_Failure_SchedulesRetry()
    {
        await using var dbContext = CreateDbContext();
        await SeedPendingSmsAsync(dbContext);
        var processor = CreateProcessor(dbContext, smsSuccess: false);

        await processor.ProcessBatchAsync();

        var stored = await dbContext.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationOutboxStatuses.Pending, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.NotNull(stored.NextAttemptAt);
    }

    [Fact]
    public async Task ProcessBatchAsync_MaxAttempts_MarksFailed()
    {
        await using var dbContext = CreateDbContext();
        var entity = await SeedPendingSmsAsync(dbContext);
        entity.AttemptCount = 2;
        entity.MaxAttempts = 3;
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var processor = CreateProcessor(dbContext, smsSuccess: false);
        await processor.ProcessBatchAsync();

        var stored = await dbContext.NotificationOutboxItems.SingleAsync();
        Assert.Equal(NotificationOutboxStatuses.Failed, stored.Status);
        Assert.Equal(3, stored.AttemptCount);
        Assert.Null(stored.NextAttemptAt);
    }

    private static NotificationOutboxBatchProcessor CreateProcessor(
        AppDbContext dbContext,
        bool smsSuccess) =>
        new(
            dbContext,
            new FakeNotificationSender(smsSuccess),
            Options.Create(new NotificationOutboxOptions { BatchSize = 20 }),
            NullLogger<NotificationOutboxBatchProcessor>.Instance);

    private static async Task<Domain.Entities.NotificationOutbox> SeedPendingSmsAsync(AppDbContext dbContext)
    {
        var entity = new Domain.Entities.NotificationOutbox
        {
            Channel = NotificationChannels.Sms,
            ProviderKey = NotificationProviderKeys.CustomHttp,
            Recipient = "+905551234567",
            RecipientMasked = "+905********67",
            Body = "hello",
            Status = NotificationOutboxStatuses.Pending,
            AttemptCount = 0,
            MaxAttempts = 3,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await dbContext.NotificationOutboxItems.AddAsync(entity);
        await dbContext.SaveChangesAsync();
        return entity;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeNotificationSender(bool smsSuccess) : Application.Abstractions.Services.INotificationSender
    {
        public Task<SmsSendResult> SendSmsAsync(SmsSendRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SmsSendResult(smsSuccess, smsSuccess ? "ok" : "failed", "HTTP 500"));

        public Task<EmailSendResult> SendEmailAsync(EmailSendRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailSendResult(true, "ok"));
    }
}
