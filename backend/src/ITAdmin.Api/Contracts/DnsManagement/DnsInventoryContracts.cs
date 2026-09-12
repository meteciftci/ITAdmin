using ITAdmin.Domain.Enums;
using AppModels = ITAdmin.Application.Common.Models.DnsManagement;

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
    bool IsAutoCreated, IReadOnlyList<string> MasterServers,
    int? ForwarderTimeoutSeconds, bool? UseRecursion,
    int RecordCount, DateTime SnapshotCompletedAt);

public sealed record DnsRecordInventoryResponse(
    Guid Id, string RelativeName, string FullyQualifiedName, string RecordType,
    string CanonicalValue, string RecordDataJson, int TimeToLiveSeconds,
    DateTime? Timestamp, string? ZoneScope, string? VirtualizationInstance,
    string RecordHash);

public sealed record DnsComparisonContextResponse(
    bool PromptForFullSyncOnOpen, DateTime? LastFullInventorySyncAt,
    bool SynchronizationInProgress, int EnabledServerCount, int UnavailableServerCount,
    IReadOnlyList<DnsInventoryServerResponse> Servers);

public sealed record DnsComparisonZoneResponse(string Name, int ServerCount);

public sealed record DnsComparisonRequest(
    IReadOnlyList<Guid> ServerIds, IReadOnlyList<string> ZoneNames,
    bool CompareTimeToLive, string? Search, int PageNumber = 1, int PageSize = 20);

public sealed record DnsComparisonCellResponse(
    Guid ServerId, string Status, IReadOnlyList<string> Values,
    IReadOnlyList<int> TimeToLiveValues);

public sealed record DnsComparisonRowResponse(
    string ZoneName, string RelativeName, string RecordType,
    string? ZoneScope, string? VirtualizationInstance,
    IReadOnlyList<DnsComparisonCellResponse> Cells);

public sealed record DnsComparisonResponse(
    IReadOnlyList<DnsInventoryServerResponse> Servers,
    IReadOnlyList<DnsComparisonRowResponse> Items,
    int PageNumber, int PageSize, int TotalCount, int TotalPages);

public sealed record DnsSyncBatchResponse(
    Guid BatchId, int TargetedCount, int QueuedCount, int AlreadyQueuedCount,
    int FailedCount, IReadOnlyList<DnsSyncJobResponse> Jobs);

public sealed record CreateDnsRecordRequest(
    string RelativeName, string RecordType, IReadOnlyList<string> Values,
    int TimeToLiveSeconds, string? ZoneScope);

public sealed record UpdateDnsRecordRequest(
    IReadOnlyList<string> Values, int TimeToLiveSeconds, string ExpectedRecordHash);

public sealed record DeleteDnsRecordRequest(string ExpectedRecordHash);

public sealed record DnsRecordMutationResponse(
    bool Success, string? ErrorCode, string Message,
    DnsRecordInventoryResponse? Before, DnsRecordInventoryResponse? After,
    DnsSyncJobResponse? Synchronization);

public sealed record CreateDnsZoneRequest(
    string Name, AppModels.DnsZoneKind ZoneKind, bool IsDsIntegrated,
    string? DynamicUpdate, string? ReplicationScope, string? DirectoryPartitionName,
    string? ZoneFile, IReadOnlyList<string> MasterServers,
    int? ForwarderTimeoutSeconds, bool? UseRecursion);

public sealed record UpdateDnsZoneRequest(
    string? DynamicUpdate, IReadOnlyList<string> MasterServers,
    int? ForwarderTimeoutSeconds, bool? UseRecursion);

public sealed record DnsZoneMutationResponse(
    bool Success, string? ErrorCode, string Message,
    DnsZoneInventoryResponse? Before, DnsZoneInventoryResponse? After,
    DnsSyncJobResponse? Synchronization);
