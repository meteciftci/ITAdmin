import { apiClient } from "@/lib/api-client";

export type SetupStatusResponse = {
  isSetupRequired: boolean;
};

export type CompleteSetupLdapRequest = {
  name: string;
  host: string;
  port: number;
  useSsl: boolean;
  baseDn: string;
  userSearchBase: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain?: string | null;
  bindPassword: string;
  nationalIdAttribute?: string | null;
};

export type CompleteSetupAdminRequest = {
  userName: string;
  password: string;
};

export type CompleteSetupRequest = {
  setupKey: string;
  ldap: CompleteSetupLdapRequest;
  admin: CompleteSetupAdminRequest;
};

export type CompleteSetupResponse = {
  isCompleted: boolean;
  message: string;
};

export const getSetupStatus = async (): Promise<SetupStatusResponse> => {
  const { data } = await apiClient.get<SetupStatusResponse>("/setup/status");
  return data;
};

export const completeSetup = async (
  request: CompleteSetupRequest,
): Promise<CompleteSetupResponse> => {
  const { data } = await apiClient.post<CompleteSetupResponse>("/setup/complete", request);
  return data;
};
