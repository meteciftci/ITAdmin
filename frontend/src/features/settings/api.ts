import { apiClient } from "@/lib/api-client";

import type {
  BrandingLogoUploadResponse,
  BrandingSettings,
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

export const getBrandingSettings = async (): Promise<BrandingSettings> => {
  const { data } = await apiClient.get<BrandingSettings>("/settings/branding");
  return data;
};

export const uploadBrandingLogo = async (
  file: File,
): Promise<BrandingLogoUploadResponse> => {
  const formData = new FormData();
  formData.append("file", file);

  const { data } = await apiClient.post<BrandingLogoUploadResponse>(
    "/settings/branding/logo",
    formData,
    {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    },
  );

  return data;
};
