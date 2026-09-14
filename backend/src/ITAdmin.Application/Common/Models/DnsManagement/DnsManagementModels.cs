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
    bool Zones, bool Records, bool ServerSettings, bool Dnssec, bool Policies, bool Scopes, bool Cache,
    bool NetworkConfiguration);

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

public sealed record DnsServerSettingsModel(
    IReadOnlyList<string> ForwarderAddresses, bool ForwarderUseRootHint,
    int ForwarderTimeoutSeconds, bool ForwarderEnableReordering,
    bool RecursionEnabled, int RecursionAdditionalTimeoutSeconds,
    int RecursionRetryIntervalSeconds, int RecursionTimeoutSeconds,
    bool RecursionSecureResponse, string StateToken);

public sealed record DnsServerSettingsCommand(
    Guid ServerId, IReadOnlyList<string> ForwarderAddresses, bool ForwarderUseRootHint,
    int ForwarderTimeoutSeconds, bool ForwarderEnableReordering,
    bool RecursionEnabled, int RecursionAdditionalTimeoutSeconds,
    int RecursionRetryIntervalSeconds, int RecursionTimeoutSeconds,
    bool RecursionSecureResponse, string ExpectedStateToken, DnsActorContext Actor);

public sealed record DnsServerSettingsOperationModel(
    bool Success, string? ErrorCode, string Message,
    DnsServerSettingsModel? Settings = null);

public enum DnsPolicyAction { SaveClientSubnet, DeleteClientSubnet, CreateZoneScope, DeleteZoneScope, SaveQueryPolicy, DeleteQueryPolicy, SetQueryPolicyEnabled }
public enum DnsPolicyLevel { Server, Zone }
public enum DnsPolicyDecision { Allow, Deny, Ignore }
public enum DnsPolicyCondition { And, Or }
public enum DnsPolicyMatchOperator { Eq, Ne }
public sealed record DnsPolicyCriterionModel(DnsPolicyMatchOperator Operator, IReadOnlyList<string> Values);
public sealed record DnsZoneScopeWeightModel(string Name, int Weight);
public sealed record DnsClientSubnetModel(string Name, IReadOnlyList<string> Ipv4Subnets, IReadOnlyList<string> Ipv6Subnets);
public sealed record DnsZoneScopeModel(string ZoneName, string Name);
public sealed record DnsQueryPolicyModel(
    string Name, string Level, string? ZoneName, string Action, string Condition, int ProcessingOrder,
    bool Enabled, string? ClientSubnet, string? Fqdn, string? QueryType, string? TransportProtocol,
    string? InternetProtocol, string? ServerInterfaceIp, string? ZoneScope);
public sealed record DnsPolicyConfigurationModel(
    IReadOnlyList<DnsClientSubnetModel> ClientSubnets, IReadOnlyList<DnsZoneScopeModel> ZoneScopes,
    IReadOnlyList<DnsQueryPolicyModel> QueryPolicies, string StateToken);
public sealed record DnsPolicyMutationCommand(
    Guid ServerId, DnsPolicyAction Action, string Name, string? ZoneName,
    IReadOnlyList<string> Ipv4Subnets, IReadOnlyList<string> Ipv6Subnets,
    DnsPolicyLevel Level, DnsPolicyDecision Decision, DnsPolicyCondition Condition,
    int ProcessingOrder, bool Enabled, DnsPolicyCriterionModel? ClientSubnet,
    DnsPolicyCriterionModel? Fqdn, DnsPolicyCriterionModel? QueryType,
    DnsPolicyCriterionModel? TransportProtocol, DnsPolicyCriterionModel? InternetProtocol,
    DnsPolicyCriterionModel? ServerInterfaceIp, IReadOnlyList<DnsZoneScopeWeightModel> ZoneScopes,
    string ExpectedStateToken, DnsActorContext Actor);
public sealed record DnsPolicyOperationModel(bool Success, string? ErrorCode, string Message, DnsPolicyConfigurationModel? Configuration = null);

public enum DnssecAction { SignWithDefaults, Resign, Unsign, RolloverKeys, SetValidationEnabled, RetrieveRootTrustAnchor, AddDsTrustAnchor, AddDnsKeyTrustAnchor, RemoveTrustAnchorType }
public sealed record DnssecSigningKeyModel(
    Guid KeyId, string KeyType, string? CryptoAlgorithm, int? KeyLength, string? KeyStatus,
    string? KeyStorageProvider, bool? IsRolloverEnabled, long? RolloverPeriodSeconds,
    string? NextRolloverAction, DateTimeOffset? NextRolloverTime);
public sealed record DnssecZoneModel(
    string Name, string ZoneType, bool IsDsIntegrated, bool IsAutoCreated, bool IsSigned,
    bool IsEligibleForSigning, string? IneligibilityReason, bool? IsKeyMasterServer,
    string? KeyMasterServer, string? KeyMasterStatus, string? DenialOfExistence,
    int? Nsec3Iterations, bool? Nsec3OptOut, long? DnsKeyRecordSetTtlSeconds,
    long? DsRecordSetTtlSeconds, IReadOnlyList<string> DsRecordGenerationAlgorithms,
    bool? ParentHasSecureDelegation, IReadOnlyList<DnssecSigningKeyModel> SigningKeys);
public sealed record DnssecTrustAnchorModel(string Type, string? State, string? Data);
public sealed record DnssecTrustPointModel(
    string Name, string? State, DateTimeOffset? LastActiveRefreshTime, DateTimeOffset? NextActiveRefreshTime,
    IReadOnlyList<DnssecTrustAnchorModel> Anchors);
public sealed record DnssecResolverConfigurationModel(
    bool ValidationEnabled, bool IsReadOnlyDomainController, bool DirectoryServicesAvailable,
    string? RootTrustAnchorsUrl, IReadOnlyList<DnssecTrustPointModel> TrustPoints);
public sealed record DnssecConfigurationModel(
    IReadOnlyList<DnssecZoneModel> Zones, DnssecResolverConfigurationModel Resolver, string StateToken);
public sealed record DnssecMutationCommand(
    Guid ServerId, DnssecAction Action, string? ZoneName, IReadOnlyList<Guid> KeyIds,
    bool? ValidationEnabled, string? TrustPointName, string? TrustAnchorType,
    string? CryptoAlgorithm, int? KeyTag, string? DigestType, string? Digest, string? Base64Data,
    string ExpectedStateToken, DnsActorContext Actor);
public sealed record DnssecOperationModel(
    bool Success, string? ErrorCode, string Message, DnssecConfigurationModel? Configuration = null);

public enum DnsScavengingAction { UpdateServer, UpdateZone, StartScavenging }
public sealed record DnsZoneAgingModel(
    string Name, string ZoneType, bool AgingEnabled, bool IsEligible, string? IneligibilityReason,
    long NoRefreshIntervalSeconds, long RefreshIntervalSeconds, DateTimeOffset? AvailableForScavengeTime,
    IReadOnlyList<string> ScavengeServers);
public sealed record DnsScavengingConfigurationModel(
    bool ScavengingEnabled, long ScavengingIntervalSeconds, long DefaultNoRefreshIntervalSeconds,
    long DefaultRefreshIntervalSeconds, DateTimeOffset? LastScavengeTime,
    IReadOnlyList<DnsZoneAgingModel> Zones, string StateToken);
public sealed record DnsScavengingMutationCommand(
    Guid ServerId, DnsScavengingAction Action, bool? ScavengingEnabled, int? ScavengingIntervalHours,
    string? ZoneName, bool? ZoneAgingEnabled, int? NoRefreshIntervalHours, int? RefreshIntervalHours,
    IReadOnlyList<string> ScavengeServers, string ExpectedStateToken, DnsActorContext Actor);
public sealed record DnsScavengingOperationModel(
    bool Success, string? ErrorCode, string Message, DnsScavengingConfigurationModel? Configuration = null);

public enum DnsNetworkAction { UpdateListeningAddresses, AddRootHint, UpdateRootHint, RemoveRootHint }
public sealed record DnsRootHintModel(string NameServer, IReadOnlyList<string> IpAddresses);
public sealed record DnsNetworkConfigurationModel(
    IReadOnlyList<string> ListeningIpAddresses, IReadOnlyList<string> AvailableIpAddresses,
    IReadOnlyList<DnsRootHintModel> RootHints, string StateToken);
public sealed record DnsNetworkMutationCommand(
    Guid ServerId, DnsNetworkAction Action, IReadOnlyList<string> ListeningIpAddresses,
    string? RootHintNameServer, IReadOnlyList<string> RootHintIpAddresses,
    string? OriginalRootHintNameServer, string ExpectedStateToken, DnsActorContext Actor);
public sealed record DnsNetworkOperationModel(
    bool Success, string? ErrorCode, string Message, DnsNetworkConfigurationModel? Configuration = null);

public sealed record DnsOperationLogQuery(
    Guid? ServerId, string? OperationType, string? Status,
    string? TargetSearch, string? ActorUserName,
    DateTimeOffset? DateFrom, DateTimeOffset? DateTo,
    int PageNumber, int PageSize);

public sealed record DnsOperationLogListItemModel(
    Guid Id, DateTimeOffset CreatedAt, Guid? ServerId, string? ServerDisplayName,
    string OperationType, string Status, string? ZoneName, string? RecordName,
    string? RecordType, string? ActorUserName, string? ErrorCode,
    string? ErrorMessage, bool HasRequestSummary, bool HasBeforeSnapshot,
    bool HasAfterSnapshot);

public sealed record DnsOperationLogDetailModel(
    Guid Id, DateTimeOffset CreatedAt, Guid? ServerId, string? ServerDisplayName,
    string OperationType, string Status, string? ZoneName, string? RecordName,
    string? RecordType, string? RequestSummaryJson, string? BeforeSnapshotJson,
    string? AfterSnapshotJson, string? ErrorCode, string? ErrorMessage,
    Guid? ActorUserId, string? ActorUserName, string? IpAddress,
    string? UserAgent, string? CorrelationId);
