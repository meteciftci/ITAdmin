import { useEffect, useMemo, useState, useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";

import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { AdManagementSettingsTab } from "@/features/ad-management/AdManagementSettingsTab";
import { useAuthStore } from "@/features/auth/auth-store";
import { getSettings } from "@/features/settings/api";
import {
  ApplicationSettingsForm,
} from "@/features/settings/components/ApplicationSettingsForm";
import { LdapSettingsForm } from "@/features/settings/components/LdapSettingsForm";
import { SessionSecuritySettingsForm } from "@/features/settings/components/SessionSecuritySettingsForm";
import {
  DEFAULT_SESSION_SECURITY,
  SETTINGS_QUERY_KEY,
  isSettingsTabVisible,
  resolveDefaultSettingsTab,
  type SettingsTabValue,
} from "@/features/settings/settings-constants";
import { useBrandingAssetSettingsForm } from "@/features/settings/hooks/useBrandingAssetSettingsForm";
import { useBrandingSettingsForm } from "@/features/settings/hooks/useBrandingSettingsForm";
import { useBrandingSettingsSave } from "@/features/settings/hooks/useBrandingSettingsSave";
import { useLdapSettingsForm } from "@/features/settings/hooks/useLdapSettingsForm";
import { useLdapSettingsSave } from "@/features/settings/hooks/useLdapSettingsSave";
import { useSessionSecuritySettingsSave } from "@/features/settings/hooks/useSessionSecuritySettingsSave";
import { sessionSecurityFingerprint } from "@/features/settings/settings-utils";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { canAccess } from "@/lib/permissions";
import { useTranslation } from "react-i18next";

export function SettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const currentUser = useAuthStore((state) => state.user);
  const canViewSystemSettings = canAccess(currentUser, "Settings.View");
  const canViewAdManagementSettings = canAccess(currentUser, "AdManagement.Settings.View");
  const canUpdateSystemSettings = canAccess(currentUser, "Settings.Update");
  const canUpdateAdManagementSettings = canAccess(currentUser, "AdManagement.Settings.Update");
  const isSystemReadOnly = !canUpdateSystemSettings;

  const defaultTab = useMemo(
    () => resolveDefaultSettingsTab(canViewSystemSettings, canViewAdManagementSettings),
    [canViewAdManagementSettings, canViewSystemSettings],
  );

  const [activeTab, setActiveTab] = useState<SettingsTabValue>(defaultTab);

  const visibleTab = useMemo(() => {
    if (isSettingsTabVisible(activeTab, canViewSystemSettings, canViewAdManagementSettings)) {
      return activeTab;
    }

    return defaultTab;
  }, [activeTab, canViewAdManagementSettings, canViewSystemSettings, defaultTab]);

  const handleTabChange = useCallback(
    (value: string) => {
      const nextTab = value as SettingsTabValue;
      if (!isSettingsTabVisible(nextTab, canViewSystemSettings, canViewAdManagementSettings)) {
        return;
      }

      setActiveTab(nextTab);
    },
    [canViewAdManagementSettings, canViewSystemSettings],
  );

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

  const visibleTabCount = useMemo(() => {
    let count = 0;
    if (canViewSystemSettings) count += 3;
    if (canViewAdManagementSettings) count += 1;
    return count;
  }, [canViewAdManagementSettings, canViewSystemSettings]);

  const tabsListClassName = useMemo(() => {
    if (visibleTabCount <= 1) {
      return "grid w-full grid-cols-1";
    }

    if (visibleTabCount === 2) {
      return "grid w-full grid-cols-1 sm:grid-cols-2";
    }

    if (visibleTabCount === 3) {
      return "grid w-full grid-cols-1 sm:grid-cols-3";
    }

    return "grid w-full grid-cols-1 sm:grid-cols-4";
  }, [visibleTabCount]);

  const refreshAction = useMemo(
    () => (
      <Button variant="outline" onClick={() => void settingsQuery.refetch()}>
        {t("common:actions.refresh")}
      </Button>
    ),
    [settingsQuery, t],
  );

  if (canViewSystemSettings && settingsQuery.isError) {
    const routeState = createApiErrorRouteState(settingsQuery.error, {
      fromPath: "/settings",
      retryPath: "/settings",
      sourceLabel: t("settings:title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  if (canViewSystemSettings && settingsQuery.isLoading) {
    return (
      <section className="space-y-4">
        <div className="flex justify-end">{refreshAction}</div>
        <LoadingState />
      </section>
    );
  }

  return (
    <section className="space-y-4">
      {canViewSystemSettings ? (
        <div className="flex justify-end">{refreshAction}</div>
      ) : null}

      {canViewSystemSettings && isSystemReadOnly ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("settings:readOnlyNotice")}
        </p>
      ) : null}

      {canViewAdManagementSettings && !canUpdateAdManagementSettings ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("settings:adManagement.readOnlyNotice")}
        </p>
      ) : null}

      <Tabs value={visibleTab} onValueChange={handleTabChange}>
        <TabsList className={tabsListClassName}>
          {canViewSystemSettings ? (
            <>
              <TabsTrigger value="branding">{t("settings:tabs.branding")}</TabsTrigger>
              <TabsTrigger value="sessionSecurity">
                {t("settings:tabs.sessionSecurity")}
              </TabsTrigger>
              <TabsTrigger value="ldap">{t("settings:tabs.ldap")}</TabsTrigger>
            </>
          ) : null}
          {canViewAdManagementSettings ? (
            <TabsTrigger value="directory">{t("settings:tabs.directory")}</TabsTrigger>
          ) : null}
        </TabsList>

        {canViewSystemSettings ? (
          <>
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
          </>
        ) : null}

        {canViewAdManagementSettings ? (
          <TabsContent value="directory">
            <SectionCard>
              <AdManagementSettingsTab readOnly={!canUpdateAdManagementSettings} />
            </SectionCard>
          </TabsContent>
        ) : null}
      </Tabs>
    </section>
  );
}
