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
};

export const getSetupPreflight = async (): Promise<SetupPreflightResponse> => {
  const { data } = await apiClient.get<SetupPreflightResponse>("/setup/preflight");
  return data;
};

export type CompleteSetupLdapRequest = {
  name: string;
  host: string;
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
