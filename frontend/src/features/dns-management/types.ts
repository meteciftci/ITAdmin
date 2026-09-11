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
