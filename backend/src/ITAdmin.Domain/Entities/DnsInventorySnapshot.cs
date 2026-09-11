using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public sealed class DnsInventorySnapshot : BaseEntity
{
    public Guid DnsServerId { get; set; }
    public DnsServer DnsServer { get; set; } = null!;
    public Guid Version { get; set; } = Guid.NewGuid();
    public DnsSyncScope Scope { get; set; }
    public DnsSyncTrigger Trigger { get; set; }
    public DnsSyncStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ZoneCount { get; set; }
    public int RecordCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedByUserName { get; set; }
    public string? CorrelationId { get; set; }

    public ICollection<DnsZoneSnapshot> Zones { get; set; } = new List<DnsZoneSnapshot>();
}
