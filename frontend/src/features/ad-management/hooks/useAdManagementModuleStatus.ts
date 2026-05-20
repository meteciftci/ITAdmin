import { useQuery } from "@tanstack/react-query";

import {
  AD_MANAGEMENT_SETTINGS_QUERY_KEY,
  getAdManagementSettings,
} from "@/features/ad-management/api";
import type { AdManagementSettings } from "@/features/ad-management/types";

export type AdManagementModuleStatus = {
  isLoading: boolean;
  isError: boolean;
  isConfigured: boolean;
  isEnabled: boolean;
  isOperational: boolean;
  settings: AdManagementSettings | undefined;
};

export function resolveAdManagementModuleStatus(
  settings: AdManagementSettings | undefined,
  queryState: { isLoading: boolean; isError: boolean },
): AdManagementModuleStatus {
  const isConfigured = settings?.isConfigured ?? false;
  const isEnabled = settings?.isEnabled ?? false;

  return {
    isLoading: queryState.isLoading,
    isError: queryState.isError,
    isConfigured,
    isEnabled,
    isOperational: isConfigured && isEnabled,
    settings,
  };
}

export function useAdManagementModuleStatus(): AdManagementModuleStatus {
  const settingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getAdManagementSettings,
    staleTime: 60_000,
  });

  return resolveAdManagementModuleStatus(settingsQuery.data, {
    isLoading: settingsQuery.isLoading,
    isError: settingsQuery.isError,
  });
}
