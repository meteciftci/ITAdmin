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
import {
  LdapSettingsForm,
  type LdapFormValues,
} from "@/features/settings/components/LdapSettingsForm";
import { SessionSecuritySettingsForm } from "@/features/settings/components/SessionSecuritySettingsForm";
import { getApiErrorMessage } from "@/lib/api-error";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { canAccess } from "@/lib/permissions";
import { BRANDING_QUERY_KEY } from "@/hooks/useBrandingSettings";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";
import type {
  SessionSecuritySettings,
  UpdateLdapSettingsRequest,
  ValidateLdapSettingsRequest,
} from "@/features/settings/types";

const SETTINGS_QUERY_KEY = ["settings", "overview"] as const;
const DIRECTORY_NATIONAL_ID_ATTRIBUTE_KEY = "Directory:NationalIdAttribute";
const BRANDING_APPLICATION_NAME_KEY = "Branding:ApplicationName";
const BRANDING_BROWSER_TITLE_KEY = "Branding:BrowserTitle";
const BRANDING_LOGO_URL_KEY = "Branding:LogoUrl";
const BRANDING_FAVICON_URL_KEY = "Branding:FaviconUrl";
const BRANDING_FORGOT_PASSWORD_URL_KEY = "Branding:ForgotPasswordUrl";
const SETTING_VALUE_TYPE_STRING = 1;
const MAX_LOGO_BYTES = 2 * 1024 * 1024;
const MAX_FAVICON_BYTES = 512 * 1024;

type SettingsTabValue = "ldap" | "branding" | "directory" | "sessionSecurity";
const DEFAULT_TAB: SettingsTabValue = "ldap";

const DEFAULT_SESSION_SECURITY: SessionSecuritySettings = {
  accessTokenMinutes: 30,
  idleTimeoutMinutes: 30,
  idleWarningSeconds: 30,
  sessionRefreshTokenHours: 6,
  rememberMeRefreshTokenDays: 7,
  rememberMeEnabled: true,
};

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

const sessionSecurityFingerprint = (s: SessionSecuritySettings): string =>
  [
    s.accessTokenMinutes,
    s.idleTimeoutMinutes,
    s.idleWarningSeconds,
    s.sessionRefreshTokenHours,
    s.rememberMeRefreshTokenDays,
    s.rememberMeEnabled ? "1" : "0",
  ].join("|");

export function SettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const canUpdate = canAccess(currentUser, "Settings.Update");
  const isReadOnly = !canUpdate;

  const [activeTab, setActiveTab] = useState<SettingsTabValue>(DEFAULT_TAB);
  const [ldapForm, setLdapForm] = useState<LdapFormValues>(createEmptyLdapForm);
  const [ldapFieldErrors, setLdapFieldErrors] = useState<
    Partial<Record<keyof LdapFormValues, string>>
  >({});
  const [hasBindPassword, setHasBindPassword] = useState(false);
  const [nationalIdAttribute, setNationalIdAttribute] = useState("");
  const [brandingApplicationName, setBrandingApplicationName] = useState("SAS Portal v2");
  const [brandingBrowserTitle, setBrandingBrowserTitle] = useState("SAS Portal v2");
  const [brandingLogoUrl, setBrandingLogoUrl] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoPreviewUrl, setLogoPreviewUrl] = useState<string | null>(null);
  const [selectedLogoFileName, setSelectedLogoFileName] = useState<string | null>(null);
  const [brandingFaviconUrl, setBrandingFaviconUrl] = useState<string | null>(null);
  const [faviconFile, setFaviconFile] = useState<File | null>(null);
  const [faviconPreviewUrl, setFaviconPreviewUrl] = useState<string | null>(null);
  const [selectedFaviconFileName, setSelectedFaviconFileName] = useState<string | null>(null);
  const [forgotPasswordUrl, setForgotPasswordUrl] = useState("");
  const [forgotPasswordUrlError, setForgotPasswordUrlError] = useState<string | undefined>(undefined);
  const [brandingError, setBrandingError] = useState<string | undefined>(undefined);
  const [directoryError, setDirectoryError] = useState<string | undefined>(undefined);

  const settingsQuery = useQuery({
    queryKey: SETTINGS_QUERY_KEY,
    queryFn: getSettings,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  useEffect(() => {
    if (!settingsQuery.data) return;
    const ldap = settingsQuery.data.ldap;
    const directorySetting = settingsQuery.data.applicationSettings.find(
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
    setNationalIdAttribute(directorySetting?.value ?? "");
    setBrandingApplicationName(settingsQuery.data.branding.applicationName ?? "SAS Portal v2");
    setBrandingBrowserTitle(settingsQuery.data.branding.browserTitle ?? "SAS Portal v2");
    setBrandingLogoUrl(settingsQuery.data.branding.logoUrl ?? null);
    setLogoFile(null);
    setLogoPreviewUrl(null);
    setSelectedLogoFileName(null);
    setBrandingFaviconUrl(settingsQuery.data.branding.faviconUrl ?? null);
    setFaviconFile(null);
    setFaviconPreviewUrl(null);
    setSelectedFaviconFileName(null);
    setForgotPasswordUrl(settingsQuery.data.branding.forgotPasswordUrl ?? "");
    setForgotPasswordUrlError(undefined);
    setBrandingError(undefined);
    setDirectoryError(undefined);
  }, [settingsQuery.data]);

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
      setBrandingError(undefined);
      setForgotPasswordUrlError(undefined);
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
      setDirectoryError(undefined);
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
      toast.success(t("settings:sessionSecurity.messages.saved"));
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("settings:sessionSecurity.messages.saveFailed")));
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

  const validateBrandingInput = (): boolean => {
    if (brandingApplicationName.trim().length > 100) {
      setBrandingError(t("settings:application.validation.applicationNameMax"));
      return false;
    }

    if (brandingBrowserTitle.trim().length > 100) {
      setBrandingError(t("settings:application.validation.browserTitleMax"));
      return false;
    }

    return true;
  };

  const validateForgotPasswordUrlInput = (): boolean => {
    const trimmed = forgotPasswordUrl.trim();
    if (!trimmed) {
      setForgotPasswordUrlError(undefined);
      return true;
    }

    if (!/^https?:\/\//i.test(trimmed) || trimmed.length > 500) {
      setForgotPasswordUrlError(t("settings:application.validation.forgotPasswordUrlInvalid"));
      return false;
    }

    setForgotPasswordUrlError(undefined);
    return true;
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
    setBrandingError(undefined);
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

    const trimmedForgotPasswordUrl = forgotPasswordUrl.trim();

    updateBrandingMutation.mutate({
      items: [
        {
          key: BRANDING_APPLICATION_NAME_KEY,
          value: brandingApplicationName.trim() || "SAS Portal v2",
          valueType: SETTING_VALUE_TYPE_STRING,
        },
        {
          key: BRANDING_BROWSER_TITLE_KEY,
          value: brandingBrowserTitle.trim() || "SAS Portal v2",
          valueType: SETTING_VALUE_TYPE_STRING,
        },
        {
          key: BRANDING_LOGO_URL_KEY,
          value: logoUrlToPersist,
          valueType: SETTING_VALUE_TYPE_STRING,
        },
        {
          key: BRANDING_FAVICON_URL_KEY,
          value: faviconUrlToPersist,
          valueType: SETTING_VALUE_TYPE_STRING,
        },
        {
          key: BRANDING_FORGOT_PASSWORD_URL_KEY,
          value: trimmedForgotPasswordUrl || null,
          valueType: SETTING_VALUE_TYPE_STRING,
        },
      ],
    });
  };

  const handleDirectorySave = () => {
    if (!canUpdate) return;
    setDirectoryError(undefined);

    updateDirectoryMutation.mutate({
      items: [
        {
          key: DIRECTORY_NATIONAL_ID_ATTRIBUTE_KEY,
          value: nationalIdAttribute.trim() || null,
          valueType: SETTING_VALUE_TYPE_STRING,
        },
      ],
    });
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
          <TabsTrigger value="ldap">{t("settings:tabs.ldap")}</TabsTrigger>
          <TabsTrigger value="branding">{t("settings:tabs.branding")}</TabsTrigger>
          <TabsTrigger value="directory">{t("settings:tabs.directory")}</TabsTrigger>
          <TabsTrigger value="sessionSecurity">{t("settings:tabs.sessionSecurity")}</TabsTrigger>
        </TabsList>

        <TabsContent value="ldap">
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
        </TabsContent>

        <TabsContent value="branding">
          <SectionCard
            title={t("settings:application.brandingSectionTitle")}
            description={t("settings:application.sectionDescription")}
          >
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
              onApplicationNameChange={(value) => {
                setBrandingError(undefined);
                setBrandingApplicationName(value);
              }}
              onBrowserTitleChange={(value) => {
                setBrandingError(undefined);
                setBrandingBrowserTitle(value);
              }}
              onSelectLogo={handleLogoSelect}
              onSelectFavicon={handleFaviconSelect}
              onForgotPasswordUrlChange={(value) => {
                setForgotPasswordUrlError(undefined);
                setForgotPasswordUrl(value);
              }}
              onSave={() => void handleBrandingSave()}
            />
          </SectionCard>
        </TabsContent>

        <TabsContent value="directory">
          <SectionCard
            title={t("settings:directory.sectionTitle")}
            description={t("settings:directory.sectionDescription")}
          >
            <DirectorySettingsForm
              nationalIdAttribute={nationalIdAttribute}
              readOnly={isReadOnly}
              isSaving={updateDirectoryMutation.isPending}
              errorMessage={directoryError}
              onNationalIdAttributeChange={(value) => {
                setDirectoryError(undefined);
                setNationalIdAttribute(value);
              }}
              onSave={handleDirectorySave}
            />
          </SectionCard>
        </TabsContent>

        <TabsContent value="sessionSecurity">
          <SectionCard
            title={t("settings:sessionSecurity.title")}
            description={t("settings:sessionSecurity.description")}
          >
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
      </Tabs>
    </section>
  );
}
