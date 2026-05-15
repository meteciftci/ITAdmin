import { apiClient } from "@/lib/api-client";

import type {
  AdAttributeMapping,
  AdManagementSettings,
  AdManagementValidationResult,
  CreateAdAttributeMappingRequest,
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

export const validateAdManagementSettings =
  async (): Promise<AdManagementValidationResult> => {
    const { data } = await apiClient.post<AdManagementValidationResult>(
      "/ad-management/settings/validate",
      {},
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
