import type { QueryClient } from "@tanstack/react-query";

import { AD_OPERATION_LOGS_QUERY_KEY } from "@/features/ad-management/operation-logs-api";
import { apiClient } from "@/lib/api-client";

import { AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY } from "./query-keys.ts";
import type {
  AdOrganizationalUnitDetail,
  AdOrganizationalUnitManageListResponse,
  CreateAdOrganizationalUnitRequest,
  CreateAdOrganizationalUnitResponse,
  DeleteAdOrganizationalUnitResponse,
  GetAdOrganizationalUnitsParams,
  MoveAdOrganizationalUnitRequest,
  MoveAdOrganizationalUnitResponse,
  RenameAdOrganizationalUnitRequest,
  RenameAdOrganizationalUnitResponse,
} from "@/features/ad-management/types";

export async function invalidateAdOrganizationalUnitQueries(
  queryClient: QueryClient,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY,
  });
  await queryClient.invalidateQueries({
    queryKey: AD_OPERATION_LOGS_QUERY_KEY,
  });
}

export const getAdOrganizationalUnits = async (
  params: GetAdOrganizationalUnitsParams,
): Promise<AdOrganizationalUnitManageListResponse> => {
  const { data } = await apiClient.get<AdOrganizationalUnitManageListResponse>(
    "/ad-management/organizational-units/manage",
    { params },
  );
  return data;
};

export const getAdOrganizationalUnitById = async (
  organizationalUnitId: string,
): Promise<AdOrganizationalUnitDetail> => {
  const { data } = await apiClient.get<AdOrganizationalUnitDetail>(
    `/ad-management/organizational-units/${organizationalUnitId}`,
  );
  return data;
};

export const createAdOrganizationalUnit = async (
  payload: CreateAdOrganizationalUnitRequest,
): Promise<CreateAdOrganizationalUnitResponse> => {
  const { data } = await apiClient.post<CreateAdOrganizationalUnitResponse>(
    "/ad-management/organizational-units",
    payload,
  );
  return data;
};

export const renameAdOrganizationalUnit = async (
  organizationalUnitId: string,
  payload: RenameAdOrganizationalUnitRequest,
): Promise<RenameAdOrganizationalUnitResponse> => {
  const { data } = await apiClient.put<RenameAdOrganizationalUnitResponse>(
    `/ad-management/organizational-units/${organizationalUnitId}/rename`,
    payload,
  );
  return data;
};

export const moveAdOrganizationalUnit = async (
  organizationalUnitId: string,
  payload: MoveAdOrganizationalUnitRequest,
): Promise<MoveAdOrganizationalUnitResponse> => {
  const { data } = await apiClient.post<MoveAdOrganizationalUnitResponse>(
    `/ad-management/organizational-units/${organizationalUnitId}/move`,
    payload,
  );
  return data;
};

export const deleteAdOrganizationalUnit = async (
  organizationalUnitId: string,
): Promise<DeleteAdOrganizationalUnitResponse> => {
  const { data } = await apiClient.delete<DeleteAdOrganizationalUnitResponse>(
    `/ad-management/organizational-units/${organizationalUnitId}`,
  );
  return data;
};
