import { useCallback, useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { toast } from "sonner";

import {
  updateLdapSettings,
  validateLdapSettings,
  validateSavedLdapSettings,
} from "@/features/settings/api";
import { SETTINGS_QUERY_KEY } from "@/features/settings/settings-constants";
import type {
  UpdateLdapSettingsRequest,
  ValidateLdapSettingsRequest,
  ValidateLdapSettingsResponse,
} from "@/features/settings/types";
import { getApiErrorMessage } from "@/lib/api-error";

import type { UseLdapSettingsFormNamespaces } from "./useLdapSettingsForm";

export type UseLdapSettingsSaveParams = {
  t: TFunction<UseLdapSettingsFormNamespaces>;
  canUpdate: boolean;
  ldapFormIsMinimumValid: boolean;
  ldapConfigurationFingerprint: string;
  validateLdapForm: () => boolean;
  buildLdapPayload: () => UpdateLdapSettingsRequest;
  buildLdapValidatePayload: () => ValidateLdapSettingsRequest;
  clearBindPasswordAfterSave: () => void;
};

export function useLdapSettingsSave({
  t,
  canUpdate,
  ldapFormIsMinimumValid,
  ldapConfigurationFingerprint,
  validateLdapForm,
  buildLdapPayload,
  buildLdapValidatePayload,
  clearBindPasswordAfterSave,
}: UseLdapSettingsSaveParams) {
  const queryClient = useQueryClient();
  const [candidateResult, setCandidateResult] = useState<ValidateLdapSettingsResponse | null>(null);
  const [savedResult, setSavedResult] = useState<ValidateLdapSettingsResponse | null>(null);
  const [validatedFingerprint, setValidatedFingerprint] = useState<string | null>(null);
  const [candidateRequestFingerprint, setCandidateRequestFingerprint] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSucceeded, setSaveSucceeded] = useState(false);

  const candidateMutation = useMutation({
    mutationFn: validateLdapSettings,
    onSuccess: (result, variables) => {
      setCandidateResult(result);
      setCandidateRequestFingerprint(JSON.stringify(variables));
      setValidatedFingerprint(result.isValid ? ldapConfigurationFingerprint : null);
    },
  });
  const savedMutation = useMutation({
    mutationFn: validateSavedLdapSettings,
    onSuccess: setSavedResult,
  });
  const updateMutation = useMutation({
    mutationFn: updateLdapSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      clearBindPasswordAfterSave();
      setSaveError(null);
      setSaveSucceeded(true);
      setValidatedFingerprint(null);
      toast.success(t("settings:ldap.messages.saveSuccess"));
    },
    onError: (error: unknown) => {
      setSaveSucceeded(false);
      setSaveError(getApiErrorMessage(error, t("settings:ldap.messages.saveFailed")));
    },
  });

  const testCandidate = useCallback(async () => {
    if (!canUpdate || !validateLdapForm()) return;
    setSaveError(null);
    try {
      await candidateMutation.mutateAsync(buildLdapValidatePayload());
    } catch (error) {
      setCandidateResult(null);
      setCandidateRequestFingerprint(null);
      setValidatedFingerprint(null);
      setSaveError(getApiErrorMessage(error, t("settings:ldap.validation.requestFailed")));
    }
  }, [buildLdapValidatePayload, canUpdate, candidateMutation, t, validateLdapForm]);

  const testSaved = useCallback(async () => {
    setSaveError(null);
    try {
      await savedMutation.mutateAsync();
    } catch (error) {
      setSavedResult(null);
      setSaveError(getApiErrorMessage(error, t("settings:ldap.validation.requestFailed")));
    }
  }, [savedMutation, t]);

  const save = useCallback(() => {
    if (!canUpdate || !validateLdapForm()) return;
    if (validatedFingerprint !== ldapConfigurationFingerprint) return;
    setSaveError(null);
    setSaveSucceeded(false);
    updateMutation.mutate(buildLdapPayload());
  }, [
    buildLdapPayload,
    canUpdate,
    ldapConfigurationFingerprint,
    updateMutation,
    validateLdapForm,
    validatedFingerprint,
  ]);

  const candidateIsCurrent = validatedFingerprint === ldapConfigurationFingerprint;
  const candidateResultIsCurrent = candidateRequestFingerprint === JSON.stringify(buildLdapValidatePayload());
  const isBusy = candidateMutation.isPending || savedMutation.isPending || updateMutation.isPending;
  const canSave = useMemo(
    () => canUpdate && ldapFormIsMinimumValid && candidateIsCurrent && !isBusy,
    [canUpdate, candidateIsCurrent, isBusy, ldapFormIsMinimumValid],
  );

  return {
    saveLdapSettings: save,
    testCandidateLdapSettings: testCandidate,
    testSavedLdapSettings: testSaved,
    canSaveLdap: canSave,
    isSavingLdap: updateMutation.isPending,
    isTestingCandidateLdap: candidateMutation.isPending,
    isTestingSavedLdap: savedMutation.isPending,
    candidateLdapValidation: candidateResult,
    savedLdapValidation: savedResult,
    candidateLdapValidationIsCurrent: candidateResultIsCurrent,
    ldapSaveError: saveError,
    ldapSaveSucceeded: saveSucceeded,
  };
}
