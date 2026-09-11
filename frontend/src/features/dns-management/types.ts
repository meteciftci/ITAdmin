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
