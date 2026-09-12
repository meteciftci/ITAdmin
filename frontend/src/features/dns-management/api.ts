import { apiClient } from "@/lib/api-client";
import type { CreateDnsRecord, DnsComparisonContext, DnsComparisonRequest, DnsComparisonResponse, DnsComparisonZone, DnsCredentialProfile, DnsInventoryServer, DnsManagementSettings, DnsRecordInventory, DnsRecordMutation, DnsRecordMutationInput, DnsServer, DnsServerConnectionTest, DnsServerOperation, DnsServerSettings, DnsSyncBatch, DnsSyncJob, DnsZoneInventory, DnsZoneMutation, PagedDnsResponse, SaveDnsCredentialProfile, SaveDnsServer, SaveDnsZone, UpdateDnsServerSettings, UpdateDnsZone } from "./types";

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
export const DNS_SERVER_SETTINGS_QUERY_KEY = ["dns-management", "server-settings"] as const;
export const getDnsServerSettings = async (id: string) =>
  (await apiClient.get<DnsServerSettings>(`${basePath}/servers/${id}/server-settings`)).data;
export const updateDnsServerSettings = async (id: string, request: UpdateDnsServerSettings) =>
  (await apiClient.put<DnsServerOperation>(`${basePath}/servers/${id}/server-settings`, request)).data;
export const clearDnsServerCache = async (id: string) =>
  (await apiClient.post<DnsServerOperation>(`${basePath}/servers/${id}/cache/clear`)).data;

export const DNS_INVENTORY_SERVERS_QUERY_KEY = ["dns-management", "inventory", "servers"] as const;
export const DNS_ZONES_QUERY_KEY = ["dns-management", "inventory", "zones"] as const;
export const getDnsInventoryServers = async () =>
  (await apiClient.get<DnsInventoryServer[]>(`${basePath}/inventory/servers`)).data;
export const getDnsZones = async (params: { serverId?: string; search?: string; pageNumber: number; pageSize: number }) =>
  (await apiClient.get<PagedDnsResponse<DnsZoneInventory>>(`${basePath}/inventory/zones`, { params })).data;
export const getDnsZone = async (id: string) =>
  (await apiClient.get<DnsZoneInventory>(`${basePath}/inventory/zones/${id}`)).data;
export const createDnsZone = async (serverId: string, request: SaveDnsZone) =>
  (await apiClient.post<DnsZoneMutation>(`${basePath}/inventory/servers/${serverId}/zones`, request)).data;
export const updateDnsZone = async (zoneId: string, request: UpdateDnsZone) =>
  (await apiClient.put<DnsZoneMutation>(`${basePath}/inventory/zones/${zoneId}`, request)).data;
export const deleteDnsZone = async (zoneId: string) =>
  (await apiClient.delete<DnsZoneMutation>(`${basePath}/inventory/zones/${zoneId}`)).data;
export const getDnsRecords = async (id: string, params: { search?: string; recordType?: string; pageNumber: number; pageSize: number }) =>
  (await apiClient.get<PagedDnsResponse<DnsRecordInventory>>(`${basePath}/inventory/zones/${id}/records`, { params })).data;
export const createDnsRecord = async (zoneId: string, request: CreateDnsRecord) =>
  (await apiClient.post<DnsRecordMutation>(`${basePath}/inventory/zones/${zoneId}/records`, request)).data;
export const updateDnsRecord = async (zoneId: string, record: DnsRecordInventory, request: DnsRecordMutationInput) =>
  (await apiClient.put<DnsRecordMutation>(`${basePath}/inventory/zones/${zoneId}/records/${record.id}`, { ...request, expectedRecordHash: record.recordHash })).data;
export const deleteDnsRecord = async (zoneId: string, record: DnsRecordInventory) =>
  (await apiClient.delete<DnsRecordMutation>(`${basePath}/inventory/zones/${zoneId}/records/${record.id}`, { data: { expectedRecordHash: record.recordHash } })).data;

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
