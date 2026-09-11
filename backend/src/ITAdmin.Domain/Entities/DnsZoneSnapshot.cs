using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public sealed class DnsZoneSnapshot : BaseEntity
{
    public Guid DnsInventorySnapshotId { get; set; }
    public DnsInventorySnapshot InventorySnapshot { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string ZoneType { get; set; } = string.Empty;
    public bool IsReverseLookupZone { get; set; }
    public bool IsDsIntegrated { get; set; }
    public bool IsSigned { get; set; }
    public bool IsPaused { get; set; }
    public string? DynamicUpdate { get; set; }
    public string? ReplicationScope { get; set; }
    public string? DirectoryPartitionName { get; set; }
    public string? ZoneFile { get; set; }
    public string? VirtualizationInstance { get; set; }
    public string? PropertiesJson { get; set; }

    public ICollection<DnsRecordSnapshot> Records { get; set; } = new List<DnsRecordSnapshot>();
}
