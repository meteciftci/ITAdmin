namespace SasPortal.Application.Common.Models.Notifications;

public sealed record NotificationOutboxEnqueueRequest(
    string Channel,
    string? ProviderKey,
    string Recipient,
    string? Subject,
    string Body,
    string? RelatedModule,
    string? RelatedEvent,
    string? RelatedEntityType,
    string? RelatedEntityId,
    string? CorrelationId,
    int Priority,
    int? MaxAttempts,
    string? CreatedBy);

public sealed record NotificationOutboxEnqueueResult(
    bool IsSuccess,
    string Message,
    Guid? OutboxId = null);

public sealed record NotificationOutboxListQuery(
    string? Channel,
    string? Status,
    string? RelatedModule,
    string? RelatedEvent,
    string? Search,
    int PageNumber,
    int PageSize);

public sealed record NotificationOutboxListItem(
    Guid Id,
    string Channel,
    string ProviderKey,
    string RecipientMasked,
    string? Subject,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? SentAt,
    string? RelatedModule,
    string? RelatedEvent,
    string? RelatedEntityType,
    string? RelatedEntityId,
    DateTimeOffset CreatedAt,
    string? ProviderSummary,
    string? LastErrorMessage);

public sealed record NotificationOutboxDetail(
    Guid Id,
    string Channel,
    string ProviderKey,
    string RecipientMasked,
    string? Subject,
    string Body,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? LockedAt,
    string? LockedBy,
    string? LastErrorMessage,
    string? ProviderSummary,
    string? RelatedModule,
    string? RelatedEvent,
    string? RelatedEntityType,
    string? RelatedEntityId,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record NotificationOutboxActorRequest(
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);

public sealed record NotificationOutboxOperationResult(
    bool IsSuccess,
    string Message,
    NotificationOutboxDetail? Item = null);
