import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { useCallback } from "react";
import { toast } from "sonner";

import { AUTH_SESSION_OPTIONS_QUERY_KEY } from "@/features/auth/query-keys";
import { updateSessionSecuritySettings } from "@/features/settings/api";
import { SETTINGS_QUERY_KEY } from "@/features/settings/settings-constants";
import type { SessionSecuritySettings } from "@/features/settings/types";
import { getApiErrorMessage } from "@/lib/api-error";

export type UseSessionSecuritySettingsSaveNamespaces = readonly ["settings", "common"];

export type UseSessionSecuritySettingsSaveParams = {
  t: TFunction<UseSessionSecuritySettingsSaveNamespaces>;
  canUpdate: boolean;
};

export type UseSessionSecuritySettingsSaveReturn = {
  saveSessionSecuritySettings: (payload: SessionSecuritySettings) => void;
  isSavingSessionSecurity: boolean;
};

export function useSessionSecuritySettingsSave({
  t,
  canUpdate,
}: UseSessionSecuritySettingsSaveParams): UseSessionSecuritySettingsSaveReturn {
  const queryClient = useQueryClient();

  const { mutate, isPending } = useMutation({
    mutationFn: updateSessionSecuritySettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      await queryClient.invalidateQueries({ queryKey: AUTH_SESSION_OPTIONS_QUERY_KEY });
      await queryClient.refetchQueries({
        queryKey: AUTH_SESSION_OPTIONS_QUERY_KEY,
        type: "active",
      });
      toast.success(t("settings:sessionSecurity.messages.saved"));
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("settings:sessionSecurity.messages.saveFailed")));
    },
  });

  const saveSessionSecuritySettings = useCallback(
    (payload: SessionSecuritySettings) => {
      if (!canUpdate) return;
      mutate(payload);
    },
    [canUpdate, mutate],
  );

  return {
    saveSessionSecuritySettings,
    isSavingSessionSecurity: isPending,
  };
}
