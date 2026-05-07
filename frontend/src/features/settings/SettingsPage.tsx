import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/features/auth/auth-store";
import {
  getSettings,
  updateApplicationSettings,
  updateLdapSettings,
  validateLdapSettings,
} from "@/features/settings/api";
import {
  ApplicationSettingsForm,
} from "@/features/settings/components/ApplicationSettingsForm";
import {
  LdapSettingsForm,
  type LdapFormValues,
} from "@/features/settings/components/LdapSettingsForm";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";
import type {
  UpdateLdapSettingsRequest,
  ValidateLdapSettingsRequest,
} from "@/features/settings/types";

const SETTINGS_QUERY_KEY = ["settings", "overview"] as const;
const DIRECTORY_NATIONAL_ID_ATTRIBUTE_KEY = "Directory:NationalIdAttribute";
const SETTING_VALUE_TYPE_STRING = 1;

const createEmptyLdapForm = (): LdapFormValues => ({
  name: "",
  host: "",
  port: "389",
  useSsl: false,
  baseDn: "",
  userSearchBase: "",
  userSearchFilter: "",
  bindUserName: "",
  bindUserDomain: "",
  bindPassword: "",
  description: "",
});

export function SettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const canUpdate = canAccess(currentUser, "Settings.Update");
  const isReadOnly = !canUpdate;

  const [ldapForm, setLdapForm] = useState<LdapFormValues>(createEmptyLdapForm);
  const [ldapFieldErrors, setLdapFieldErrors] = useState<
    Partial<Record<keyof LdapFormValues, string>>
  >({});
  const [hasBindPassword, setHasBindPassword] = useState(false);
  const [applicationValue, setApplicationValue] = useState("");
  const [applicationError, setApplicationError] = useState<string | undefined>(undefined);

  const settingsQuery = useQuery({
    queryKey: SETTINGS_QUERY_KEY,
    queryFn: getSettings,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  useEffect(() => {
    if (!settingsQuery.data) return;
    const ldap = settingsQuery.data.ldap;
    const applicationSetting = settingsQuery.data.applicationSettings.find(
      (item) => item.key === DIRECTORY_NATIONAL_ID_ATTRIBUTE_KEY,
    );

    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLdapForm({
      name: ldap?.name ?? "",
      host: ldap?.host ?? "",
      port: ldap?.port ? String(ldap.port) : "389",
      useSsl: ldap?.useSsl ?? false,
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
    setApplicationValue(applicationSetting?.value ?? "");
    setApplicationError(undefined);
  }, [settingsQuery.data]);

  const updateLdapMutation = useMutation({
    mutationFn: updateLdapSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      setLdapForm((prev) => ({ ...prev, bindPassword: "" }));
      toast.success(t("settings:ldap.messages.saveSuccess"));
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("settings:ldap.messages.saveFailed")));
    },
  });

  const validateLdapMutation = useMutation({
    mutationFn: validateLdapSettings,
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("settings:ldap.validation.requestFailed")));
    },
  });

  const updateApplicationMutation = useMutation({
    mutationFn: updateApplicationSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      setApplicationError(undefined);
      toast.success(t("settings:application.messages.saveSuccess"));
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("settings:application.messages.saveFailed")));
    },
  });

  const getLdapFormErrors = (form: LdapFormValues) => {
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
    const parsedPort = Number(form.port);
    if (!Number.isInteger(parsedPort) || parsedPort < 1 || parsedPort > 65535) {
      errors.port = t("settings:validation.portRange");
    }
    return errors;
  };

  const validateLdapForm = () => {
    const errors = getLdapFormErrors(ldapForm);
    setLdapFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const buildLdapPayload = (): UpdateLdapSettingsRequest => {
    const payload = {
      name: ldapForm.name.trim(),
      host: ldapForm.host.trim(),
      port: Number(ldapForm.port),
      useSsl: ldapForm.useSsl,
      baseDn: ldapForm.baseDn.trim(),
      userSearchBase: ldapForm.userSearchBase.trim(),
      userSearchFilter: ldapForm.userSearchFilter.trim(),
      bindUserName: ldapForm.bindUserName.trim(),
      bindUserDomain: ldapForm.bindUserDomain.trim() || null,
      description: ldapForm.description.trim() || null,
    };

    return ldapForm.bindPassword ? { ...payload, bindPassword: ldapForm.bindPassword } : payload;
  };

  const buildLdapValidatePayload = (): ValidateLdapSettingsRequest => {
    const payload = {
      name: ldapForm.name.trim(),
      host: ldapForm.host.trim(),
      port: Number(ldapForm.port),
      useSsl: ldapForm.useSsl,
      baseDn: ldapForm.baseDn.trim(),
      userSearchBase: ldapForm.userSearchBase.trim(),
      userSearchFilter: ldapForm.userSearchFilter.trim(),
      bindUserName: ldapForm.bindUserName.trim(),
      bindUserDomain: ldapForm.bindUserDomain.trim() || null,
    };

    return ldapForm.bindPassword ? { ...payload, bindPassword: ldapForm.bindPassword } : payload;
  };

  const ldapFormIsMinimumValid = Object.keys(getLdapFormErrors(ldapForm)).length === 0;
  const canSaveLdap =
    canUpdate &&
    ldapFormIsMinimumValid &&
    !updateLdapMutation.isPending &&
    !validateLdapMutation.isPending;

  const handleLdapSave = async () => {
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
      const detailMessage = getLocalizedLdapValidationMessage(validateResult.message);
      toast.error(t("settings:ldap.validation.saveBlockedByValidation"), {
        description: detailMessage,
      });
      return;
    }

    updateLdapMutation.mutate(payload);
  };

  const getLocalizedLdapValidationMessage = (message?: string | null): string => {
    const trimmedMessage = message?.trim();
    if (!trimmedMessage) {
      return t("settings:ldap.validation.genericCouldNotValidate");
    }

    const messageKeyMap: Record<string, string> = {
      "LDAP service account authentication failed.":
        "settings:ldap.validation.backendMessages.serviceAccountAuthFailed",
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
    };

    const mappedKey = messageKeyMap[trimmedMessage];
    if (!mappedKey) {
      return t("settings:ldap.validation.genericCouldNotValidate");
    }

    return t(mappedKey);
  };

  const handleApplicationSave = () => {
    if (!canUpdate) return;
    setApplicationError(undefined);
    updateApplicationMutation.mutate({
      items: [
        {
          key: DIRECTORY_NATIONAL_ID_ATTRIBUTE_KEY,
          value: applicationValue.trim() || null,
          valueType: SETTING_VALUE_TYPE_STRING,
        },
      ],
    });
  };

  const pageActions = useMemo(
    () => (
      <Button variant="outline" onClick={() => settingsQuery.refetch()}>
        {t("common:actions.refresh")}
      </Button>
    ),
    [settingsQuery, t],
  );

  if (settingsQuery.isLoading) {
    return (
      <section className="space-y-4">
        <PageHeader
          title={t("settings:title")}
          description={t("settings:description")}
          actions={pageActions}
        />
        <LoadingState />
      </section>
    );
  }

  if (settingsQuery.isError) {
    return (
      <section className="space-y-4">
        <PageHeader
          title={t("settings:title")}
          description={t("settings:description")}
          actions={pageActions}
        />
        <ErrorState
          title={t("settings:errors.loadFailed")}
          description={getApiErrorMessage(settingsQuery.error, t("settings:errors.loadFailed"))}
          retry={
            <Button variant="outline" onClick={() => settingsQuery.refetch()}>
              {t("common:actions.refresh")}
            </Button>
          }
        />
      </section>
    );
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("settings:title")}
        description={t("settings:description")}
        actions={pageActions}
      />

      {isReadOnly ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("settings:readOnlyNotice")}
        </p>
      ) : null}

      <SectionCard
        title={t("settings:ldap.sectionTitle")}
        description={t("settings:ldap.sectionDescription")}
      >
        <LdapSettingsForm
          values={ldapForm}
          fieldErrors={ldapFieldErrors}
          hasBindPassword={hasBindPassword}
          readOnly={isReadOnly}
          savePending={updateLdapMutation.isPending}
          canSave={canSaveLdap}
          onChange={(field, value) => {
            setLdapForm((prev) => ({ ...prev, [field]: value }));
            setLdapFieldErrors((prev) => ({ ...prev, [field]: undefined }));
          }}
          onSave={handleLdapSave}
        />
      </SectionCard>

      <SectionCard
        title={t("settings:application.sectionTitle")}
        description={t("settings:application.sectionDescription")}
      >
        <ApplicationSettingsForm
          nationalIdAttribute={applicationValue}
          readOnly={isReadOnly}
          isSaving={updateApplicationMutation.isPending}
          errorMessage={applicationError}
          onNationalIdAttributeChange={(value) => {
            setApplicationError(undefined);
            setApplicationValue(value);
          }}
          onSave={handleApplicationSave}
        />
      </SectionCard>
    </section>
  );
}
