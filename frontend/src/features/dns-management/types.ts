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
  recordCount: number; snapshotCompletedAt: string;
};

export type DnsRecordInventory = {
  id: string; relativeName: string; fullyQualifiedName: string; recordType: string;
  canonicalValue: string; recordDataJson: string; timeToLiveSeconds: number;
  timestamp?: string | null; zoneScope?: string | null; virtualizationInstance?: string | null;
  recordHash: string;
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
