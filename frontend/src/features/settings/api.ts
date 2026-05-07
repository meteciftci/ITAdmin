import { apiClient } from "@/lib/api-client";

import type {
  SettingsOverview,
  UpdateApplicationSettingsRequest,
  UpdateLdapSettingsRequest,
  ValidateLdapSettingsRequest,
  ValidateLdapSettingsResponse,
} from "@/features/settings/types";

export const getSettings = async (): Promise<SettingsOverview> => {
  const { data } = await apiClient.get<SettingsOverview>("/settings");
  return data;
};

export const updateLdapSettings = async (
  payload: UpdateLdapSettingsRequest,
): Promise<void> => {
  await apiClient.put("/settings/ldap", payload);
};

export const validateLdapSettings = async (
  payload: ValidateLdapSettingsRequest,
): Promise<ValidateLdapSettingsResponse> => {
  const { data } = await apiClient.post<ValidateLdapSettingsResponse>(
    "/settings/ldap/validate",
    payload,
  );
  return data;
};

export const updateApplicationSettings = async (
  payload: UpdateApplicationSettingsRequest,
): Promise<void> => {
  await apiClient.put("/settings/application", payload);
};
