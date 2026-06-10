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
  MoveAdGroupOuRequest,
  MoveAdGroupOuResponse,
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
  AdGroupDetail,
  AdGroupListResponse,
  CreateAdGroupRequest,
  DeleteAdGroupResponse,
  GetAdGroupsParams,
  UpdateAdGroupRequest,
  AdGroupMembersListResponse,
  GetAdGroupMembersParams,
  AdGroupMemberCandidatesResponse,
  GetAdGroupMemberCandidatesParams,
  AddAdGroupMemberRequest,
  RemoveAdGroupMemberRequest,
  AdGroupMemberOperationResponse,
  AdComputerDetail,
  AdComputerListResponse,
  GetAdComputersParams,
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

export const AD_MANAGEMENT_GROUPS_QUERY_KEY = ["ad-management", "groups"] as const;

export const AD_MANAGEMENT_COMPUTERS_QUERY_KEY = ["ad-management", "computers"] as const;

export const AD_MANAGEMENT_GROUP_MEMBERS_QUERY_KEY = [
  "ad-management",
  "groups",
  "members",
] as const;

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

export const getAdComputers = async (
  params: GetAdComputersParams,
): Promise<AdComputerListResponse> => {
  const { data } = await apiClient.get<AdComputerListResponse>("/ad-management/computers", {
    params: {
      search: params.search,
      status: params.status ?? "active",
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 20,
    },
  });
  return data;
};

export const getAdComputerById = async (id: string): Promise<AdComputerDetail> => {
  const { data } = await apiClient.get<AdComputerDetail>(`/ad-management/computers/${id}`);
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

export async function invalidateAdManagementComputerQueries(
  queryClient: QueryClient,
): Promise<void> {
  await queryClient.invalidateQueries({
    queryKey: AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
  });
}

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
