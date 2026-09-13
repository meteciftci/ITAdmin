export type DnsAuthenticationMode = "Negotiate" | "BasicOverTls";
export type DnsServerEnvironment = "Internal" | "Public" | "Other";

export type DnsManagementSettings = {
  isEnabled: boolean;
  automaticSyncEnabled: boolean;
  defaultSyncIntervalMinutes: number;
  healthCheckIntervalMinutes: number;
  commandTimeoutSeconds: number;
  maxParallelServers: number;
  snapshotRetentionDays: number;
  syncRecordInventory: boolean;
  promptForFullSyncOnComparisonOpen: boolean;
  comparisonSnapshotStaleAfterMinutes: number;
  updatedAt?: string | null;
  updatedBy?: string | null;
};

export type DnsCredentialProfile = {
  id: string;
  name: string;
  authenticationMode: DnsAuthenticationMode;
  userName: string;
  hasPassword: boolean;
  isEnabled: boolean;
  lastValidatedAt?: string | null;
  lastValidationStatus?: string | null;
  lastValidationMessage?: string | null;
};

export type SaveDnsCredentialProfile = Omit<DnsCredentialProfile, "id" | "hasPassword" | "lastValidatedAt" | "lastValidationStatus" | "lastValidationMessage"> & { password?: string | null };

export type DnsServer = {
  id: string;
  displayName: string;
  hostName: string;
  port: number;
  environment: DnsServerEnvironment;
  credentialProfileId: string;
  credentialProfileName: string;
  isEnabled: boolean;
  syncIntervalMinutes?: number | null;
  tlsCertificateThumbprint?: string | null;
  notes?: string | null;
  operatingSystemVersion?: string | null;
  dnsServerVersion?: string | null;
  lastSeenAt?: string | null;
  lastSuccessfulSyncAt?: string | null;
  lastSyncStatus?: string | null;
  lastSyncMessage?: string | null;
};

export type SaveDnsServer = Omit<DnsServer, "id" | "credentialProfileName" | "operatingSystemVersion" | "dnsServerVersion" | "lastSeenAt" | "lastSuccessfulSyncAt" | "lastSyncStatus" | "lastSyncMessage">;

export type DnsServerCapabilities = { zones: boolean; records: boolean; serverSettings: boolean; dnssec: boolean; policies: boolean; scopes: boolean; cache: boolean };
export type DnsServerConnectionTest = {
  serverId: string; serverDisplayName: string; success: boolean; failureKind?: string | null; message: string;
  hostAgentAvailable: boolean; networkReachable: boolean; tlsValidated: boolean;
  authenticationSucceeded: boolean; dnsModuleAvailable: boolean; dnsServiceReachable: boolean;
  operatingSystemVersion?: string | null; powerShellVersion?: string | null; dnsModuleVersion?: string | null;
  dnsServerVersion?: string | null; zoneCount?: number | null; capabilities?: DnsServerCapabilities | null; testedAt: string;
};

export type DnsSyncJob = {
  id: string; batchId: string; serverId: string; serverDisplayName: string;
  scope: "Health" | "Zones" | "Records" | "FullInventory";
  trigger: "Scheduled" | "Manual" | "PostMutation";
  status: "Pending" | "Running" | "Completed" | "Failed" | "Cancelled";
  attemptCount: number; requestedAt: string; startedAt?: string | null; completedAt?: string | null;
  errorCode?: string | null; message?: string | null; alreadyQueued: boolean;
};

export type PagedDnsResponse<T> = {
  items: T[]; pageNumber: number; pageSize: number; totalCount: number; totalPages: number;
};

export type DnsInventoryServer = {
  serverId: string; serverDisplayName: string; environment: DnsServerEnvironment; isEnabled: boolean;
  snapshotId?: string | null; snapshotVersion?: string | null;
  snapshotScope?: "Health" | "Zones" | "Records" | "FullInventory" | null;
  snapshotCompletedAt?: string | null; zoneCount: number; recordCount: number;
  lastSyncStatus?: string | null; lastSyncMessage?: string | null;
  isStale: boolean; isAvailable: boolean;
};

export type DnsZoneInventory = {
  id: string; snapshotId: string; serverId: string; serverDisplayName: string;
  environment: DnsServerEnvironment; name: string; zoneType: string;
  isReverseLookupZone: boolean; isDsIntegrated: boolean; isSigned: boolean; isPaused: boolean;
  dynamicUpdate?: string | null; replicationScope?: string | null;
  directoryPartitionName?: string | null; zoneFile?: string | null;
  virtualizationInstance?: string | null; zoneScopes: string[];
  isAutoCreated: boolean; masterServers: string[];
  forwarderTimeoutSeconds?: number | null; useRecursion?: boolean | null;
  recordCount: number; snapshotCompletedAt: string;
};

export type DnsZoneKind = "Primary" | "Secondary" | "Stub" | "Forwarder";
export type SaveDnsZone = {
  name: string; zoneKind: DnsZoneKind; isDsIntegrated: boolean;
  dynamicUpdate?: string | null; replicationScope?: string | null;
  directoryPartitionName?: string | null; zoneFile?: string | null;
  masterServers: string[]; forwarderTimeoutSeconds?: number | null;
  useRecursion?: boolean | null;
};
export type UpdateDnsZone = Pick<SaveDnsZone, "dynamicUpdate" | "masterServers" | "forwarderTimeoutSeconds" | "useRecursion">;
export type DnsZoneMutation = {
  success: boolean; errorCode?: string | null; message: string;
  before?: DnsZoneInventory | null; after?: DnsZoneInventory | null;
  synchronization?: DnsSyncJob | null;
};

export type DnsRecordInventory = {
  id: string; relativeName: string; fullyQualifiedName: string; recordType: string;
  canonicalValue: string; recordDataJson: string; timeToLiveSeconds: number;
  timestamp?: string | null; zoneScope?: string | null; virtualizationInstance?: string | null;
  recordHash: string;
};

export type DnsRecordMutationInput = {
  values: string[];
  timeToLiveSeconds: number;
};

export type CreateDnsRecord = DnsRecordMutationInput & {
  relativeName: string;
  recordType: string;
  zoneScope?: string | null;
};

export type DnsRecordMutation = {
  success: boolean;
  errorCode?: string | null;
  message: string;
  before?: DnsRecordInventory | null;
  after?: DnsRecordInventory | null;
  synchronization?: DnsSyncJob | null;
};

export type DnsComparisonContext = {
  promptForFullSyncOnOpen: boolean;
  lastFullInventorySyncAt: string | null;
  synchronizationInProgress: boolean;
  enabledServerCount: number;
  unavailableServerCount: number;
  servers: DnsInventoryServer[];
};

export type DnsComparisonZone = { name: string; serverCount: number };

export type DnsComparisonStatus = "Equal" | "Different" | "Missing" | "Unavailable" | "Stale";

export type DnsComparisonCell = {
  serverId: string;
  status: DnsComparisonStatus;
  values: string[];
  timeToLiveValues: number[];
};

export type DnsComparisonRow = {
  zoneName: string;
  relativeName: string;
  recordType: string;
  zoneScope: string | null;
  virtualizationInstance: string | null;
  cells: DnsComparisonCell[];
};

export type DnsComparisonResponse = PagedDnsResponse<DnsComparisonRow> & {
  servers: DnsInventoryServer[];
};

export type DnsComparisonRequest = {
  serverIds: string[];
  zoneNames: string[];
  compareTimeToLive: boolean;
  search?: string;
  pageNumber: number;
  pageSize: number;
};

export type DnsSyncBatch = {
  batchId: string;
  targetedCount: number;
  queuedCount: number;
  alreadyQueuedCount: number;
  failedCount: number;
  jobs: DnsSyncJob[];
};

export type DnsServerSettings = {
  forwarderAddresses: string[];
  forwarderUseRootHint: boolean;
  forwarderTimeoutSeconds: number;
  forwarderEnableReordering: boolean;
  recursionEnabled: boolean;
  recursionAdditionalTimeoutSeconds: number;
  recursionRetryIntervalSeconds: number;
  recursionTimeoutSeconds: number;
  recursionSecureResponse: boolean;
  stateToken: string;
};

export type UpdateDnsServerSettings = Omit<DnsServerSettings, "stateToken"> & {
  expectedStateToken: string;
};

export type DnsServerOperation = {
  success: boolean;
  errorCode?: string | null;
  message: string;
  settings?: DnsServerSettings | null;
};

export type DnsPolicyCriterion = { operator: "Eq" | "Ne"; values: string[] };
export type DnsPolicyConfiguration = {
  clientSubnets: { name: string; ipv4Subnets: string[]; ipv6Subnets: string[] }[];
  zoneScopes: { zoneName: string; name: string }[];
  queryPolicies: { name: string; level: "Server" | "Zone"; zoneName: string | null; action: string; condition: string; processingOrder: number; enabled: boolean; clientSubnet: string | null; fqdn: string | null; queryType: string | null; transportProtocol: string | null; internetProtocol: string | null; serverInterfaceIp: string | null; zoneScope: string | null }[];
  stateToken: string;
};
export type DnsPolicyMutationInput = {
  action: "SaveClientSubnet" | "DeleteClientSubnet" | "CreateZoneScope" | "DeleteZoneScope" | "SaveQueryPolicy" | "DeleteQueryPolicy" | "SetQueryPolicyEnabled";
  name: string; zoneName?: string | null; ipv4Subnets?: string[]; ipv6Subnets?: string[];
  level?: "Server" | "Zone"; decision?: "Allow" | "Deny" | "Ignore"; condition?: "And" | "Or";
  processingOrder?: number; enabled?: boolean; clientSubnet?: DnsPolicyCriterion | null; fqdn?: DnsPolicyCriterion | null;
  queryType?: DnsPolicyCriterion | null; transportProtocol?: DnsPolicyCriterion | null; internetProtocol?: DnsPolicyCriterion | null;
  serverInterfaceIp?: DnsPolicyCriterion | null; zoneScopes?: { name: string; weight: number }[]; expectedStateToken: string;
};
export type DnsPolicyOperation = { success: boolean; errorCode?: string | null; message: string; configuration?: DnsPolicyConfiguration | null };

export type DnssecSigningKey = {
  keyId: string; keyType: string; cryptoAlgorithm?: string | null; keyLength?: number | null;
  keyStatus?: string | null; keyStorageProvider?: string | null; isRolloverEnabled?: boolean | null;
  rolloverPeriodSeconds?: number | null; nextRolloverAction?: string | null; nextRolloverTime?: string | null;
};
export type DnssecZone = {
  name: string; zoneType: string; isDsIntegrated: boolean; isAutoCreated: boolean; isSigned: boolean;
  isEligibleForSigning: boolean; ineligibilityReason?: string | null; isKeyMasterServer?: boolean | null;
  keyMasterServer?: string | null; keyMasterStatus?: string | null; denialOfExistence?: string | null;
  nsec3Iterations?: number | null; nsec3OptOut?: boolean | null; dnsKeyRecordSetTtlSeconds?: number | null;
  dsRecordSetTtlSeconds?: number | null; dsRecordGenerationAlgorithms: string[];
  parentHasSecureDelegation?: boolean | null; signingKeys: DnssecSigningKey[];
};
export type DnssecTrustAnchor = { type: string; state?: string | null; data?: string | null };
export type DnssecTrustPoint = {
  name: string; state?: string | null; lastActiveRefreshTime?: string | null;
  nextActiveRefreshTime?: string | null; anchors: DnssecTrustAnchor[];
};
export type DnssecResolverConfiguration = {
  validationEnabled: boolean; isReadOnlyDomainController: boolean; directoryServicesAvailable: boolean;
  rootTrustAnchorsUrl?: string | null; trustPoints: DnssecTrustPoint[];
};
export type DnssecConfiguration = { zones: DnssecZone[]; resolver: DnssecResolverConfiguration; stateToken: string };
export type DnssecMutationInput = {
  action: "SignWithDefaults" | "Resign" | "Unsign" | "RolloverKeys" | "SetValidationEnabled" |
    "RetrieveRootTrustAnchor" | "AddDsTrustAnchor" | "AddDnsKeyTrustAnchor" | "RemoveTrustAnchorType";
  zoneName?: string | null; keyIds?: string[]; validationEnabled?: boolean | null;
  trustPointName?: string | null; trustAnchorType?: "DnsKey" | "Ds" | null;
  cryptoAlgorithm?: string | null; keyTag?: number | null; digestType?: "Sha1" | "Sha256" | "Sha384" | null;
  digest?: string | null; base64Data?: string | null; expectedStateToken: string;
};
export type DnssecOperation = { success: boolean; errorCode?: string | null; message: string; configuration?: DnssecConfiguration | null };

export type DnsOperationLogListItem = {
  id: string;
  createdAt: string;
  serverId: string | null;
  serverDisplayName: string | null;
  operationType: string;
  status: string;
  zoneName: string | null;
  recordName: string | null;
  recordType: string | null;
  actorUserName: string | null;
  errorCode: string | null;
  errorMessage: string | null;
  hasRequestSummary: boolean;
  hasBeforeSnapshot: boolean;
  hasAfterSnapshot: boolean;
};

export type DnsOperationLogDetail = Omit<DnsOperationLogListItem,
  "hasRequestSummary" | "hasBeforeSnapshot" | "hasAfterSnapshot"> & {
  requestSummaryJson: string | null;
  beforeSnapshotJson: string | null;
  afterSnapshotJson: string | null;
  actorUserId: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  correlationId: string | null;
};

export type DnsOperationLogQuery = {
  serverId?: string;
  operationType?: string;
  status?: string;
  targetSearch?: string;
  actorUserName?: string;
  dateFrom?: string;
  dateTo?: string;
  pageNumber: number;
  pageSize: number;
};

export type DnsDownload = { blob: Blob; fileName: string };
