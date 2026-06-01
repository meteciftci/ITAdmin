import { apiClient } from "@/lib/api-client";

import type {
  AdOperationLogDetail,
  AdOperationLogFilters,
  AdOperationLogListItem,
  PagedResponse,
} from "@/features/ad-management/operation-logs-types";

export const AD_OPERATION_LOGS_QUERY_KEY = ["ad-management", "operation-logs"] as const;

export async function getAdOperationLogs(
  params: AdOperationLogFilters = { pageNumber: 1, pageSize: 20 },
): Promise<PagedResponse<AdOperationLogListItem>> {
  const { data } = await apiClient.get<PagedResponse<AdOperationLogListItem>>(
    "/ad-management/operation-logs",
    {
      params: {
        pageNumber: 1,
        pageSize: 20,
        ...params,
      },
    },
  );

  return data;
}

export async function getAdOperationLogById(id: string): Promise<AdOperationLogDetail> {
  const { data } = await apiClient.get<AdOperationLogDetail>(
    `/ad-management/operation-logs/${id}`,
  );

  return data;
}
