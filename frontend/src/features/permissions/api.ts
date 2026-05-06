import { apiClient } from "@/lib/api-client";

import type {
  PagedResponse,
  PermissionListItem,
} from "@/features/permissions/types";

type GetPermissionsParams = {
  search?: string;
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

export const getPermissions = async (
  params: GetPermissionsParams = { pageNumber: 1, pageSize: 100 },
): Promise<PagedResponse<PermissionListItem>> => {
  const mergedParams: GetPermissionsParams = {
    pageNumber: 1,
    pageSize: 100,
    ...params,
  };

  const { data } = await apiClient.get<PagedResponse<PermissionListItem>>(
    "/permissions",
    { params: mergedParams },
  );

  return data;
};
