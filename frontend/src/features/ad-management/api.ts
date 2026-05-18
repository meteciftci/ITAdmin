import type { QueryClient } from "@tanstack/react-query";

import { apiClient } from "@/lib/api-client";

import type {
  AdAttributeMapping,
  AdManagementSettings,
  AdUserDetail,
  AdUserSearchResponse,
  CreateAdAttributeMappingRequest,
  GetAdUsersParams,
  UpdateAdAttributeMappingRequest,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";

export const AD_MANAGEMENT_SETTINGS_QUERY_KEY = [
  "ad-management",
  "settings",
] as const;

export const AD_MANAGEMENT_MAPPINGS_QUERY_KEY = [
  "ad-management",
  "attribute-mappings",
] as const;

export const AD_MANAGEMENT_USERS_QUERY_KEY = ["ad-management", "users"] as const;

export async function invalidateAdManagementUserQueries(
  queryClient: QueryClient,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: AD_MANAGEMENT_USERS_QUERY_KEY,
  });
}

export const getAdManagementSettings = async (): Promise<AdManagementSettings> => {
  const { data } = await apiClient.get<AdManagementSettings>(
    "/ad-management/settings",
  );
  return data;
};

export const updateAdManagementSettings = async (
  payload: UpdateAdManagementSettingsRequest,
): Promise<AdManagementSettings> => {
  const { data } = await apiClient.put<AdManagementSettings>(
    "/ad-management/settings",
    payload,
  );
  return data;
};

export const getAdAttributeMappings = async (): Promise<AdAttributeMapping[]> => {
  const { data } = await apiClient.get<AdAttributeMapping[]>(
    "/ad-management/attribute-mappings",
  );
  return data;
};

export const createAdAttributeMapping = async (
  payload: CreateAdAttributeMappingRequest,
): Promise<AdAttributeMapping> => {
  const { data } = await apiClient.post<AdAttributeMapping>(
    "/ad-management/attribute-mappings",
    payload,
  );
  return data;
};

export const updateAdAttributeMapping = async (
  id: string,
  payload: UpdateAdAttributeMappingRequest,
): Promise<AdAttributeMapping> => {
  const { data } = await apiClient.put<AdAttributeMapping>(
    `/ad-management/attribute-mappings/${id}`,
    payload,
  );
  return data;
};

export const deleteAdAttributeMapping = async (id: string): Promise<void> => {
  await apiClient.delete(`/ad-management/attribute-mappings/${id}`);
};

export const getAdUsers = async (
  params: GetAdUsersParams,
): Promise<AdUserSearchResponse> => {
  const { data } = await apiClient.get<AdUserSearchResponse>("/ad-management/users", {
    params: {
      search: params.search,
      status: params.status ?? "active",
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 20,
    },
  });
  return data;
};

export const getAdUserById = async (id: string): Promise<AdUserDetail> => {
  const { data } = await apiClient.get<AdUserDetail>(`/ad-management/users/${id}`);
  return data;
};
