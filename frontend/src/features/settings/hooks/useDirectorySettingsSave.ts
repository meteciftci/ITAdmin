import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { useCallback } from "react";
import { toast } from "sonner";

import { updateApplicationSettings } from "@/features/settings/api";
import { SETTINGS_QUERY_KEY } from "@/features/settings/settings-constants";
import type { UpdateApplicationSettingsRequest } from "@/features/settings/types";
import { getApiErrorMessage } from "@/lib/api-error";

export type UseDirectorySettingsSaveNamespaces = readonly ["settings", "common"];

export type UseDirectorySettingsSaveParams = {
  t: TFunction<UseDirectorySettingsSaveNamespaces>;
  canUpdate: boolean;
  buildDirectoryPayload: () => UpdateApplicationSettingsRequest;
  clearDirectoryError: () => void;
};

export type UseDirectorySettingsSaveReturn = {
  saveDirectorySettings: () => void;
  isSavingDirectory: boolean;
};

export function useDirectorySettingsSave({
  t,
  canUpdate,
  buildDirectoryPayload,
  clearDirectoryError,
}: UseDirectorySettingsSaveParams): UseDirectorySettingsSaveReturn {
  const queryClient = useQueryClient();

  const { mutate, isPending } = useMutation({
    mutationFn: updateApplicationSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      clearDirectoryError();
      toast.success(t("settings:directory.messages.saveSuccess"));
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("settings:directory.messages.saveFailed")));
    },
  });

  const saveDirectorySettings = useCallback(() => {
    if (!canUpdate) return;
    clearDirectoryError();
    mutate(buildDirectoryPayload());
  }, [canUpdate, clearDirectoryError, buildDirectoryPayload, mutate]);

  return {
    saveDirectorySettings,
    isSavingDirectory: isPending,
  };
}
