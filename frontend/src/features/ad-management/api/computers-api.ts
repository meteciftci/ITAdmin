import type { QueryClient } from "@tanstack/react-query";

import { AD_OPERATION_LOGS_QUERY_KEY } from "@/features/ad-management/operation-logs-api";
import { apiClient } from "@/lib/api-client";

import {
  AD_MANAGEMENT_COMPUTER_GROUPS_QUERY_KEY,
  AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
} from "./query-keys.ts";
import type {
  AdComputerAccountOperationResponse,
  AdComputerDetail,
  AdComputerGroupCandidateSearchResponse,
  AdComputerGroupMembershipResponse,
  AdComputerGroupMutationRequest,
  AdComputerGroupOperationResponse,
  AdComputerListResponse,
  AdComputerOperatingSystemOptionsResponse,
  AdOrganizationalUnitSearchResponse,
  DeleteAdComputerResponse,
  GetAdComputersParams,
  MoveAdComputerOuRequest,
  UpdateAdComputerRequest,
} from "@/features/ad-management/types";

export const getAdComputers = async (
  params: GetAdComputersParams,
): Promise<AdComputerListResponse> => {
  const { data } = await apiClient.get<AdComputerListResponse>("/ad-management/computers", {
    params: {
      search: params.search,
      status: params.status ?? "active",
      operatingSystem: params.operatingSystem?.trim() || undefined,
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 20,
    },
  });
  return data;
};

export const getAdComputerOperatingSystems = async (): Promise<AdComputerOperatingSystemOptionsResponse> => {
  const { data } = await apiClient.get<AdComputerOperatingSystemOptionsResponse>(
    "/ad-management/computer-operating-systems",
  );
  return data;
};

export const getAdComputerById = async (id: string): Promise<AdComputerDetail> => {
  const { data } = await apiClient.get<AdComputerDetail>(`/ad-management/computers/${id}`);
  return data;
};

export const enableAdComputer = async (
  computerId: string,
): Promise<AdComputerAccountOperationResponse> => {
  const { data } = await apiClient.post<AdComputerAccountOperationResponse>(
    `/ad-management/computers/${computerId}/enable`,
  );
  return data;
};

export const disableAdComputer = async (
  computerId: string,
): Promise<AdComputerAccountOperationResponse> => {
  const { data } = await apiClient.post<AdComputerAccountOperationResponse>(
    `/ad-management/computers/${computerId}/disable`,
  );
  return data;
};

export const searchComputerOrganizationalUnits = async (
  search?: string,
  pageSize = 50,
): Promise<AdOrganizationalUnitSearchResponse> => {
  const { data } = await apiClient.get<AdOrganizationalUnitSearchResponse>(
    "/ad-management/computer-organizational-units",
    {
      params: {
        search,
        pageSize,
      },
    },
  );
  return data;
};

export const updateAdComputer = async (
  computerId: string,
  payload: UpdateAdComputerRequest,
): Promise<AdComputerAccountOperationResponse> => {
  const { data } = await apiClient.put<AdComputerAccountOperationResponse>(
    `/ad-management/computers/${computerId}`,
    payload,
  );
  return data;
};

export const moveAdComputerOu = async (
  computerId: string,
  payload: MoveAdComputerOuRequest,
): Promise<AdComputerAccountOperationResponse> => {
  const { data } = await apiClient.post<AdComputerAccountOperationResponse>(
    `/ad-management/computers/${computerId}/move-ou`,
    payload,
  );
  return data;
};

export const deleteAdComputer = async (
  computerId: string,
): Promise<DeleteAdComputerResponse> => {
  const { data } = await apiClient.delete<DeleteAdComputerResponse>(
    `/ad-management/computers/${computerId}`,
  );
  return data;
};

export async function invalidateAdManagementComputerQueries(
  queryClient: QueryClient,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
  });
  await queryClient.invalidateQueries({
    queryKey: AD_OPERATION_LOGS_QUERY_KEY,
  });
}

export const getAdComputerGroups = async (
  computerId: string,
): Promise<AdComputerGroupMembershipResponse> => {
  const { data } = await apiClient.get<AdComputerGroupMembershipResponse>(
    `/ad-management/computers/${computerId}/groups`,
  );
  return data;
};

export const searchAdComputerGroupCandidates = async (
  computerId: string,
  query: string,
): Promise<AdComputerGroupCandidateSearchResponse> => {
  const { data } = await apiClient.get<AdComputerGroupCandidateSearchResponse>(
    `/ad-management/computers/${computerId}/group-candidates`,
    { params: { query } },
  );
  return data;
};

export const addAdComputerToGroup = async (
  computerId: string,
  payload: AdComputerGroupMutationRequest,
): Promise<AdComputerGroupOperationResponse> => {
  const { data } = await apiClient.post<AdComputerGroupOperationResponse>(
    `/ad-management/computers/${computerId}/groups`,
    payload,
  );
  return data;
};

export const removeAdComputerFromGroup = async (
  computerId: string,
  payload: AdComputerGroupMutationRequest,
): Promise<AdComputerGroupOperationResponse> => {
  const { data } = await apiClient.delete<AdComputerGroupOperationResponse>(
    `/ad-management/computers/${computerId}/groups`,
    { data: payload },
  );
  return data;
};

export async function invalidateAdComputerGroupsQuery(
  queryClient: QueryClient,
  computerId: string,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: [...AD_MANAGEMENT_COMPUTER_GROUPS_QUERY_KEY, computerId],
  });
  await invalidateAdManagementComputerQueries(queryClient);
}
