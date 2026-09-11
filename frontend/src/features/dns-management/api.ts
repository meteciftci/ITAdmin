import { apiClient } from "@/lib/api-client";
import type { DnsCredentialProfile, DnsManagementSettings, DnsServer, SaveDnsCredentialProfile, SaveDnsServer } from "./types";

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
