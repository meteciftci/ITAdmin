using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Options;
using ITAdmin.Application.Notifications;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class NotificationOutboxBatchProcessor(
    AppDbContext context,
    INotificationSender notificationSender,
    IOptions<NotificationOutboxOptions> options,
    ILogger<NotificationOutboxBatchProcessor> logger) : INotificationOutboxBatchProcessor
{
    private static readonly string WorkerIdentity =
        $"{Environment.MachineName}:{Environment.ProcessId}";

    public async Task<int> RecoverStaleProcessingAsync(CancellationToken cancellationToken = default)
    {
        var lockTimeout = DateTimeOffset.UtcNow.AddMinutes(-options.Value.ProcessingLockTimeoutMinutes);
        var staleItems = await context.NotificationOutboxItems
            .Where(x =>
                x.Status == NotificationOutboxStatuses.Processing
                && x.LockedAt != null
                && x.LockedAt < lockTimeout)
            .ToListAsync(cancellationToken);

        if (staleItems.Count == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var item in staleItems)
        {
            item.Status = NotificationOutboxStatuses.Pending;
            item.NextAttemptAt = now;
            item.LockedAt = null;
            item.LockedBy = null;
            item.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Recovered {Count} stale notification outbox items.", staleItems.Count);
        return staleItems.Count;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = options.Value.BatchSize <= 0 ? 20 : options.Value.BatchSize;
        var now = DateTimeOffset.UtcNow;

        var pendingItems = await context.NotificationOutboxItems
            .Where(x =>
                x.Status == NotificationOutboxStatuses.Pending
                && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pendingItems.Count == 0)
        {
            return 0;
        }

        foreach (var item in pendingItems)
        {
            item.Status = NotificationOutboxStatuses.Processing;
            item.LockedAt = now;
            item.LockedBy = WorkerIdentity;
            item.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        var processed = 0;
        foreach (var item in pendingItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessItemAsync(item, cancellationToken);
            processed++;
        }

        return processed;
    }

    private async Task ProcessItemAsync(NotificationOutbox item, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        try
        {
            var sendResult = await SendAsync(item, cancellationToken);
            item.LastAttemptAt = now;
            item.UpdatedAt = now;
            item.LockedAt = null;
            item.LockedBy = null;
            item.ProviderSummary = NotificationOutboxRetryHelper.SanitizeProviderSummary(sendResult.ProviderSummary);

            if (sendResult.IsSuccess)
            {
                item.Status = NotificationOutboxStatuses.Sent;
                item.SentAt = now;
                item.LastErrorMessage = null;
                item.NextAttemptAt = null;
            }
            else
            {
                await ApplyFailureAsync(item, sendResult.Message, now, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Notification outbox item {OutboxId} processing failed.", item.Id);
            await ApplyFailureAsync(item, "Notification delivery failed.", now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyFailureAsync(
        NotificationOutbox item,
        string message,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        item.AttemptCount += 1;
        item.LastErrorMessage = NotificationOutboxRetryHelper.SanitizeErrorMessage(message);
        item.LockedAt = null;
        item.LockedBy = null;
        item.UpdatedAt = now;

        if (item.AttemptCount >= item.MaxAttempts)
        {
            item.Status = NotificationOutboxStatuses.Failed;
            item.NextAttemptAt = null;
            return;
        }

        item.Status = NotificationOutboxStatuses.Pending;
        item.NextAttemptAt = NotificationOutboxRetryHelper.CalculateNextAttemptUtc(item.AttemptCount, now);
        await Task.CompletedTask;
    }

    private async Task<SendOutcome> SendAsync(NotificationOutbox item, CancellationToken cancellationToken)
    {
        if (string.Equals(item.Channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase))
        {
            var result = await notificationSender.SendSmsAsync(
                new SmsSendRequest(item.Recipient, item.Body),
                cancellationToken);
            return new SendOutcome(result.IsSuccess, result.Message, result.ProviderSummary);
        }

        if (string.Equals(item.Channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
        {
            var result = await notificationSender.SendEmailAsync(
                new EmailSendRequest(
                    item.Recipient,
                    item.Subject ?? string.Empty,
                    item.Body),
                cancellationToken);
            return new SendOutcome(result.IsSuccess, result.Message, result.ProviderSummary);
        }

        return new SendOutcome(false, "Notification channel is invalid.", null);
    }

    private sealed record SendOutcome(bool IsSuccess, string Message, string? ProviderSummary);
}
