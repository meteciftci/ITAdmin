import { apiClient } from "@/lib/api-client";

import type {
  GetSecurityLogsParams,
  PagedResponse,
  SecurityLogFilterOptions,
  SecurityLogListItem,
} from "@/features/security-logs/types";

export const getSecurityLogs = async (
  params: GetSecurityLogsParams = { pageNumber: 1, pageSize: 20 },
): Promise<PagedResponse<SecurityLogListItem>> => {
  const mergedParams: GetSecurityLogsParams = {
    pageNumber: 1,
    pageSize: 20,
    ...params,
  };

  if (!mergedParams.eventTypes?.length) {
    mergedParams.eventTypes = undefined;
  }

  if (!mergedParams.severities?.length) {
    mergedParams.severities = undefined;
  }

  const { data } = await apiClient.get<PagedResponse<SecurityLogListItem>>("/security-logs", {
    params: mergedParams,
  });
  return data;
};

export const getSecurityLogFilterOptions = async (): Promise<SecurityLogFilterOptions> => {
  const { data } = await apiClient.get<SecurityLogFilterOptions>(
    "/security-logs/filter-options",
  );
  return data;
};
