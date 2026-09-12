using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models.DnsManagement;

public sealed record DnsActorContext(Guid? UserId, string? UserName, string? IpAddress, string? UserAgent);

public sealed record DnsManagementSettingsModel(
    bool IsEnabled, bool AutomaticSyncEnabled, int DefaultSyncIntervalMinutes,
    int HealthCheckIntervalMinutes, int CommandTimeoutSeconds, int MaxParallelServers,
    int SnapshotRetentionDays, bool SyncRecordInventory,
    bool PromptForFullSyncOnComparisonOpen, int ComparisonSnapshotStaleAfterMinutes,
    DateTime? UpdatedAt, string? UpdatedBy);

public sealed record UpdateDnsManagementSettingsRequest(
    bool IsEnabled, bool AutomaticSyncEnabled, int DefaultSyncIntervalMinutes,
    int HealthCheckIntervalMinutes, int CommandTimeoutSeconds, int MaxParallelServers,
    int SnapshotRetentionDays, bool SyncRecordInventory,
    bool PromptForFullSyncOnComparisonOpen, int ComparisonSnapshotStaleAfterMinutes,
    DnsActorContext Actor);

public sealed record DnsCredentialProfileModel(
    Guid Id, string Name, DnsAuthenticationMode AuthenticationMode, string UserName,
    bool HasPassword, bool IsEnabled, DateTime? LastValidatedAt,
    string? LastValidationStatus, string? LastValidationMessage);

public sealed record SaveDnsCredentialProfileRequest(
    Guid? Id, string Name, DnsAuthenticationMode AuthenticationMode, string UserName,
    string? Password, bool IsEnabled, DnsActorContext Actor);

public sealed record DnsServerModel(
    Guid Id, string DisplayName, string HostName, int Port, DnsServerEnvironment Environment,
    Guid CredentialProfileId, string CredentialProfileName, bool IsEnabled,
    int? SyncIntervalMinutes, string? TlsCertificateThumbprint, string? Notes,
    string? OperatingSystemVersion, string? DnsServerVersion,
    DateTime? LastSeenAt, DateTime? LastSuccessfulSyncAt,
    string? LastSyncStatus, string? LastSyncMessage);

public sealed record SaveDnsServerRequest(
    Guid? Id, string DisplayName, string HostName, int Port, DnsServerEnvironment Environment,
    Guid CredentialProfileId, bool IsEnabled, int? SyncIntervalMinutes,
    string? TlsCertificateThumbprint, string? Notes, DnsActorContext Actor);

public sealed record DnsAdministrationResult<T>(bool IsSuccess, string Message, T? Value = default);

public sealed record DnsServerCapabilitiesModel(
    bool Zones, bool Records, bool ServerSettings, bool Dnssec, bool Policies, bool Scopes, bool Cache);

public sealed record DnsServerConnectionTestModel(
    Guid ServerId, string ServerDisplayName, bool Success, string? FailureKind, string Message,
    bool HostAgentAvailable, bool NetworkReachable, bool TlsValidated,
    bool AuthenticationSucceeded, bool DnsModuleAvailable, bool DnsServiceReachable,
    string? OperatingSystemVersion, string? PowerShellVersion, string? DnsModuleVersion,
    string? DnsServerVersion, int? ZoneCount, DnsServerCapabilitiesModel? Capabilities,
    DateTime TestedAt);

public sealed record DnsSyncJobModel(
    Guid Id, Guid BatchId, Guid ServerId, string ServerDisplayName,
    DnsSyncScope Scope, DnsSyncTrigger Trigger, DnsSyncStatus Status,
    int AttemptCount, DateTime RequestedAt, DateTime? StartedAt, DateTime? CompletedAt,
    string? ErrorCode, string? Message, bool AlreadyQueued);

public sealed record DnsSyncBatchModel(
    Guid BatchId, int TargetedCount, int QueuedCount, int AlreadyQueuedCount,
    int FailedCount, IReadOnlyList<DnsSyncJobModel> Jobs);

public enum DnsRecordMutationKind
{
    Create = 0,
    Update = 1,
    Delete = 2,
}

public sealed record DnsRecordMutationCommand(
    Guid ZoneSnapshotId, Guid? RecordSnapshotId, string RelativeName, string RecordType,
    IReadOnlyList<string> Values, int TimeToLiveSeconds, string? ZoneScope,
    string? ExpectedRecordHash, DnsRecordMutationKind Kind,
    DnsActorContext Actor);

public sealed record DnsRecordMutationModel(
    bool Success, string? ErrorCode, string Message,
    DnsRecordInventoryModel? Before, DnsRecordInventoryModel? After,
    DnsSyncJobModel? Synchronization);

public enum DnsZoneMutationKind
{
    Create = 0,
    Update = 1,
    Delete = 2,
}

public enum DnsZoneKind
{
    Primary = 0,
    Secondary = 1,
    Stub = 2,
    Forwarder = 3,
}

public sealed record DnsZoneMutationCommand(
    Guid ServerId, Guid? ZoneSnapshotId, string Name, DnsZoneKind ZoneKind,
    bool IsDsIntegrated, string? DynamicUpdate, string? ReplicationScope,
    string? DirectoryPartitionName, string? ZoneFile, IReadOnlyList<string> MasterServers,
    int? ForwarderTimeoutSeconds, bool? UseRecursion, DnsZoneMutationKind Kind,
    DnsActorContext Actor);

public sealed record DnsZoneMutationModel(
    bool Success, string? ErrorCode, string Message,
    DnsZoneInventoryModel? Before, DnsZoneInventoryModel? After,
    DnsSyncJobModel? Synchronization);
