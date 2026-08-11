import { apiClient } from "@/lib/api-client";

import type {
  PermissionCatalog,
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

type GetPermissionCatalogParams = Pick<GetPermissionsParams, "search" | "isActive">;

export const getPermissionCatalog = async (
  params: GetPermissionCatalogParams = {},
): Promise<PermissionCatalog> => {
  const firstPage = await getPermissions({ ...params, pageNumber: 1, pageSize: 100 });

  if (firstPage.totalPages <= 1) {
    return { items: firstPage.items, totalCount: firstPage.totalCount };
  }

  const remainingPages = await Promise.all(
    Array.from({ length: firstPage.totalPages - 1 }, (_, index) =>
      getPermissions({ ...params, pageNumber: index + 2, pageSize: 100 }),
    ),
  );

  return {
    items: [firstPage, ...remainingPages].flatMap((page) => page.items),
    totalCount: firstPage.totalCount,
  };
};
