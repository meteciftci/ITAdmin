import type { QueryClient } from "@tanstack/react-query";

import { AD_OPERATION_LOGS_QUERY_KEY } from "@/features/ad-management/operation-logs-api";
import { apiClient } from "@/lib/api-client";

import {
  AD_MANAGEMENT_GROUP_MEMBERS_QUERY_KEY,
  AD_MANAGEMENT_GROUPS_QUERY_KEY,
} from "./query-keys.ts";
import type {
  AddAdGroupMemberRequest,
  AdGroupDetail,
  AdGroupListResponse,
  AdGroupMemberCandidatesResponse,
  AdGroupMemberOperationResponse,
  AdGroupMembersListResponse,
  AdOrganizationalUnitSearchResponse,
  CreateAdGroupRequest,
  DeleteAdGroupResponse,
  GetAdGroupMemberCandidatesParams,
  GetAdGroupMembersParams,
  GetAdGroupsParams,
  MoveAdGroupOuRequest,
  MoveAdGroupOuResponse,
  RemoveAdGroupMemberRequest,
  UpdateAdGroupRequest,
} from "@/features/ad-management/types";

export const getAdGroups = async (
  params: GetAdGroupsParams,
): Promise<AdGroupListResponse> => {
  const { data } = await apiClient.get<AdGroupListResponse>("/ad-management/groups", {
    params: {
      search: params.search,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    },
  });
  return data;
};

export const getAdGroupById = async (id: string): Promise<AdGroupDetail> => {
  const { data } = await apiClient.get<AdGroupDetail>(`/ad-management/groups/${id}`);
  return data;
};

export const createAdGroup = async (
  payload: CreateAdGroupRequest,
): Promise<AdGroupDetail> => {
  const { data } = await apiClient.post<AdGroupDetail>("/ad-management/groups", payload);
  return data;
};

export const updateAdGroup = async (
  groupId: string,
  payload: UpdateAdGroupRequest,
): Promise<AdGroupDetail> => {
  const { data } = await apiClient.put<AdGroupDetail>(
    `/ad-management/groups/${groupId}`,
    payload,
  );
  return data;
};

export const deleteAdGroup = async (groupId: string): Promise<DeleteAdGroupResponse> => {
  const { data } = await apiClient.delete<DeleteAdGroupResponse>(
    `/ad-management/groups/${groupId}`,
  );
  return data;
};

export const moveAdGroupOu = async (
  groupId: string,
  payload: MoveAdGroupOuRequest,
): Promise<MoveAdGroupOuResponse> => {
  const { data } = await apiClient.post<MoveAdGroupOuResponse>(
    `/ad-management/groups/${groupId}/move-ou`,
    payload,
  );
  return data;
};

export const searchGroupOrganizationalUnits = async (params: {
  search?: string;
  pageSize?: number;
}): Promise<AdOrganizationalUnitSearchResponse> => {
  const { data } = await apiClient.get<AdOrganizationalUnitSearchResponse>(
    "/ad-management/group-organizational-units",
    {
      params: {
        search: params.search,
        pageSize: params.pageSize ?? 50,
      },
    },
  );
  return data;
};

export async function invalidateAdManagementGroupQueries(
  queryClient: QueryClient,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: AD_MANAGEMENT_GROUPS_QUERY_KEY,
  });
}

export async function invalidateAdGroupOuMoveQueries(
  queryClient: QueryClient,
): Promise<void> {
  await invalidateAdManagementGroupQueries(queryClient);
  await queryClient.invalidateQueries({
    queryKey: AD_OPERATION_LOGS_QUERY_KEY,
  });
}

export const getAdGroupMembers = async (
  groupId: string,
  params: GetAdGroupMembersParams,
): Promise<AdGroupMembersListResponse> => {
  const { data } = await apiClient.get<AdGroupMembersListResponse>(
    `/ad-management/groups/${groupId}/members`,
    {
      params: {
        search: params.search,
        type: params.type ?? "all",
        pageNumber: params.pageNumber ?? 1,
        pageSize: params.pageSize ?? 20,
      },
    },
  );
  return data;
};

export const searchAdGroupMemberCandidates = async (
  groupId: string,
  params: GetAdGroupMemberCandidatesParams,
): Promise<AdGroupMemberCandidatesResponse> => {
  const { data } = await apiClient.get<AdGroupMemberCandidatesResponse>(
    `/ad-management/groups/${groupId}/member-candidates`,
    {
      params: {
        search: params.search,
        types: params.types?.join(",") ?? "user,group,computer",
        pageSize: params.pageSize ?? 50,
      },
    },
  );
  return data;
};

export const addAdGroupMember = async (
  groupId: string,
  payload: AddAdGroupMemberRequest,
): Promise<AdGroupMemberOperationResponse> => {
  const { data } = await apiClient.post<AdGroupMemberOperationResponse>(
    `/ad-management/groups/${groupId}/members`,
    payload,
  );
  return data;
};

export const removeAdGroupMember = async (
  groupId: string,
  payload: RemoveAdGroupMemberRequest,
): Promise<AdGroupMemberOperationResponse> => {
  const { data } = await apiClient.delete<AdGroupMemberOperationResponse>(
    `/ad-management/groups/${groupId}/members`,
    { data: payload },
  );
  return data;
};

export async function invalidateAdGroupMemberQueries(
  queryClient: QueryClient,
  groupId: string,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: [...AD_MANAGEMENT_GROUP_MEMBERS_QUERY_KEY, groupId],
  });
  await invalidateAdManagementGroupQueries(queryClient);
  await queryClient.invalidateQueries({
    queryKey: AD_OPERATION_LOGS_QUERY_KEY,
  });
}
