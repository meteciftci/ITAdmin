import type { QueryClient } from "@tanstack/react-query";

import type { RestoreAdDeletedObjectRequest } from "@/features/ad-management/ad-deleted-object-restore-types";
import { AD_OPERATION_LOGS_QUERY_KEY } from "@/features/ad-management/operation-logs-api";
import { apiClient } from "@/lib/api-client";

import { AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY } from "./query-keys.ts";
import type {
  AdDeletedObjectDetail,
  AdDeletedObjectListResponse,
  AdDeletedObjectRestoreReadinessResult,
  AdDeletedObjectRestoreResponse,
  GetAdDeletedObjectsParams,
} from "@/features/ad-management/types";

export const getAdDeletedObjects = async (
  params: GetAdDeletedObjectsParams,
): Promise<AdDeletedObjectListResponse> => {
  const { data } = await apiClient.get<AdDeletedObjectListResponse>(
    "/ad-management/deleted-objects",
    {
      params: {
        search: params.search,
        type: params.type ?? "all",
        pageNumber: params.pageNumber ?? 1,
        pageSize: params.pageSize ?? 20,
        includeAll: params.includeAll ?? false,
      },
    },
  );
  return data;
};

export const getAdDeletedObjectById = async (id: string): Promise<AdDeletedObjectDetail> => {
  const { data } = await apiClient.get<AdDeletedObjectDetail>(
    `/ad-management/deleted-objects/${id}`,
  );
  return data;
};

export const restoreAdDeletedObject = async (
  id: string,
  payload?: RestoreAdDeletedObjectRequest,
): Promise<AdDeletedObjectRestoreResponse> => {
  const { data } = await apiClient.post<AdDeletedObjectRestoreResponse>(
    `/ad-management/deleted-objects/${id}/restore`,
    payload ?? undefined,
  );
  return data;
};

export const getAdDeletedObjectRestoreReadiness =
  async (): Promise<AdDeletedObjectRestoreReadinessResult> => {
    const { data } = await apiClient.get<AdDeletedObjectRestoreReadinessResult>(
      "/ad-management/deleted-objects/restore-readiness",
    );
    return data;
  };

export async function invalidateAdManagementDeletedObjectQueries(
  queryClient: QueryClient,
): Promise<void> {
  await queryClient.invalidateQueries({ queryKey: AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY });
}

export async function invalidateAdManagementDeletedObjectRestoreQueries(
  queryClient: QueryClient,
): Promise<void> {
  await invalidateAdManagementDeletedObjectQueries(queryClient);
  await queryClient.invalidateQueries({ queryKey: AD_OPERATION_LOGS_QUERY_KEY });
}
