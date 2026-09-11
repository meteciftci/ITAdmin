using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public sealed class DnsOperationLog : BaseEntity
{
    public Guid? DnsServerId { get; set; }
    public DnsServer? DnsServer { get; set; }
    public string? ServerDisplayName { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ZoneName { get; set; }
    public string? RecordName { get; set; }
    public string? RecordType { get; set; }
    public string? RequestSummaryJson { get; set; }
    public string? BeforeSnapshotJson { get; set; }
    public string? AfterSnapshotJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorUserName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
