using ITAdmin.Domain.Enums;
using ITAdmin.Application.Common.Models.DnsManagement;

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

public sealed record DnsServerSettingsResponse(
    IReadOnlyList<string> ForwarderAddresses, bool ForwarderUseRootHint,
    int ForwarderTimeoutSeconds, bool ForwarderEnableReordering,
    bool RecursionEnabled, int RecursionAdditionalTimeoutSeconds,
    int RecursionRetryIntervalSeconds, int RecursionTimeoutSeconds,
    bool RecursionSecureResponse, string StateToken);

public sealed record UpdateDnsServerSettingsRequest(
    IReadOnlyList<string>? ForwarderAddresses, bool ForwarderUseRootHint,
    int ForwarderTimeoutSeconds, bool ForwarderEnableReordering,
    bool RecursionEnabled, int RecursionAdditionalTimeoutSeconds,
    int RecursionRetryIntervalSeconds, int RecursionTimeoutSeconds,
    bool RecursionSecureResponse, string ExpectedStateToken);

public sealed record DnsServerOperationResponse(
    bool Success, string? ErrorCode, string Message,
    DnsServerSettingsResponse? Settings = null);

public sealed record DnsPolicyCriterionRequest(DnsPolicyMatchOperator Operator, IReadOnlyList<string>? Values);
public sealed record DnsZoneScopeWeightRequest(string Name, int Weight);
public sealed record DnsPolicyMutationRequest(
    DnsPolicyAction Action, string Name, string? ZoneName,
    IReadOnlyList<string>? Ipv4Subnets, IReadOnlyList<string>? Ipv6Subnets,
    DnsPolicyLevel Level, DnsPolicyDecision Decision, DnsPolicyCondition Condition,
    int ProcessingOrder, bool Enabled, DnsPolicyCriterionRequest? ClientSubnet,
    DnsPolicyCriterionRequest? Fqdn, DnsPolicyCriterionRequest? QueryType,
    DnsPolicyCriterionRequest? TransportProtocol, DnsPolicyCriterionRequest? InternetProtocol,
    DnsPolicyCriterionRequest? ServerInterfaceIp, IReadOnlyList<DnsZoneScopeWeightRequest>? ZoneScopes,
    string ExpectedStateToken);
public sealed record DnsClientSubnetResponse(string Name, IReadOnlyList<string> Ipv4Subnets, IReadOnlyList<string> Ipv6Subnets);
public sealed record DnsZoneScopeResponse(string ZoneName, string Name);
public sealed record DnsQueryPolicyResponse(
    string Name, string Level, string? ZoneName, string Action, string Condition, int ProcessingOrder,
    bool Enabled, string? ClientSubnet, string? Fqdn, string? QueryType, string? TransportProtocol,
    string? InternetProtocol, string? ServerInterfaceIp, string? ZoneScope);
public sealed record DnsPolicyConfigurationResponse(
    IReadOnlyList<DnsClientSubnetResponse> ClientSubnets, IReadOnlyList<DnsZoneScopeResponse> ZoneScopes,
    IReadOnlyList<DnsQueryPolicyResponse> QueryPolicies, string StateToken);
public sealed record DnsPolicyOperationResponse(bool Success, string? ErrorCode, string Message, DnsPolicyConfigurationResponse? Configuration = null);

public sealed record DnssecSigningKeyResponse(
    Guid KeyId, string KeyType, string? CryptoAlgorithm, int? KeyLength, string? KeyStatus,
    string? KeyStorageProvider, bool? IsRolloverEnabled, long? RolloverPeriodSeconds,
    string? NextRolloverAction, DateTimeOffset? NextRolloverTime);
public sealed record DnssecZoneResponse(
    string Name, string ZoneType, bool IsDsIntegrated, bool IsAutoCreated, bool IsSigned,
    bool IsEligibleForSigning, string? IneligibilityReason, bool? IsKeyMasterServer,
    string? KeyMasterServer, string? KeyMasterStatus, string? DenialOfExistence,
    int? Nsec3Iterations, bool? Nsec3OptOut, long? DnsKeyRecordSetTtlSeconds,
    long? DsRecordSetTtlSeconds, IReadOnlyList<string> DsRecordGenerationAlgorithms,
    bool? ParentHasSecureDelegation, IReadOnlyList<DnssecSigningKeyResponse> SigningKeys);
public sealed record DnssecConfigurationResponse(IReadOnlyList<DnssecZoneResponse> Zones, string StateToken);
public sealed record DnssecMutationRequest(
    DnssecAction Action, string ZoneName, IReadOnlyList<Guid>? KeyIds, string ExpectedStateToken);
public sealed record DnssecOperationResponse(
    bool Success, string? ErrorCode, string Message, DnssecConfigurationResponse? Configuration = null);

public sealed record DnsOperationLogListItemResponse(
    Guid Id, DateTimeOffset CreatedAt, Guid? ServerId, string? ServerDisplayName,
    string OperationType, string Status, string? ZoneName, string? RecordName,
    string? RecordType, string? ActorUserName, string? ErrorCode,
    string? ErrorMessage, bool HasRequestSummary, bool HasBeforeSnapshot,
    bool HasAfterSnapshot);

public sealed record DnsOperationLogDetailResponse(
    Guid Id, DateTimeOffset CreatedAt, Guid? ServerId, string? ServerDisplayName,
    string OperationType, string Status, string? ZoneName, string? RecordName,
    string? RecordType, string? RequestSummaryJson, string? BeforeSnapshotJson,
    string? AfterSnapshotJson, string? ErrorCode, string? ErrorMessage,
    Guid? ActorUserId, string? ActorUserName, string? IpAddress,
    string? UserAgent, string? CorrelationId);
