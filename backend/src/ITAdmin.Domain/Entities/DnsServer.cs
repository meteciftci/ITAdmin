using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public sealed class DnsServer : AuditableEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 5986;
    public DnsConnectionTransport Transport { get; set; } = DnsConnectionTransport.Https;
    public DnsServerEnvironment Environment { get; set; }
    public Guid DnsCredentialProfileId { get; set; }
    public DnsCredentialProfile CredentialProfile { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public int? SyncIntervalMinutes { get; set; }
    public string? TlsCertificateThumbprint { get; set; }
    public string? Notes { get; set; }
    public string? OperatingSystemVersion { get; set; }
    public string? DnsServerVersion { get; set; }
    public string? CapabilitiesJson { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastSuccessfulSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastSyncMessage { get; set; }

    public ICollection<DnsInventorySnapshot> InventorySnapshots { get; set; } = new List<DnsInventorySnapshot>();
    public ICollection<DnsSyncJob> SyncJobs { get; set; } = new List<DnsSyncJob>();
}
