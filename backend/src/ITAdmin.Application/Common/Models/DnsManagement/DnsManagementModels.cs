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
