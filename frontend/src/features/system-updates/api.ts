import { apiClient } from "@/lib/api-client";
import type {
  InstallSystemUpdateResponse,
  SystemUpdateStatus,
} from "@/features/system-updates/types";

export const SYSTEM_UPDATE_STATUS_QUERY_KEY = ["system-updates", "status"] as const;

export async function getSystemUpdateStatus(): Promise<SystemUpdateStatus> {
  const { data } = await apiClient.get<SystemUpdateStatus>("/system/updates/status");
  return data;
}

export async function checkForSystemUpdates(): Promise<SystemUpdateStatus> {
  const { data } = await apiClient.post<SystemUpdateStatus>("/system/updates/check");
  return data;
}

export async function installSystemUpdate(): Promise<InstallSystemUpdateResponse> {
  const { data } = await apiClient.post<InstallSystemUpdateResponse>(
    "/system/updates/install",
    { databaseBackupConfirmed: true },
  );
  return data;
}
