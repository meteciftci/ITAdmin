using ITAdmin.Domain.Enums;

namespace ITAdmin.Api.Contracts.DnsManagement;

public sealed record DnsManagementSettingsResponse(
    bool IsEnabled, bool AutomaticSyncEnabled, int DefaultSyncIntervalMinutes,
    int HealthCheckIntervalMinutes, int CommandTimeoutSeconds, int MaxParallelServers,
    int SnapshotRetentionDays, bool SyncRecordInventory,
    bool PromptForFullSyncOnComparisonOpen, int ComparisonSnapshotStaleAfterMinutes,
    DateTime? UpdatedAt, string? UpdatedBy);

public sealed record UpdateDnsManagementSettingsRequest(
    bool IsEnabled, bool AutomaticSyncEnabled, int DefaultSyncIntervalMinutes,
    int HealthCheckIntervalMinutes, int CommandTimeoutSeconds, int MaxParallelServers,
    int SnapshotRetentionDays, bool SyncRecordInventory,
    bool PromptForFullSyncOnComparisonOpen, int ComparisonSnapshotStaleAfterMinutes);

public sealed record DnsCredentialProfileResponse(
    Guid Id, string Name, DnsAuthenticationMode AuthenticationMode, string UserName,
    bool HasPassword, bool IsEnabled, DateTime? LastValidatedAt,
    string? LastValidationStatus, string? LastValidationMessage);

public sealed record SaveDnsCredentialProfileRequest(
    string Name, DnsAuthenticationMode AuthenticationMode, string UserName,
    string? Password, bool IsEnabled);

public sealed record DnsServerResponse(
    Guid Id, string DisplayName, string HostName, int Port, DnsServerEnvironment Environment,
    Guid CredentialProfileId, string CredentialProfileName, bool IsEnabled,
    int? SyncIntervalMinutes, string? TlsCertificateThumbprint, string? Notes,
    string? OperatingSystemVersion, string? DnsServerVersion,
    DateTime? LastSeenAt, DateTime? LastSuccessfulSyncAt,
    string? LastSyncStatus, string? LastSyncMessage);

public sealed record SaveDnsServerRequest(
    string DisplayName, string HostName, int Port, DnsServerEnvironment Environment,
    Guid CredentialProfileId, bool IsEnabled, int? SyncIntervalMinutes,
    string? TlsCertificateThumbprint, string? Notes);

public sealed record DnsServerCapabilitiesResponse(
    bool Zones, bool Records, bool ServerSettings, bool Dnssec, bool Policies, bool Scopes, bool Cache);

public sealed record DnsServerConnectionTestResponse(
    Guid ServerId, string ServerDisplayName, bool Success, string? FailureKind, string Message,
    bool HostAgentAvailable, bool NetworkReachable, bool TlsValidated,
    bool AuthenticationSucceeded, bool DnsModuleAvailable, bool DnsServiceReachable,
    string? OperatingSystemVersion, string? PowerShellVersion, string? DnsModuleVersion,
    string? DnsServerVersion, int? ZoneCount, DnsServerCapabilitiesResponse? Capabilities,
    DateTime TestedAt);

public sealed record DnsSyncJobResponse(
    Guid Id, Guid BatchId, Guid ServerId, string ServerDisplayName,
    DnsSyncScope Scope, DnsSyncTrigger Trigger, DnsSyncStatus Status,
    int AttemptCount, DateTime RequestedAt, DateTime? StartedAt, DateTime? CompletedAt,
    string? ErrorCode, string? Message, bool AlreadyQueued);
