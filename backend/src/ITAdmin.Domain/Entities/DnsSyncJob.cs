using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public sealed class DnsSyncJob : BaseEntity
{
    public Guid BatchId { get; set; }
    public Guid DnsServerId { get; set; }
    public DnsServer DnsServer { get; set; } = null!;
    public DnsSyncScope Scope { get; set; }
    public DnsSyncTrigger Trigger { get; set; }
    public DnsSyncStatus Status { get; set; } = DnsSyncStatus.Pending;
    public string? ZoneName { get; set; }
    public string DedupeKey { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int AttemptCount { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? LeaseOwner { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedByUserName { get; set; }
    public string? CorrelationId { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}
