import { apiClient } from "@/lib/api-client";

export type SetupStatusResponse = {
  isSetupRequired: boolean;
};

export type SetupPreflightCheckResponse = {
  key: string;
  status: "ok" | "warning" | "error";
  messageKey: string;
  detail?: string | null;
};

export type SetupPreflightResponse = {
  checks: SetupPreflightCheckResponse[];
  canContinue: boolean;
};

export type CompleteSetupLdapRequest = {
  name: string;
  host: string;
  baseDn: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain?: string | null;
  bindPassword: string;
};

export type CompleteSetupAdManagementModuleRequest = {
  isEnabled: boolean;
  usersSearchBase?: string | null;
  groupsSearchBase?: string | null;
  computersSearchBase?: string | null;
  defaultUserOu?: string | null;
  defaultGroupOu?: string | null;
  defaultComputerOu?: string | null;
  deletedObjectsEnabled: boolean;
};

export type CompleteSetupModulesRequest = {
  adManagement?: CompleteSetupAdManagementModuleRequest | null;
};

export type CompleteSetupAdminUserRequest = {
  userName: string;
  distinguishedName?: string | null;
  directoryObjectId?: string | null;
};

export type CompleteSetupRequest = {
  setupKey: string;
  ldap: CompleteSetupLdapRequest;
  modules: CompleteSetupModulesRequest;
  adminUsers: CompleteSetupAdminUserRequest[];
};

export type CompleteSetupResponse = {
  isCompleted: boolean;
  message: string;
};

export type ValidateSetupLdapRequest = {
  setupKey: string;
  host: string;
  baseDn: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain?: string | null;
  bindPassword: string;
};

export type ValidateLdapResponse = {
  isValid: boolean;
  message: string;
};

export type SearchSetupAdminUsersRequest = {
  setupKey: string;
  ldap: CompleteSetupLdapRequest;
  search: string;
};

export type SetupAdminUserSearchResultResponse = {
  userName: string;
  displayName: string;
  email?: string | null;
  distinguishedName?: string | null;
  directoryObjectId?: string | null;
};

export type SearchSetupAdminUsersResponse = {
  users: SetupAdminUserSearchResultResponse[];
};

export type SearchSetupOrganizationalUnitsRequest = {
  setupKey: string;
  ldap: CompleteSetupLdapRequest;
  search?: string | null;
  parentDistinguishedName?: string | null;
};

export type SetupOrganizationalUnitListItemResponse = {
  distinguishedName: string;
  name?: string | null;
  displayName?: string | null;
  ou?: string | null;
  label: string;
};

export type SearchSetupOrganizationalUnitsResponse = {
  items: SetupOrganizationalUnitListItemResponse[];
  hasMore: boolean;
};

export const getSetupStatus = async (): Promise<SetupStatusResponse> => {
  const { data } = await apiClient.get<SetupStatusResponse>("/setup/status");
  return data;
};

export const getSetupPreflight = async (): Promise<SetupPreflightResponse> => {
  const { data } = await apiClient.get<SetupPreflightResponse>("/setup/preflight");
  return data;
};

export const validateSetupLdap = async (
  request: ValidateSetupLdapRequest,
): Promise<ValidateLdapResponse> => {
  const { data } = await apiClient.post<ValidateLdapResponse>("/setup/validate-ldap", request);
  return data;
};

export const searchSetupAdminUsers = async (
  request: SearchSetupAdminUsersRequest,
): Promise<SearchSetupAdminUsersResponse> => {
  const { data } = await apiClient.post<SearchSetupAdminUsersResponse>(
    "/setup/search-admin-users",
    request,
  );
  return data;
};

export const searchSetupOrganizationalUnits = async (
  request: SearchSetupOrganizationalUnitsRequest,
): Promise<SearchSetupOrganizationalUnitsResponse> => {
  const { data } = await apiClient.post<SearchSetupOrganizationalUnitsResponse>(
    "/setup/search-organizational-units",
    request,
  );
  return data;
};

export const completeSetup = async (
  request: CompleteSetupRequest,
): Promise<CompleteSetupResponse> => {
  const { data } = await apiClient.post<CompleteSetupResponse>("/setup/complete", request);
  return data;
};
