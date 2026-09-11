import { apiClient } from "@/lib/api-client";
import type { DnsComparisonContext, DnsComparisonRequest, DnsComparisonResponse, DnsComparisonZone, DnsCredentialProfile, DnsInventoryServer, DnsManagementSettings, DnsRecordInventory, DnsServer, DnsServerConnectionTest, DnsSyncBatch, DnsSyncJob, DnsZoneInventory, PagedDnsResponse, SaveDnsCredentialProfile, SaveDnsServer } from "./types";

const basePath = "/dns-management";
export const DNS_SETTINGS_QUERY_KEY = ["dns-management", "settings"] as const;
export const DNS_CREDENTIALS_QUERY_KEY = ["dns-management", "credential-profiles"] as const;
export const DNS_SERVERS_QUERY_KEY = ["dns-management", "servers"] as const;

export const getDnsSettings = async () => (await apiClient.get<DnsManagementSettings>(`${basePath}/settings`)).data;
export const updateDnsSettings = async (request: DnsManagementSettings) => (await apiClient.put<DnsManagementSettings>(`${basePath}/settings`, request)).data;
export const getDnsCredentials = async () => (await apiClient.get<DnsCredentialProfile[]>(`${basePath}/credential-profiles`)).data;
export const saveDnsCredential = async (id: string | null, request: SaveDnsCredentialProfile) =>
  (id ? apiClient.put<DnsCredentialProfile>(`${basePath}/credential-profiles/${id}`, request) : apiClient.post<DnsCredentialProfile>(`${basePath}/credential-profiles`, request)).then((x) => x.data);
export const deleteDnsCredential = async (id: string) => { await apiClient.delete(`${basePath}/credential-profiles/${id}`); };
export const getDnsServers = async () => (await apiClient.get<DnsServer[]>(`${basePath}/servers`)).data;
export const saveDnsServer = async (id: string | null, request: SaveDnsServer) =>
  (id ? apiClient.put<DnsServer>(`${basePath}/servers/${id}`, request) : apiClient.post<DnsServer>(`${basePath}/servers`, request)).then((x) => x.data);
export const testDnsServerConnection = async (id: string) =>
  (await apiClient.post<DnsServerConnectionTest>(`${basePath}/servers/${id}/test-connection`)).data;
export const synchronizeDnsServer = async (id: string) =>
  (await apiClient.post<DnsSyncJob>(`${basePath}/servers/${id}/synchronizations`)).data;

export const DNS_INVENTORY_SERVERS_QUERY_KEY = ["dns-management", "inventory", "servers"] as const;
export const DNS_ZONES_QUERY_KEY = ["dns-management", "inventory", "zones"] as const;
export const getDnsInventoryServers = async () =>
  (await apiClient.get<DnsInventoryServer[]>(`${basePath}/inventory/servers`)).data;
export const getDnsZones = async (params: { serverId?: string; search?: string; pageNumber: number; pageSize: number }) =>
  (await apiClient.get<PagedDnsResponse<DnsZoneInventory>>(`${basePath}/inventory/zones`, { params })).data;
export const getDnsZone = async (id: string) =>
  (await apiClient.get<DnsZoneInventory>(`${basePath}/inventory/zones/${id}`)).data;
export const getDnsRecords = async (id: string, params: { search?: string; recordType?: string; pageNumber: number; pageSize: number }) =>
  (await apiClient.get<PagedDnsResponse<DnsRecordInventory>>(`${basePath}/inventory/zones/${id}/records`, { params })).data;

export const DNS_COMPARISON_CONTEXT_QUERY_KEY = ["dns-management", "inventory", "comparison", "context"] as const;
export const DNS_COMPARISON_ZONES_QUERY_KEY = ["dns-management", "inventory", "comparison", "zones"] as const;
export const DNS_COMPARISON_RESULTS_QUERY_KEY = ["dns-management", "inventory", "comparison", "results"] as const;
export const getDnsComparisonContext = async () =>
  (await apiClient.get<DnsComparisonContext>(`${basePath}/inventory/comparison/context`)).data;
export const getDnsComparisonZones = async (serverIds: string[], search?: string) =>
  (await apiClient.get<DnsComparisonZone[]>(`${basePath}/inventory/comparison/zones`, {
    params: { serverIds, search, limit: 200 },
    paramsSerializer: { indexes: null },
  })).data;
export const compareDnsInventory = async (request: DnsComparisonRequest) =>
  (await apiClient.post<DnsComparisonResponse>(`${basePath}/inventory/comparison/query`, request)).data;
export const synchronizeAllDnsInventory = async () =>
  (await apiClient.post<DnsSyncBatch>(`${basePath}/inventory/comparison/synchronizations`)).data;
