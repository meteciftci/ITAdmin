import { apiClient } from "@/lib/api-client";

import type {
  CreateUserRequest,
  DirectoryLookupResponse,
  PagedResponse,
  RoleListItem,
  UpdateUserRolesRequest,
  UpdateUserStatusRequest,
  UserDetail,
  UserListItem,
} from "@/features/users/types";

type GetUsersParams = {
  search?: string;
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

type LookupDirectoryUsersParams = {
  search: string;
  maxResults?: number;
};

type GetRolesParams = {
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

export const getUsers = async (
  params: GetUsersParams,
): Promise<PagedResponse<UserListItem>> => {
  const { data } = await apiClient.get<PagedResponse<UserListItem>>("/users", {
    params,
  });
  return data;
};

export const getUserById = async (id: string): Promise<UserDetail> => {
  const { data } = await apiClient.get<UserDetail>(`/users/${id}`);
  return data;
};

export const lookupDirectoryUsers = async (
  params: LookupDirectoryUsersParams,
): Promise<DirectoryLookupResponse> => {
  const { data } = await apiClient.get<DirectoryLookupResponse>(
    "/users/lookup-directory",
    { params },
  );
  return data;
};

export const createUser = async (request: CreateUserRequest): Promise<void> => {
  await apiClient.post("/users", request);
};

export const updateUserStatus = async (
  id: string,
  request: UpdateUserStatusRequest,
): Promise<void> => {
  await apiClient.patch(`/users/${id}/status`, request);
};

export const updateUserRoles = async (
  id: string,
  request: UpdateUserRolesRequest,
): Promise<void> => {
  await apiClient.put(`/users/${id}/roles`, request);
};

export const getRoles = async (
  params: GetRolesParams = { isActive: true, pageSize: 100 },
): Promise<PagedResponse<RoleListItem>> => {
  const mergedParams: GetRolesParams = {
    pageNumber: 1,
    pageSize: 100,
    isActive: true,
    ...params,
  };

  const { data } = await apiClient.get<PagedResponse<RoleListItem>>("/roles", {
    params: mergedParams,
  });
  return data;
};
