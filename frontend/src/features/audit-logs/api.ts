import { apiClient } from "@/lib/api-client";

import type {
  AuditLogListItem,
  GetAuditLogsParams,
  PagedResponse,
} from "@/features/audit-logs/types";

export const getAuditLogs = async (
  params: GetAuditLogsParams = { pageNumber: 1, pageSize: 20 },
): Promise<PagedResponse<AuditLogListItem>> => {
  const mergedParams: GetAuditLogsParams = {
    pageNumber: 1,
    pageSize: 20,
    ...params,
  };

  const { data } = await apiClient.get<PagedResponse<AuditLogListItem>>(
    "/audit-logs",
    { params: mergedParams },
  );

  return data;
};
