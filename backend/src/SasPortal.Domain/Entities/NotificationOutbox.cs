using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public sealed class NotificationOutbox : BaseEntity
{
    public string Channel { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string RecipientMasked { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public string? LockedBy { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? ProviderSummary { get; set; }
    public string? RelatedModule { get; set; }
    public string? RelatedEvent { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
