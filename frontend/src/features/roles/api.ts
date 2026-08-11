import { apiClient } from "@/lib/api-client";

import type {
  CreateRoleRequest,
  PagedResponse,
  RoleDetail,
  RoleListItem,
  UpdateRolePermissionsRequest,
  UpdateRoleRequest,
  UpdateRoleStatusRequest,
} from "@/features/roles/types";

type GetRolesParams = {
  search?: string;
  isActive?: boolean;
  isSystem?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

export const getRoles = async (
  params: GetRolesParams,
): Promise<PagedResponse<RoleListItem>> => {
  const { data } = await apiClient.get<PagedResponse<RoleListItem>>("/roles", {
    params,
  });
  return data;
};

export const getRoleById = async (id: string): Promise<RoleDetail> => {
  const { data } = await apiClient.get<RoleDetail>(`/roles/${id}`);
  return data;
};

export const createRole = async (request: CreateRoleRequest): Promise<void> => {
  await apiClient.post("/roles", request);
};

export const updateRole = async (
  id: string,
  request: UpdateRoleRequest,
): Promise<void> => {
  await apiClient.put(`/roles/${id}`, request);
};

export const updateRoleStatus = async (
  id: string,
  request: UpdateRoleStatusRequest,
): Promise<void> => {
  await apiClient.patch(`/roles/${id}/status`, request);
};

export const updateRolePermissions = async (
  id: string,
  request: UpdateRolePermissionsRequest,
): Promise<void> => {
  await apiClient.put(`/roles/${id}/permissions`, request);
};
