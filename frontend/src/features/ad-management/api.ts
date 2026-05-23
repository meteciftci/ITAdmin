import type { QueryClient } from "@tanstack/react-query";

import { defaultAdManagementNotificationSettings } from "@/features/ad-management/ad-management-settings-payload";
import { apiClient } from "@/lib/api-client";

import type {
  AdAttributeMapping,
  AdManagementSettings,
  AdOrganizationalUnitSearchResponse,
  AdUpnSuffixesResponse,
  AdUserDetail,
  AdUserSearchResponse,
  CreateAdAttributeMappingRequest,
  CreateAdUserRequest,
  CreateAdUserResponse,
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

export const AD_UPN_SUFFIXES_QUERY_KEY = ["ad-management", "upn-suffixes"] as const;

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
  return {
    ...data,
    notificationSettings:
      data.notificationSettings ?? defaultAdManagementNotificationSettings(),
  };
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

export const searchOrganizationalUnits = async (params: {
  search?: string;
  pageSize?: number;
}): Promise<AdOrganizationalUnitSearchResponse> => {
  const { data } = await apiClient.get<AdOrganizationalUnitSearchResponse>(
    "/ad-management/organizational-units",
    {
      params: {
        search: params.search,
        pageSize: params.pageSize ?? 50,
      },
    },
  );
  return data;
};

export const getAdUpnSuffixes = async (): Promise<AdUpnSuffixesResponse> => {
  const { data } = await apiClient.get<AdUpnSuffixesResponse>("/ad-management/upn-suffixes");
  return data;
};

export const createAdUser = async (
  payload: CreateAdUserRequest,
): Promise<CreateAdUserResponse> => {
  const { data } = await apiClient.post<CreateAdUserResponse>(
    "/ad-management/users",
    payload,
  );
  return data;
};
