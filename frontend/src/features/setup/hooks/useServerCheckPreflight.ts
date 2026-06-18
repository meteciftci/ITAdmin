import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import { useTranslation } from "react-i18next";

import { getSetupPreflight } from "@/features/setup/api";
import { getApiErrorMessage } from "@/lib/api-error";

const SETUP_PREFLIGHT_QUERY_KEY = ["setup", "preflight"] as const;

export function useServerCheckPreflight(enabled: boolean) {
  const { t } = useTranslation(["setup"]);

  const query = useQuery({
    queryKey: SETUP_PREFLIGHT_QUERY_KEY,
    queryFn: getSetupPreflight,
    enabled,
  });

  const fallback = t("setup:steps.serverCheck.loadFailed");
  const errorMessage = query.error
    ? axios.isAxiosError(query.error)
      ? getApiErrorMessage(query.error, fallback)
      : fallback
    : null;

  return {
    preflight: query.data ?? null,
    isLoading: query.isFetching,
    errorMessage,
    reloadPreflight: () => void query.refetch(),
  };
}
