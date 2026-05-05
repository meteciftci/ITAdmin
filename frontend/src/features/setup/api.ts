import { apiClient } from "@/lib/api-client";

export type SetupStatusResponse = {
  isSetupRequired: boolean;
};

export const getSetupStatus = async (): Promise<SetupStatusResponse> => {
  const { data } = await apiClient.get<SetupStatusResponse>("/setup/status");
  return data;
};
