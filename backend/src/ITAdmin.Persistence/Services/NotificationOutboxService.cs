using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Audit;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.Notifications;
using ITAdmin.Application.Common.Notifications;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class NotificationOutboxService(AppDbContext context) : INotificationOutboxService
{
    private const int AuditIpAddressMaxLength = 64;
    private const int AuditUserAgentMaxLength = 1024;
    private const int BodyPreviewMaxLength = 2000;
    private const int DefaultMaxAttempts = 3;

    public async Task<NotificationOutboxEnqueueResult> EnqueueAsync(
        NotificationOutboxEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidChannel(request.Channel))
        {
            return new NotificationOutboxEnqueueResult(false, "Notification channel is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Recipient))
        {
            return new NotificationOutboxEnqueueResult(false, "Recipient is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return new NotificationOutboxEnqueueResult(false, "Message body is required.");
        }

        var channel = request.Channel.Trim();
        var providerKey = ResolveProviderKey(channel, request.ProviderKey);
        var now = DateTimeOffset.UtcNow;
        var maxAttempts = request.MaxAttempts is > 0 and <= 10 ? request.MaxAttempts.Value : DefaultMaxAttempts;

        var entity = new NotificationOutbox
        {
            Channel = channel,
            ProviderKey = providerKey,
            Recipient = request.Recipient.Trim(),
            RecipientMasked = MaskRecipient(channel, request.Recipient),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            Body = request.Body.Trim(),
            Status = NotificationOutboxStatuses.Pending,
            Priority = request.Priority,
            AttemptCount = 0,
            MaxAttempts = maxAttempts,
            NextAttemptAt = now,
            RelatedModule = TrimOrNull(request.RelatedModule),
            RelatedEvent = TrimOrNull(request.RelatedEvent),
            RelatedEntityType = TrimOrNull(request.RelatedEntityType),
            RelatedEntityId = TrimOrNull(request.RelatedEntityId),
            CorrelationId = TrimOrNull(request.CorrelationId),
            CreatedAt = now,
            CreatedBy = TrimOrNull(request.CreatedBy),
        };

        await context.NotificationOutboxItems.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new NotificationOutboxEnqueueResult(true, "Notification queued.", entity.Id);
    }

    public async Task<PagedResult<NotificationOutboxListItem>> GetListAsync(
        NotificationOutboxListQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize,
        };

        var itemsQuery = context.NotificationOutboxItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            itemsQuery = itemsQuery.Where(x => x.Channel == query.Channel.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            itemsQuery = itemsQuery.Where(x => x.Status == query.Status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.RelatedModule))
        {
            itemsQuery = itemsQuery.Where(x => x.RelatedModule == query.RelatedModule.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.RelatedEvent))
        {
            itemsQuery = itemsQuery.Where(x => x.RelatedEvent == query.RelatedEvent.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = $"%{query.Search.Trim()}%";
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.RecipientMasked, search)
                || (x.Subject != null && EF.Functions.ILike(x.Subject, search))
                || (x.RelatedModule != null && EF.Functions.ILike(x.RelatedModule, search))
                || (x.RelatedEvent != null && EF.Functions.ILike(x.RelatedEvent, search))
                || (x.LastErrorMessage != null && EF.Functions.ILike(x.LastErrorMessage, search)));
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await itemsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationOutboxListItem(
                x.Id,
                x.Channel,
                x.ProviderKey,
                x.RecipientMasked,
                x.Subject,
                x.Status,
                x.AttemptCount,
                x.MaxAttempts,
                x.NextAttemptAt,
                x.LastAttemptAt,
                x.SentAt,
                x.RelatedModule,
                x.RelatedEvent,
                x.RelatedEntityType,
                x.RelatedEntityId,
                x.CreatedAt,
                x.ProviderSummary,
                x.LastErrorMessage))
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationOutboxListItem>(
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<NotificationOutboxDetail?> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.NotificationOutboxItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapDetail(entity);
    }

    public async Task<NotificationOutboxOperationResult> RetryAsync(
        Guid id,
        NotificationOutboxActorRequest actor,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.NotificationOutboxItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return new NotificationOutboxOperationResult(false, "Notification outbox item was not found.");
        }

        if (!string.Equals(entity.Status, NotificationOutboxStatuses.Failed, StringComparison.Ordinal))
        {
            return new NotificationOutboxOperationResult(false, "Only failed notifications can be retried.");
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = NotificationOutboxStatuses.Pending;
        entity.AttemptCount = 0;
        entity.NextAttemptAt = now;
        entity.LastErrorMessage = null;
        entity.ProviderSummary = null;
        entity.LockedAt = null;
        entity.LockedBy = null;
        entity.UpdatedAt = now;
        entity.UpdatedBy = actor.ActorUserName;

        await WriteAuditAsync(
            "Retry",
            entity,
            $"Notification outbox item retried. Channel: {entity.Channel}. Recipient: {entity.RecipientMasked}.{FormatRelated(entity)}",
            actor,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return new NotificationOutboxOperationResult(
            true,
            "Notification queued for retry.",
            MapDetail(entity));
    }

    public async Task<NotificationOutboxOperationResult> CancelAsync(
        Guid id,
        NotificationOutboxActorRequest actor,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.NotificationOutboxItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return new NotificationOutboxOperationResult(false, "Notification outbox item was not found.");
        }

        if (entity.Status is not NotificationOutboxStatuses.Pending and not NotificationOutboxStatuses.Failed)
        {
            return new NotificationOutboxOperationResult(false, "Only pending or failed notifications can be cancelled.");
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = NotificationOutboxStatuses.Cancelled;
        entity.NextAttemptAt = null;
        entity.LockedAt = null;
        entity.LockedBy = null;
        entity.UpdatedAt = now;
        entity.UpdatedBy = actor.ActorUserName;

        await WriteAuditAsync(
            "Cancel",
            entity,
            $"Notification outbox item cancelled. Channel: {entity.Channel}. Recipient: {entity.RecipientMasked}.{FormatRelated(entity)}",
            actor,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return new NotificationOutboxOperationResult(
            true,
            "Notification cancelled.",
            MapDetail(entity));
    }

    private static string ResolveProviderKey(string channel, string? providerKey)
    {
        if (!string.IsNullOrWhiteSpace(providerKey))
        {
            return providerKey.Trim();
        }

        return string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase)
            ? NotificationProviderKeys.Smtp
            : NotificationProviderKeys.CustomHttp;
    }

    private static string MaskRecipient(string channel, string recipient) =>
        string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase)
            ? NotificationRecipientMasker.MaskEmail(recipient)
            : NotificationRecipientMasker.MaskPhone(recipient);

    private static bool IsValidChannel(string channel) =>
        string.Equals(channel, NotificationChannels.Sms, StringComparison.OrdinalIgnoreCase)
        || string.Equals(channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase);

    private static NotificationOutboxDetail MapDetail(NotificationOutbox entity) =>
        new(
            entity.Id,
            entity.Channel,
            entity.ProviderKey,
            entity.RecipientMasked,
            entity.Subject,
            TruncateBody(entity.Body),
            entity.Status,
            entity.AttemptCount,
            entity.MaxAttempts,
            entity.NextAttemptAt,
            entity.LastAttemptAt,
            entity.SentAt,
            entity.LockedAt,
            entity.LockedBy,
            entity.LastErrorMessage,
            entity.ProviderSummary,
            entity.RelatedModule,
            entity.RelatedEvent,
            entity.RelatedEntityType,
            entity.RelatedEntityId,
            entity.CorrelationId,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);

    private static string TruncateBody(string body) =>
        body.Length <= BodyPreviewMaxLength ? body : $"{body[..BodyPreviewMaxLength]}...";

    private static string FormatRelated(NotificationOutbox entity)
    {
        if (string.IsNullOrWhiteSpace(entity.RelatedModule) && string.IsNullOrWhiteSpace(entity.RelatedEvent))
        {
            return string.Empty;
        }

        return $" Related: {entity.RelatedModule}/{entity.RelatedEvent}.";
    }

    private async Task WriteAuditAsync(
        string action,
        NotificationOutbox entity,
        string description,
        NotificationOutboxActorRequest actor,
        CancellationToken cancellationToken)
    {
        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = action,
                EntityName = "NotificationOutbox",
                EntityId = entity.Id.ToString(),
                Description = TruncateDescription(description),
                ActorUserId = actor.ActorUserId,
                ActorUserName = actor.ActorUserName,
                IpAddress = TruncateNullable(actor.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(actor.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private static string TruncateDescription(string description) =>
        description.Length <= AuditChangeSummaryBuilder.DefaultMaxLength
            ? description
            : $"{description[..(AuditChangeSummaryBuilder.DefaultMaxLength - 3)]}...";

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
