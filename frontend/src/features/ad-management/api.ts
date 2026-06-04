import type { QueryClient } from "@tanstack/react-query";

import { defaultAdManagementNotificationSettings } from "@/features/ad-management/ad-management-settings-payload";
import { AD_OPERATION_LOGS_QUERY_KEY } from "@/features/ad-management/operation-logs-api";
import { apiClient } from "@/lib/api-client";

import type {
  AdAttributeMapping,
  AdManagementSettings,
  AdOrganizationalUnitSearchResponse,
  AdUpnSuffixesResponse,
  AdUserDetail,
  AdUserSearchResponse,
  CreateAdAttributeMappingRequest,
  AdUserAccountOperationResponse,
  AdUserGroupMembershipResponse,
  AdUserEffectiveGroupsResponse,
  AdUserGroupMutationRequest,
  AdUserGroupOperationResponse,
  MoveAdUserOuRequest,
  MoveAdUserOuResponse,
  AdGroupSearchResponse,
  CreateAdUserRequest,
  CreateAdUserResponse,
  GetAdUsersParams,
  UpdateAdAttributeMappingRequest,
  UpdateAdManagementSettingsRequest,
  UpdateAdUserRequest,
  UpdateAdUserManagerRequest,
  UpdateAdUserManagerResponse,
  UpdateAdUserAccountExpirationRequest,
  UpdateAdUserAccountExpirationResponse,
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

export const AD_MANAGEMENT_USER_GROUPS_QUERY_KEY = [
  "ad-management",
  "users",
  "groups",
] as const;

export const AD_MANAGEMENT_USER_EFFECTIVE_GROUPS_QUERY_KEY = [
  "ad-management",
  "users",
  "effective-groups",
] as const;

export async function invalidateAdManagementUserQueries(
  queryClient: QueryClient,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: AD_MANAGEMENT_USERS_QUERY_KEY,
  });
}

export async function invalidateAdUserDetailRelatedQueries(
  queryClient: QueryClient,
  userId: string,
): Promise<void> {
  await invalidateAdManagementUserQueries(queryClient);
  await queryClient.invalidateQueries({
    queryKey: [...AD_OPERATION_LOGS_QUERY_KEY, "recent", userId],
  });
  await queryClient.invalidateQueries({
    queryKey: AD_OPERATION_LOGS_QUERY_KEY,
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

export const updateAdUser = async (
  userId: string,
  payload: UpdateAdUserRequest,
): Promise<AdUserDetail> => {
  const { data } = await apiClient.put<AdUserDetail>(
    `/ad-management/users/${userId}`,
    payload,
  );
  return data;
};

export const updateAdUserManager = async (
  userId: string,
  payload: UpdateAdUserManagerRequest,
): Promise<UpdateAdUserManagerResponse> => {
  const { data } = await apiClient.put<UpdateAdUserManagerResponse>(
    `/ad-management/users/${userId}/manager`,
    payload,
  );
  return data;
};

export const updateAdUserAccountExpiration = async (
  userId: string,
  payload: UpdateAdUserAccountExpirationRequest,
): Promise<UpdateAdUserAccountExpirationResponse> => {
  const { data } = await apiClient.put<UpdateAdUserAccountExpirationResponse>(
    `/ad-management/users/${userId}/account-expiration`,
    payload,
  );
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

export const enableAdUser = async (
  userId: string,
): Promise<AdUserAccountOperationResponse> => {
  const { data } = await apiClient.post<AdUserAccountOperationResponse>(
    `/ad-management/users/${userId}/enable`,
  );
  return data;
};

export const disableAdUser = async (
  userId: string,
): Promise<AdUserAccountOperationResponse> => {
  const { data } = await apiClient.post<AdUserAccountOperationResponse>(
    `/ad-management/users/${userId}/disable`,
  );
  return data;
};

export const unlockAdUser = async (
  userId: string,
): Promise<AdUserAccountOperationResponse> => {
  const { data } = await apiClient.post<AdUserAccountOperationResponse>(
    `/ad-management/users/${userId}/unlock`,
  );
  return data;
};

export const moveAdUserOu = async (
  userId: string,
  payload: MoveAdUserOuRequest,
): Promise<MoveAdUserOuResponse> => {
  const { data } = await apiClient.post<MoveAdUserOuResponse>(
    `/ad-management/users/${userId}/move-ou`,
    payload,
  );
  return data;
};

export const getAdUserGroups = async (
  userId: string,
): Promise<AdUserGroupMembershipResponse> => {
  const { data } = await apiClient.get<AdUserGroupMembershipResponse>(
    `/ad-management/users/${userId}/groups`,
  );
  return data;
};

export const getAdUserEffectiveGroups = async (
  userId: string,
  options?: { maxDepth?: number },
): Promise<AdUserEffectiveGroupsResponse> => {
  const { data } = await apiClient.get<AdUserEffectiveGroupsResponse>(
    `/ad-management/users/${userId}/effective-groups`,
    {
      params: {
        maxDepth: options?.maxDepth,
      },
    },
  );
  return data;
};

export const searchAdGroups = async (query: string): Promise<AdGroupSearchResponse> => {
  const { data } = await apiClient.get<AdGroupSearchResponse>("/ad-management/groups/search", {
    params: { query },
  });
  return data;
};

export const addAdUserToGroup = async (
  userId: string,
  payload: AdUserGroupMutationRequest,
): Promise<AdUserGroupOperationResponse> => {
  const { data } = await apiClient.post<AdUserGroupOperationResponse>(
    `/ad-management/users/${userId}/groups`,
    payload,
  );
  return data;
};

export const removeAdUserFromGroup = async (
  userId: string,
  payload: AdUserGroupMutationRequest,
): Promise<AdUserGroupOperationResponse> => {
  const { data } = await apiClient.delete<AdUserGroupOperationResponse>(
    `/ad-management/users/${userId}/groups`,
    { data: payload },
  );
  return data;
};

export async function invalidateAdUserGroupsQuery(
  queryClient: QueryClient,
  userId: string,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: [...AD_MANAGEMENT_USER_GROUPS_QUERY_KEY, userId],
  });
  await invalidateAdManagementUserQueries(queryClient);
}
