using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public sealed class DnsRecordSnapshot : BaseEntity
{
    public Guid DnsZoneSnapshotId { get; set; }
    public DnsZoneSnapshot ZoneSnapshot { get; set; } = null!;
    public string RelativeName { get; set; } = string.Empty;
    public string FullyQualifiedName { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;
    public string CanonicalValue { get; set; } = string.Empty;
    public string RecordDataJson { get; set; } = string.Empty;
    public int TimeToLiveSeconds { get; set; }
    public DateTime? Timestamp { get; set; }
    public string? ZoneScope { get; set; }
    public string? VirtualizationInstance { get; set; }
    public string RecordHash { get; set; } = string.Empty;
}
