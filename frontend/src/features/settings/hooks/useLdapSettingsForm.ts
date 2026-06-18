import { useCallback, useMemo, useState } from "react";
import type { TFunction } from "i18next";

import type { LdapFormValues } from "@/features/settings/components/LdapSettingsForm";
import { createEmptyLdapForm } from "@/features/settings/settings-utils";
import type {
  LdapSettings,
  UpdateLdapSettingsRequest,
  ValidateLdapSettingsRequest,
} from "@/features/settings/types";

export type UseLdapSettingsFormNamespaces = readonly ["settings", "common"];

export type UseLdapSettingsFormParams = {
  t: TFunction<UseLdapSettingsFormNamespaces>;
};

function getLdapFormErrors(
  form: LdapFormValues,
  t: TFunction<UseLdapSettingsFormNamespaces>,
): Partial<Record<keyof LdapFormValues, string>> {
  const errors: Partial<Record<keyof LdapFormValues, string>> = {};
  if (!form.name.trim()) errors.name = t("settings:validation.nameRequired");
  if (!form.host.trim()) errors.host = t("settings:validation.hostRequired");
  if (!form.baseDn.trim()) errors.baseDn = t("settings:validation.baseDnRequired");
  if (!form.userSearchFilter.trim()) {
    errors.userSearchFilter = t("settings:validation.userSearchFilterRequired");
  }
  if (!form.bindUserName.trim()) {
    errors.bindUserName = t("settings:validation.bindUserNameRequired");
  }
  return errors;
}

export type UseLdapSettingsFormReturn = {
  ldapForm: LdapFormValues;
  ldapFieldErrors: Partial<Record<keyof LdapFormValues, string>>;
  hasBindPassword: boolean;
  hydrateFromSettings: (ldap: LdapSettings | null) => void;
  updateField: <K extends keyof LdapFormValues>(field: K, value: LdapFormValues[K]) => void;
  validateLdapForm: () => boolean;
  buildLdapPayload: () => UpdateLdapSettingsRequest;
  buildLdapValidatePayload: () => ValidateLdapSettingsRequest;
  ldapFormIsMinimumValid: boolean;
  clearBindPasswordAfterSave: () => void;
};

export function useLdapSettingsForm({
  t,
}: UseLdapSettingsFormParams): UseLdapSettingsFormReturn {
  const [ldapForm, setLdapForm] = useState<LdapFormValues>(createEmptyLdapForm);
  const [ldapFieldErrors, setLdapFieldErrors] = useState<
    Partial<Record<keyof LdapFormValues, string>>
  >({});
  const [hasBindPassword, setHasBindPassword] = useState(false);

  const hydrateFromSettings = useCallback((ldap: LdapSettings | null) => {
    setLdapForm({
      name: ldap?.name ?? "",
      host: ldap?.host ?? "",
      baseDn: ldap?.baseDn ?? "",
      userSearchBase: ldap?.userSearchBase ?? "",
      userSearchFilter: ldap?.userSearchFilter ?? "",
      bindUserName: ldap?.bindUserName ?? "",
      bindUserDomain: ldap?.bindUserDomain ?? "",
      bindPassword: "",
      description: ldap?.description ?? "",
    });
    setHasBindPassword(Boolean(ldap?.hasBindPassword));
    setLdapFieldErrors({});
  }, []);

  const updateField = useCallback(<K extends keyof LdapFormValues>(
    field: K,
    value: LdapFormValues[K],
  ) => {
    setLdapForm((prev) => ({ ...prev, [field]: value }));
    setLdapFieldErrors((prev) => ({ ...prev, [field]: undefined }));
  }, []);

  const validateLdapForm = useCallback(() => {
    const errors = getLdapFormErrors(ldapForm, t);
    setLdapFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }, [ldapForm, t]);

  const buildLdapPayload = useCallback((): UpdateLdapSettingsRequest => {
    const payload = {
      name: ldapForm.name.trim(),
      host: ldapForm.host.trim(),
      baseDn: ldapForm.baseDn.trim(),
      userSearchBase: ldapForm.userSearchBase.trim(),
      userSearchFilter: ldapForm.userSearchFilter.trim(),
      bindUserName: ldapForm.bindUserName.trim(),
      bindUserDomain: ldapForm.bindUserDomain.trim() || null,
      description: ldapForm.description.trim() || null,
    };

    return ldapForm.bindPassword
      ? { ...payload, bindPassword: ldapForm.bindPassword }
      : payload;
  }, [ldapForm]);

  const buildLdapValidatePayload = useCallback((): ValidateLdapSettingsRequest => {
    const payload = {
      name: ldapForm.name.trim(),
      host: ldapForm.host.trim(),
      baseDn: ldapForm.baseDn.trim(),
      userSearchBase: ldapForm.userSearchBase.trim(),
      userSearchFilter: ldapForm.userSearchFilter.trim(),
      bindUserName: ldapForm.bindUserName.trim(),
      bindUserDomain: ldapForm.bindUserDomain.trim() || null,
    };

    return ldapForm.bindPassword
      ? { ...payload, bindPassword: ldapForm.bindPassword }
      : payload;
  }, [ldapForm]);

  const ldapFormIsMinimumValid = useMemo(
    () => Object.keys(getLdapFormErrors(ldapForm, t)).length === 0,
    [ldapForm, t],
  );

  const clearBindPasswordAfterSave = useCallback(() => {
    setLdapForm((prev) => ({ ...prev, bindPassword: "" }));
  }, []);

  return {
    ldapForm,
    ldapFieldErrors,
    hasBindPassword,
    hydrateFromSettings,
    updateField,
    validateLdapForm,
    buildLdapPayload,
    buildLdapValidatePayload,
    ldapFormIsMinimumValid,
    clearBindPasswordAfterSave,
  };
}
