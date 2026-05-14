import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";

import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useAuthStore } from "@/features/auth/auth-store";
import {
  getSettings,
  uploadBrandingFavicon,
  uploadBrandingLogo,
  updateApplicationSettings,
  updateLdapSettings,
  updateSessionSecuritySettings,
  validateLdapSettings,
} from "@/features/settings/api";
import {
  ApplicationSettingsForm,
} from "@/features/settings/components/ApplicationSettingsForm";
import {
  DirectorySettingsForm,
} from "@/features/settings/components/DirectorySettingsForm";
import { LdapSettingsForm } from "@/features/settings/components/LdapSettingsForm";
import { SessionSecuritySettingsForm } from "@/features/settings/components/SessionSecuritySettingsForm";
import {
  AUTH_SESSION_OPTIONS_QUERY_KEY,
  DEFAULT_SESSION_SECURITY,
  DEFAULT_TAB,
  MAX_FAVICON_BYTES,
  MAX_LOGO_BYTES,
  SETTINGS_QUERY_KEY,
  type SettingsTabValue,
} from "@/features/settings/settings-constants";
import { useBrandingSettingsForm } from "@/features/settings/hooks/useBrandingSettingsForm";
import { useDirectorySettingsForm } from "@/features/settings/hooks/useDirectorySettingsForm";
import { useLdapSettingsForm } from "@/features/settings/hooks/useLdapSettingsForm";
import { sessionSecurityFingerprint } from "@/features/settings/settings-utils";
import { getApiErrorMessage } from "@/lib/api-error";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { canAccess } from "@/lib/permissions";
import { BRANDING_QUERY_KEY } from "@/hooks/useBrandingSettings";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";
import type { SessionSecuritySettings } from "@/features/settings/types";

export function SettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const canUpdate = canAccess(currentUser, "Settings.Update");
  const isReadOnly = !canUpdate;

  const {
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
  } = useLdapSettingsForm({ t });

  const {
    nationalIdAttribute,
    directoryError,
    hydrateFromApplicationSettings,
    updateNationalIdAttribute,
    clearDirectoryError,
    buildDirectoryPayload,
  } = useDirectorySettingsForm();

  const {
    brandingApplicationName,
    brandingBrowserTitle,
    forgotPasswordUrl,
    forgotPasswordUrlError,
    brandingError,
    hydrateFromBranding,
    updateApplicationName,
    updateBrowserTitle,
    updateForgotPasswordUrl,
    clearBrandingError,
    clearForgotPasswordUrlError,
    validateBrandingInput,
    validateForgotPasswordUrlInput,
    buildBrandingPayload,
  } = useBrandingSettingsForm({ t });

  const [activeTab, setActiveTab] = useState<SettingsTabValue>(DEFAULT_TAB);
  const [brandingLogoUrl, setBrandingLogoUrl] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoPreviewUrl, setLogoPreviewUrl] = useState<string | null>(null);
  const [selectedLogoFileName, setSelectedLogoFileName] = useState<string | null>(null);
  const [brandingFaviconUrl, setBrandingFaviconUrl] = useState<string | null>(null);
  const [faviconFile, setFaviconFile] = useState<File | null>(null);
  const [faviconPreviewUrl, setFaviconPreviewUrl] = useState<string | null>(null);
  const [selectedFaviconFileName, setSelectedFaviconFileName] = useState<string | null>(null);

  const settingsQuery = useQuery({
    queryKey: SETTINGS_QUERY_KEY,
    queryFn: getSettings,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  useEffect(() => {
    if (!settingsQuery.data) return;
    const ldap = settingsQuery.data.ldap;

    /* eslint-disable react-hooks/set-state-in-effect -- bulk hydrate local UI from settings query snapshot */
    hydrateFromSettings(ldap);
    hydrateFromApplicationSettings(settingsQuery.data.applicationSettings);
    hydrateFromBranding(settingsQuery.data.branding);
    setBrandingLogoUrl(settingsQuery.data.branding.logoUrl ?? null);
    setLogoFile(null);
    setLogoPreviewUrl(null);
    setSelectedLogoFileName(null);
    setBrandingFaviconUrl(settingsQuery.data.branding.faviconUrl ?? null);
    setFaviconFile(null);
    setFaviconPreviewUrl(null);
    setSelectedFaviconFileName(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [settingsQuery.data, hydrateFromApplicationSettings, hydrateFromBranding, hydrateFromSettings]);

  useEffect(() => {
    return () => {
      if (logoPreviewUrl) {
        URL.revokeObjectURL(logoPreviewUrl);
      }
    };
  }, [logoPreviewUrl]);

  useEffect(() => {
    return () => {
      if (faviconPreviewUrl) {
        URL.revokeObjectURL(faviconPreviewUrl);
      }
    };
  }, [faviconPreviewUrl]);

  const updateLdapMutation = useMutation({
    mutationFn: updateLdapSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      clearBindPasswordAfterSave();
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

  const updateBrandingMutation = useMutation({
    mutationFn: updateApplicationSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      await queryClient.invalidateQueries({ queryKey: BRANDING_QUERY_KEY });
      if (logoPreviewUrl) {
        URL.revokeObjectURL(logoPreviewUrl);
      }
      setLogoFile(null);
      setLogoPreviewUrl(null);
      setSelectedLogoFileName(null);
      if (faviconPreviewUrl) {
        URL.revokeObjectURL(faviconPreviewUrl);
      }
      setFaviconFile(null);
      setFaviconPreviewUrl(null);
      setSelectedFaviconFileName(null);
      clearBrandingError();
      clearForgotPasswordUrlError();
      toast.success(t("settings:application.messages.saveSuccess"));
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("settings:application.messages.saveFailed")));
    },
  });

  const updateDirectoryMutation = useMutation({
    mutationFn: updateApplicationSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SETTINGS_QUERY_KEY });
      clearDirectoryError();
      toast.success(t("settings:directory.messages.saveSuccess"));
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("settings:directory.messages.saveFailed")));
    },
  });

  const updateSessionSecurityMutation = useMutation({
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
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("settings:sessionSecurity.messages.saveFailed")));
    },
  });

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

  const validateLogoFile = async (file: File): Promise<boolean> => {
    const extension = file.name.split(".").pop()?.toLowerCase();
    if (!extension || !["png", "jpg", "jpeg"].includes(extension)) {
      toast.error(t("settings:application.validation.logoType"));
      return false;
    }

    if (file.size > MAX_LOGO_BYTES) {
      toast.error(t("settings:application.validation.logoSize"));
      return false;
    }

    const objectUrl = URL.createObjectURL(file);
    const dimensionsValid = await new Promise<boolean>((resolve) => {
      const image = new Image();
      image.onload = () => {
        const ok =
          image.naturalWidth >= 32 &&
          image.naturalHeight >= 32 &&
          image.naturalWidth <= 512 &&
          image.naturalHeight <= 512;
        resolve(ok);
      };
      image.onerror = () => resolve(false);
      image.src = objectUrl;
    });
    URL.revokeObjectURL(objectUrl);

    if (!dimensionsValid) {
      toast.error(t("settings:application.validation.logoDimensions"));
      return false;
    }

    return true;
  };

  const handleLogoSelect = async (file: File | null) => {
    if (!file) {
      setSelectedLogoFileName(null);
      return;
    }

    const valid = await validateLogoFile(file);
    if (!valid) {
      setSelectedLogoFileName(null);
      return;
    }

    if (logoPreviewUrl) {
      URL.revokeObjectURL(logoPreviewUrl);
    }

    setLogoFile(file);
    setLogoPreviewUrl(URL.createObjectURL(file));
    setSelectedLogoFileName(file.name);
  };

  const validateFaviconFile = async (file: File): Promise<boolean> => {
    const extension = file.name.split(".").pop()?.toLowerCase();
    if (!extension || !["png", "jpg", "jpeg"].includes(extension)) {
      toast.error(t("settings:application.validation.faviconType"));
      return false;
    }

    if (file.size > MAX_FAVICON_BYTES) {
      toast.error(t("settings:application.validation.faviconSize"));
      return false;
    }

    const objectUrl = URL.createObjectURL(file);
    const dimensionsValid = await new Promise<boolean>((resolve) => {
      const image = new Image();
      image.onload = () => {
        const ok =
          image.naturalWidth >= 16 &&
          image.naturalHeight >= 16 &&
          image.naturalWidth <= 512 &&
          image.naturalHeight <= 512;
        resolve(ok);
      };
      image.onerror = () => resolve(false);
      image.src = objectUrl;
    });
    URL.revokeObjectURL(objectUrl);

    if (!dimensionsValid) {
      toast.error(t("settings:application.validation.faviconDimensions"));
      return false;
    }

    return true;
  };

  const handleFaviconSelect = async (file: File | null) => {
    if (!file) {
      setSelectedFaviconFileName(null);
      return;
    }

    const valid = await validateFaviconFile(file);
    if (!valid) {
      setSelectedFaviconFileName(null);
      return;
    }

    if (faviconPreviewUrl) {
      URL.revokeObjectURL(faviconPreviewUrl);
    }

    setFaviconFile(file);
    setFaviconPreviewUrl(URL.createObjectURL(file));
    setSelectedFaviconFileName(file.name);
  };

  const handleBrandingSave = async () => {
    if (!canUpdate) return;
    clearBrandingError();
    if (!validateBrandingInput()) return;
    if (!validateForgotPasswordUrlInput()) return;

    let logoUrlToPersist = brandingLogoUrl;
    if (logoFile) {
      try {
        const uploadResult = await uploadBrandingLogo(logoFile);
        logoUrlToPersist = uploadResult.logoUrl;
      } catch (error) {
        toast.error(getApiErrorMessage(error, t("settings:application.messages.logoUploadFailed")));
        return;
      }
    }

    let faviconUrlToPersist = brandingFaviconUrl;
    if (faviconFile) {
      try {
        const uploadResult = await uploadBrandingFavicon(faviconFile);
        faviconUrlToPersist = uploadResult.faviconUrl;
      } catch (error) {
        toast.error(getApiErrorMessage(error, t("settings:application.messages.faviconUploadFailed")));
        return;
      }
    }

    updateBrandingMutation.mutate(
      buildBrandingPayload({ logoUrlToPersist, faviconUrlToPersist }),
    );
  };

  const handleDirectorySave = () => {
    if (!canUpdate) return;
    clearDirectoryError();

    updateDirectoryMutation.mutate(buildDirectoryPayload());
  };

  const handleSessionSecuritySubmit = (payload: SessionSecuritySettings) => {
    if (!canUpdate) return;
    updateSessionSecurityMutation.mutate(payload);
  };

  const refreshAction = useMemo(
    () => (
      <Button variant="outline" onClick={() => settingsQuery.refetch()}>
        {t("common:actions.refresh")}
      </Button>
    ),
    [settingsQuery, t],
  );

  if (settingsQuery.isError) {
    const routeState = createApiErrorRouteState(settingsQuery.error, {
      fromPath: "/settings",
      retryPath: "/settings",
      sourceLabel: t("settings:title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  if (settingsQuery.isLoading) {
    return (
      <section className="space-y-4">
        <div className="flex justify-end">{refreshAction}</div>
        <LoadingState />
      </section>
    );
  }

  return (
    <section className="space-y-4">
      <div className="flex justify-end">{refreshAction}</div>

      {isReadOnly ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("settings:readOnlyNotice")}
        </p>
      ) : null}

      <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as SettingsTabValue)}>
        <TabsList className="grid w-full grid-cols-1 sm:grid-cols-4">
          <TabsTrigger value="branding">{t("settings:tabs.branding")}</TabsTrigger>
          <TabsTrigger value="sessionSecurity">{t("settings:tabs.sessionSecurity")}</TabsTrigger>
          <TabsTrigger value="ldap">{t("settings:tabs.ldap")}</TabsTrigger>
          <TabsTrigger value="directory">{t("settings:tabs.directory")}</TabsTrigger>
        </TabsList>

        <TabsContent value="branding">
          <SectionCard>
            <ApplicationSettingsForm
              applicationName={brandingApplicationName}
              browserTitle={brandingBrowserTitle}
              selectedLogoPreviewUrl={logoPreviewUrl}
              currentLogoUrl={brandingLogoUrl}
              selectedLogoFileName={selectedLogoFileName}
              selectedFaviconPreviewUrl={faviconPreviewUrl}
              currentFaviconUrl={brandingFaviconUrl}
              selectedFaviconFileName={selectedFaviconFileName}
              forgotPasswordUrl={forgotPasswordUrl}
              readOnly={isReadOnly}
              isSaving={updateBrandingMutation.isPending}
              errorMessage={brandingError}
              forgotPasswordUrlError={forgotPasswordUrlError}
              onApplicationNameChange={updateApplicationName}
              onBrowserTitleChange={updateBrowserTitle}
              onSelectLogo={handleLogoSelect}
              onSelectFavicon={handleFaviconSelect}
              onForgotPasswordUrlChange={updateForgotPasswordUrl}
              onSave={() => void handleBrandingSave()}
            />
          </SectionCard>
        </TabsContent>

        <TabsContent value="sessionSecurity">
          <SectionCard>
            {settingsQuery.data ? (
              <SessionSecuritySettingsForm
                key={sessionSecurityFingerprint(
                  settingsQuery.data.sessionSecurity ?? DEFAULT_SESSION_SECURITY,
                )}
                initialValues={
                  settingsQuery.data.sessionSecurity ?? DEFAULT_SESSION_SECURITY
                }
                readOnly={isReadOnly}
                isSaving={updateSessionSecurityMutation.isPending}
                onSubmit={handleSessionSecuritySubmit}
              />
            ) : null}
          </SectionCard>
        </TabsContent>

        <TabsContent value="ldap">
          <SectionCard>
            <LdapSettingsForm
              values={ldapForm}
              fieldErrors={ldapFieldErrors}
              hasBindPassword={hasBindPassword}
              readOnly={isReadOnly}
              savePending={updateLdapMutation.isPending}
              canSave={canSaveLdap}
              onChange={updateField}
              onSave={handleLdapSave}
            />
          </SectionCard>
        </TabsContent>

        <TabsContent value="directory">
          <SectionCard>
            <DirectorySettingsForm
              nationalIdAttribute={nationalIdAttribute}
              readOnly={isReadOnly}
              isSaving={updateDirectoryMutation.isPending}
              errorMessage={directoryError}
              onNationalIdAttributeChange={(value) => {
                clearDirectoryError();
                updateNationalIdAttribute(value);
              }}
              onSave={handleDirectorySave}
            />
          </SectionCard>
        </TabsContent>
      </Tabs>
    </section>
  );
}
