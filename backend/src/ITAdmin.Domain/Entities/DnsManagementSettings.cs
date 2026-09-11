using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public sealed class DnsManagementSettings : AuditableEntity
{
    public bool IsEnabled { get; set; }
    public bool AutomaticSyncEnabled { get; set; } = true;
    public int DefaultSyncIntervalMinutes { get; set; } = 15;
    public int HealthCheckIntervalMinutes { get; set; } = 5;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int MaxParallelServers { get; set; } = 3;
    public int SnapshotRetentionDays { get; set; } = 30;
    public bool SyncRecordInventory { get; set; } = true;
    public bool PromptForFullSyncOnComparisonOpen { get; set; } = true;
    public int ComparisonSnapshotStaleAfterMinutes { get; set; } = 15;
}
