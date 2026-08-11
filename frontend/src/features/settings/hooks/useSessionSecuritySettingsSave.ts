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
  sessionSecuritySaveError: string | null;
  sessionSecuritySaveSucceeded: boolean;
  clearSessionSecuritySaveState: () => void;
};

export function useSessionSecuritySettingsSave({
  t,
  canUpdate,
}: UseSessionSecuritySettingsSaveParams): UseSessionSecuritySettingsSaveReturn {
  const queryClient = useQueryClient();

  const mutation = useMutation({
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
  });

  const saveSessionSecuritySettings = useCallback(
    (payload: SessionSecuritySettings) => {
      if (!canUpdate) return;
      mutation.reset();
      mutation.mutate(payload);
    },
    [canUpdate, mutation],
  );

  return {
    saveSessionSecuritySettings,
    isSavingSessionSecurity: mutation.isPending,
    sessionSecuritySaveError: mutation.isError
      ? getApiErrorMessage(
          mutation.error,
          t("settings:sessionSecurity.messages.saveFailed"),
        )
      : null,
    sessionSecuritySaveSucceeded: mutation.isSuccess,
    clearSessionSecuritySaveState: mutation.reset,
  };
}
