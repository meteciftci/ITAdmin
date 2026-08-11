import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { toast } from "sonner";

import {
  updateApplicationSettings,
  uploadBrandingFavicon,
  uploadBrandingLogo,
} from "@/features/settings/api";
import { SETTINGS_QUERY_KEY } from "@/features/settings/settings-constants";
import type { UpdateApplicationSettingsRequest } from "@/features/settings/types";
import { BRANDING_QUERY_KEY } from "@/hooks/useBrandingSettings";
import { getApiErrorMessage } from "@/lib/api-error";
import { useState } from "react";

import type {
  BuildBrandingPayloadParams,
  UseBrandingSettingsFormNamespaces,
} from "./useBrandingSettingsForm";

export type UseBrandingSettingsSaveParams = {
  t: TFunction<UseBrandingSettingsFormNamespaces>;
  canUpdate: boolean;
  brandingLogoUrl: string | null;
  brandingFaviconUrl: string | null;
  logoFile: File | null;
  faviconFile: File | null;
  validateBrandingInput: () => boolean;
  validateForgotPasswordUrlInput: () => boolean;
  buildBrandingPayload: (params: BuildBrandingPayloadParams) => UpdateApplicationSettingsRequest;
  clearBrandingError: () => void;
  clearForgotPasswordUrlError: () => void;
  resetSelectedAssetsAfterSave: () => void;
};

export type UseBrandingSettingsSaveReturn = {
  saveBrandingSettings: () => Promise<void>;
  isSavingBranding: boolean;
  brandingSaveError: string | null;
  brandingSaveSucceeded: boolean;
  clearBrandingSaveState: () => void;
};

export function useBrandingSettingsSave({
  t,
  canUpdate,
  brandingLogoUrl,
  brandingFaviconUrl,
  logoFile,
  faviconFile,
  validateBrandingInput,
  validateForgotPasswordUrlInput,
  buildBrandingPayload,
  clearBrandingError,
  clearForgotPasswordUrlError,
  resetSelectedAssetsAfterSave,
}: UseBrandingSettingsSaveParams): UseBrandingSettingsSaveReturn {
  const queryClient = useQueryClient();
  const [brandingSaveError, setBrandingSaveError] = useState<string | null>(null);
  const [brandingSaveSucceeded, setBrandingSaveSucceeded] = useState(false);

  const clearBrandingSaveState = () => {
    setBrandingSaveError(null);
    setBrandingSaveSucceeded(false);
  };

  const updateBrandingMutation = useMutation({
    mutationFn: updateApplicationSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      await queryClient.invalidateQueries({ queryKey: BRANDING_QUERY_KEY });
      resetSelectedAssetsAfterSave();
      clearBrandingError();
      clearForgotPasswordUrlError();
      setBrandingSaveError(null);
      setBrandingSaveSucceeded(true);
      toast.success(t("settings:application.messages.saveSuccess"));
    },
    onError: (error: unknown) => {
      setBrandingSaveSucceeded(false);
      setBrandingSaveError(
        getApiErrorMessage(error, t("settings:application.messages.saveFailed")),
      );
    },
  });

  const saveBrandingSettings = async () => {
    if (!canUpdate) return;
    clearBrandingSaveState();
    clearBrandingError();
    if (!validateBrandingInput()) return;
    if (!validateForgotPasswordUrlInput()) return;

    let logoUrlToPersist = brandingLogoUrl;
    if (logoFile) {
      try {
        const uploadResult = await uploadBrandingLogo(logoFile);
        logoUrlToPersist = uploadResult.logoUrl;
      } catch (error: unknown) {
        setBrandingSaveError(
          getApiErrorMessage(error, t("settings:application.messages.logoUploadFailed")),
        );
        return;
      }
    }

    let faviconUrlToPersist = brandingFaviconUrl;
    if (faviconFile) {
      try {
        const uploadResult = await uploadBrandingFavicon(faviconFile);
        faviconUrlToPersist = uploadResult.faviconUrl;
      } catch (error: unknown) {
        setBrandingSaveError(
          getApiErrorMessage(error, t("settings:application.messages.faviconUploadFailed")),
        );
        return;
      }
    }

    updateBrandingMutation.mutate(
      buildBrandingPayload({ logoUrlToPersist, faviconUrlToPersist }),
    );
  };

  return {
    saveBrandingSettings,
    isSavingBranding: updateBrandingMutation.isPending,
    brandingSaveError,
    brandingSaveSucceeded,
    clearBrandingSaveState,
  };
}
