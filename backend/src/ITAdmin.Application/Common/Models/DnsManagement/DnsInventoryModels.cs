using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models.DnsManagement;

public sealed record DnsInventoryServerModel(
    Guid ServerId, string ServerDisplayName, DnsServerEnvironment Environment, bool IsEnabled,
    Guid? SnapshotId, Guid? SnapshotVersion, DnsSyncScope? SnapshotScope,
    DateTime? SnapshotCompletedAt, int ZoneCount, int RecordCount,
    string? LastSyncStatus, string? LastSyncMessage, bool IsStale, bool IsAvailable);

public sealed record DnsZoneInventoryQuery(
    Guid? ServerId, string? Search, int PageNumber, int PageSize);

public sealed record DnsZoneInventoryModel(
    Guid Id, Guid SnapshotId, Guid ServerId, string ServerDisplayName,
    DnsServerEnvironment Environment, string Name, string ZoneType,
    bool IsReverseLookupZone, bool IsDsIntegrated, bool IsSigned, bool IsPaused,
    string? DynamicUpdate, string? ReplicationScope, string? DirectoryPartitionName,
    string? ZoneFile, string? VirtualizationInstance, IReadOnlyList<string> ZoneScopes,
    bool IsAutoCreated, IReadOnlyList<string> MasterServers,
    int? ForwarderTimeoutSeconds, bool? UseRecursion,
    int RecordCount, DateTime SnapshotCompletedAt);

public sealed record DnsRecordInventoryQuery(
    Guid ZoneSnapshotId, string? Search, string? RecordType, int PageNumber, int PageSize);

public sealed record DnsRecordInventoryModel(
    Guid Id, string RelativeName, string FullyQualifiedName, string RecordType,
    string CanonicalValue, string RecordDataJson, int TimeToLiveSeconds,
    DateTime? Timestamp, string? ZoneScope, string? VirtualizationInstance,
    string RecordHash);

public sealed record DnsComparisonContextModel(
    bool PromptForFullSyncOnOpen, DateTime? LastFullInventorySyncAt,
    bool SynchronizationInProgress, int EnabledServerCount, int UnavailableServerCount,
    IReadOnlyList<DnsInventoryServerModel> Servers);

public sealed record DnsComparisonZoneModel(string Name, int ServerCount);

public sealed record DnsComparisonQuery(
    IReadOnlyList<Guid> ServerIds, IReadOnlyList<string> ZoneNames,
    bool CompareTimeToLive, string? Search, int PageNumber, int PageSize);

public sealed record DnsComparisonCellModel(
    Guid ServerId, string Status, IReadOnlyList<string> Values,
    IReadOnlyList<int> TimeToLiveValues);

public sealed record DnsComparisonRowModel(
    string ZoneName, string RelativeName, string RecordType,
    string? ZoneScope, string? VirtualizationInstance,
    IReadOnlyList<DnsComparisonCellModel> Cells);

public sealed record DnsComparisonResultModel(
    IReadOnlyList<DnsInventoryServerModel> Servers,
    IReadOnlyList<DnsComparisonRowModel> Items,
    int PageNumber, int PageSize, int TotalCount, int TotalPages);
