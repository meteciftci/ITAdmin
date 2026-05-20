import { useCallback, useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";

import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  ApplicationSettingsForm,
} from "@/features/settings/components/ApplicationSettingsForm";
import { LdapSettingsForm } from "@/features/settings/components/LdapSettingsForm";
import { SessionSecuritySettingsForm } from "@/features/settings/components/SessionSecuritySettingsForm";
import { getSettings } from "@/features/settings/api";
import {
  DEFAULT_SESSION_SECURITY,
  SETTINGS_QUERY_KEY,
  type ApplicationSettingsTabValue,
  DEFAULT_APPLICATION_SETTINGS_TAB,
} from "@/features/settings/settings-constants";
import { useBrandingAssetSettingsForm } from "@/features/settings/hooks/useBrandingAssetSettingsForm";
import { useBrandingSettingsForm } from "@/features/settings/hooks/useBrandingSettingsForm";
import { useBrandingSettingsSave } from "@/features/settings/hooks/useBrandingSettingsSave";
import { useLdapSettingsForm } from "@/features/settings/hooks/useLdapSettingsForm";
import { useLdapSettingsSave } from "@/features/settings/hooks/useLdapSettingsSave";
import { useSessionSecuritySettingsSave } from "@/features/settings/hooks/useSessionSecuritySettingsSave";
import { sessionSecurityFingerprint } from "@/features/settings/settings-utils";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { useTranslation } from "react-i18next";

const SETTINGS_BASE_PATH = "/settings/application";

export function ApplicationSettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const currentUser = useAuthStore((state) => state.user);
  const canViewSystemSettings = canAccess(currentUser, "Settings.View");
  const canUpdateSystemSettings = canAccess(currentUser, "Settings.Update");
  const isSystemReadOnly = !canUpdateSystemSettings;

  const [activeTab, setActiveTab] = useState<ApplicationSettingsTabValue>(
    DEFAULT_APPLICATION_SETTINGS_TAB,
  );

  const handleTabChange = useCallback((value: string) => {
    setActiveTab(value as ApplicationSettingsTabValue);
  }, []);

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

  const { saveLdapSettings, canSaveLdap, isSavingLdap } = useLdapSettingsSave({
    t,
    canUpdate: canUpdateSystemSettings,
    ldapFormIsMinimumValid,
    validateLdapForm,
    buildLdapPayload,
    buildLdapValidatePayload,
    clearBindPasswordAfterSave,
  });

  const {
    brandingApplicationName,
    brandingBrowserTitle,
    forgotPasswordUrl,
    footerText,
    forgotPasswordUrlError,
    brandingError,
    hydrateFromBranding,
    updateApplicationName,
    updateBrowserTitle,
    updateForgotPasswordUrl,
    updateFooterText,
    clearBrandingError,
    clearForgotPasswordUrlError,
    validateBrandingInput,
    validateForgotPasswordUrlInput,
    buildBrandingPayload,
  } = useBrandingSettingsForm({ t });

  const {
    brandingLogoUrl,
    logoFile,
    logoPreviewUrl,
    selectedLogoFileName,
    brandingFaviconUrl,
    faviconFile,
    faviconPreviewUrl,
    selectedFaviconFileName,
    hydrateAssetUrlsFromBranding,
    handleLogoSelect,
    handleFaviconSelect,
    resetSelectedAssetsAfterSave,
  } = useBrandingAssetSettingsForm({ t });

  const { saveBrandingSettings, isSavingBranding } = useBrandingSettingsSave({
    t,
    canUpdate: canUpdateSystemSettings,
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
  });

  const { saveSessionSecuritySettings, isSavingSessionSecurity } =
    useSessionSecuritySettingsSave({
      t,
      canUpdate: canUpdateSystemSettings,
    });

  const settingsQuery = useQuery({
    queryKey: SETTINGS_QUERY_KEY,
    queryFn: getSettings,
    enabled: canViewSystemSettings,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  useEffect(() => {
    if (!settingsQuery.data) return;
    const ldap = settingsQuery.data.ldap;

    hydrateFromSettings(ldap);
    hydrateFromBranding(settingsQuery.data.branding, settingsQuery.data.applicationSettings);
    hydrateAssetUrlsFromBranding(settingsQuery.data.branding);
  }, [
    settingsQuery.data,
    hydrateAssetUrlsFromBranding,
    hydrateFromBranding,
    hydrateFromSettings,
  ]);

  const refreshAction = useMemo(
    () => (
      <Button variant="outline" onClick={() => void settingsQuery.refetch()}>
        {t("common:actions.refresh")}
      </Button>
    ),
    [settingsQuery, t],
  );

  if (settingsQuery.isError) {
    const routeState = createApiErrorRouteState(settingsQuery.error, {
      fromPath: SETTINGS_BASE_PATH,
      retryPath: SETTINGS_BASE_PATH,
      sourceLabel: t("settings:pages.application.title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  if (settingsQuery.isLoading) {
    return (
      <section className="space-y-4">
        <PageHeader
          title={t("settings:pages.application.title")}
          description={t("settings:pages.application.description")}
          actions={refreshAction}
        />
        <LoadingState />
      </section>
    );
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("settings:pages.application.title")}
        description={t("settings:pages.application.description")}
        actions={refreshAction}
      />

      {isSystemReadOnly ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("settings:readOnlyNotice")}
        </p>
      ) : null}

      <Tabs value={activeTab} onValueChange={handleTabChange}>
        <TabsList className="grid w-full grid-cols-1 sm:grid-cols-3">
          <TabsTrigger value="branding">{t("settings:tabs.branding")}</TabsTrigger>
          <TabsTrigger value="sessionSecurity">
            {t("settings:tabs.sessionSecurity")}
          </TabsTrigger>
          <TabsTrigger value="ldap">{t("settings:tabs.ldap")}</TabsTrigger>
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
              footerText={footerText}
              readOnly={isSystemReadOnly}
              isSaving={isSavingBranding}
              errorMessage={brandingError}
              forgotPasswordUrlError={forgotPasswordUrlError}
              onApplicationNameChange={updateApplicationName}
              onBrowserTitleChange={updateBrowserTitle}
              onSelectLogo={handleLogoSelect}
              onSelectFavicon={handleFaviconSelect}
              onForgotPasswordUrlChange={updateForgotPasswordUrl}
              onFooterTextChange={updateFooterText}
              onSave={() => void saveBrandingSettings()}
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
                readOnly={isSystemReadOnly}
                isSaving={isSavingSessionSecurity}
                onSubmit={saveSessionSecuritySettings}
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
              readOnly={isSystemReadOnly}
              savePending={isSavingLdap}
              canSave={canSaveLdap}
              onChange={updateField}
              onSave={saveLdapSettings}
            />
          </SectionCard>
        </TabsContent>
      </Tabs>
    </section>
  );
}
