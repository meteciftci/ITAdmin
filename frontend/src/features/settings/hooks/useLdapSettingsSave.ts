import { useCallback, useMemo } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { toast } from "sonner";

import { updateLdapSettings, validateLdapSettings } from "@/features/settings/api";
import { SETTINGS_QUERY_KEY } from "@/features/settings/settings-constants";
import type {
  UpdateLdapSettingsRequest,
  ValidateLdapSettingsRequest,
} from "@/features/settings/types";
import { getApiErrorMessage } from "@/lib/api-error";

import type { UseLdapSettingsFormNamespaces } from "./useLdapSettingsForm";

export type UseLdapSettingsSaveParams = {
  t: TFunction<UseLdapSettingsFormNamespaces>;
  canUpdate: boolean;
  ldapFormIsMinimumValid: boolean;
  validateLdapForm: () => boolean;
  buildLdapPayload: () => UpdateLdapSettingsRequest;
  buildLdapValidatePayload: () => ValidateLdapSettingsRequest;
  clearBindPasswordAfterSave: () => void;
};

export type UseLdapSettingsSaveReturn = {
  saveLdapSettings: () => Promise<void>;
  canSaveLdap: boolean;
  isSavingLdap: boolean;
};

function getLocalizedLdapValidationMessage(
  t: TFunction<UseLdapSettingsFormNamespaces>,
  message?: string | null,
): string {
  const trimmedMessage = message?.trim();
  if (!trimmedMessage) {
    return t("settings:ldap.validation.genericCouldNotValidate");
  }

  const messageKeyMap: Record<string, string> = {
    "LDAP service account authentication failed.":
      "settings:ldap.validation.backendMessages.serviceAccountAuthFailed",
    "LDAP server connection failed.":
      "settings:ldap.validation.backendMessages.serverConnectionFailed",
    "LDAP validation failed.": "settings:ldap.validation.backendMessages.validationFailed",
    "Required LDAP fields are missing.":
      "settings:ldap.validation.backendMessages.requiredLdapFieldsMissing",
    "Required LDAP validation fields are missing.":
      "settings:ldap.validation.backendMessages.requiredLdapValidationFieldsMissing",
    "LDAP port must be between 1 and 65535.":
      "settings:ldap.validation.backendMessages.portRange",
    "Bind password is required when no active LDAP setting exists.":
      "settings:ldap.validation.backendMessages.bindPasswordRequiredWithoutActive",
    "LDAP validation could not be completed.":
      "settings:ldap.validation.backendMessages.validationCouldNotComplete",
    "Directory user could not be found.":
      "settings:ldap.validation.backendMessages.directoryUserNotFound",
    "Directory user authentication failed.":
      "settings:ldap.validation.backendMessages.directoryUserAuthFailed",
    "Directory user distinguished name could not be resolved.":
      "settings:ldap.validation.backendMessages.directoryUserDnNotResolved",
    "LDAP bind validation succeeded.":
      "settings:ldap.validation.backendMessages.bindValidationSucceeded",
    "LDAP base DN could not be resolved.":
      "settings:ldap.validation.backendMessages.baseDnCouldNotBeResolved",
    "LDAP user search base could not be resolved.":
      "settings:ldap.validation.backendMessages.userSearchBaseCouldNotBeResolved",
  };

  const mappedKey = messageKeyMap[trimmedMessage];
  if (!mappedKey) {
    return t("settings:ldap.validation.genericCouldNotValidate");
  }

  return t(mappedKey);
}

export function useLdapSettingsSave({
  t,
  canUpdate,
  ldapFormIsMinimumValid,
  validateLdapForm,
  buildLdapPayload,
  buildLdapValidatePayload,
  clearBindPasswordAfterSave,
}: UseLdapSettingsSaveParams): UseLdapSettingsSaveReturn {
  const queryClient = useQueryClient();

  const updateLdapMutation = useMutation({
    mutationFn: updateLdapSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      clearBindPasswordAfterSave();
      toast.success(t("settings:ldap.messages.saveSuccess"));
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("settings:ldap.messages.saveFailed")));
    },
  });

  const validateLdapMutation = useMutation({
    mutationFn: validateLdapSettings,
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("settings:ldap.validation.requestFailed")));
    },
  });

  const canSaveLdap = useMemo(
    () =>
      canUpdate &&
      ldapFormIsMinimumValid &&
      !updateLdapMutation.isPending &&
      !validateLdapMutation.isPending,
    [canUpdate, ldapFormIsMinimumValid, updateLdapMutation.isPending, validateLdapMutation.isPending],
  );

  const saveLdapSettings = useCallback(async () => {
    if (!canUpdate) return;
    if (!validateLdapForm()) return;

    const payload = buildLdapPayload();
    const validatePayload = buildLdapValidatePayload();
    let validateResult;
    try {
      validateResult = await validateLdapMutation.mutateAsync(validatePayload);
    } catch {
      return;
    }

    if (!validateResult.isValid) {
      const detailMessage = getLocalizedLdapValidationMessage(t, validateResult.message);
      toast.error(t("settings:ldap.validation.saveBlockedByValidation"), {
        description: detailMessage,
      });
      return;
    }

    updateLdapMutation.mutate(payload);
  }, [
    buildLdapPayload,
    buildLdapValidatePayload,
    canUpdate,
    t,
    updateLdapMutation,
    validateLdapForm,
    validateLdapMutation,
  ]);

  return {
    saveLdapSettings,
    canSaveLdap,
    isSavingLdap: updateLdapMutation.isPending,
  };
}
