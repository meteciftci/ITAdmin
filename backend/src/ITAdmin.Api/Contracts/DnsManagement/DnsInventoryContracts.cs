using ITAdmin.Domain.Enums;

namespace ITAdmin.Api.Contracts.DnsManagement;

public sealed record DnsInventoryServerResponse(
    Guid ServerId, string ServerDisplayName, DnsServerEnvironment Environment, bool IsEnabled,
    Guid? SnapshotId, Guid? SnapshotVersion, DnsSyncScope? SnapshotScope,
    DateTime? SnapshotCompletedAt, int ZoneCount, int RecordCount,
    string? LastSyncStatus, string? LastSyncMessage, bool IsStale, bool IsAvailable);

public sealed record DnsZoneInventoryResponse(
    Guid Id, Guid SnapshotId, Guid ServerId, string ServerDisplayName,
    DnsServerEnvironment Environment, string Name, string ZoneType,
    bool IsReverseLookupZone, bool IsDsIntegrated, bool IsSigned, bool IsPaused,
    string? DynamicUpdate, string? ReplicationScope, string? DirectoryPartitionName,
    string? ZoneFile, string? VirtualizationInstance, IReadOnlyList<string> ZoneScopes,
    int RecordCount, DateTime SnapshotCompletedAt);

public sealed record DnsRecordInventoryResponse(
    Guid Id, string RelativeName, string FullyQualifiedName, string RecordType,
    string CanonicalValue, string RecordDataJson, int TimeToLiveSeconds,
    DateTime? Timestamp, string? ZoneScope, string? VirtualizationInstance,
    string RecordHash);
