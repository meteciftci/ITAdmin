import { useCallback, useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { LoadingState } from "@/components/common/LoadingState";
import { PageContainer } from "@/components/common/PageContainer";
import { PageHeader } from "@/components/common/PageHeader";
import { UnsavedChangesGuard } from "@/components/common/settings-form";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  ApplicationSettingsForm,
} from "@/features/settings/components/ApplicationSettingsForm";
import { isBrandingFormDirty } from "@/features/settings/application-settings-model";
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
import { PermissionCodes } from "@/lib/permission-codes";

const SETTINGS_BASE_PATH = "/settings/application";

export function ApplicationSettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const currentUser = useAuthStore((state) => state.user);
  const canViewSystemSettings = canAccess(currentUser, PermissionCodes.Settings.View);
  const canUpdateSystemSettings = canAccess(currentUser, PermissionCodes.Settings.Update);
  const isSystemReadOnly = !canUpdateSystemSettings;

  const [activeTab, setActiveTab] = useState<ApplicationSettingsTabValue>(
    DEFAULT_APPLICATION_SETTINGS_TAB,
  );
  const [pendingTab, setPendingTab] = useState<ApplicationSettingsTabValue | null>(null);
  const [sessionSecurityDirty, setSessionSecurityDirty] = useState(false);

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
    ldapFormIsDirty,
    ldapConfigurationFingerprint,
  } = useLdapSettingsForm({ t });

  const {
    saveLdapSettings,
    testCandidateLdapSettings,
    testSavedLdapSettings,
    canSaveLdap,
    isSavingLdap,
    isTestingCandidateLdap,
    isTestingSavedLdap,
    candidateLdapValidation,
    savedLdapValidation,
    candidateLdapValidationIsCurrent,
    ldapSaveError,
    ldapSaveSucceeded,
  } = useLdapSettingsSave({
    t,
    canUpdate: canUpdateSystemSettings,
    ldapFormIsMinimumValid,
    ldapConfigurationFingerprint,
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
    applicationNameError,
    browserTitleError,
    footerTextError,
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

  const {
    saveBrandingSettings,
    isSavingBranding,
    brandingSaveError,
    brandingSaveSucceeded,
    clearBrandingSaveState,
  } = useBrandingSettingsSave({
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

  const {
    saveSessionSecuritySettings,
    isSavingSessionSecurity,
    sessionSecuritySaveError,
    sessionSecuritySaveSucceeded,
    clearSessionSecuritySaveState,
  } =
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

  const brandingIsDirty = isBrandingFormDirty(
    {
      applicationName: brandingApplicationName,
      browserTitle: brandingBrowserTitle,
      forgotPasswordUrl,
      footerText,
    },
    settingsQuery.data?.branding,
    Boolean(logoFile || faviconFile),
  );
  const activeTabIsDirty =
    activeTab === "branding" ? brandingIsDirty :
      activeTab === "sessionSecurity" ? sessionSecurityDirty : ldapFormIsDirty;

  const handleTabChange = useCallback((value: string) => {
    const nextTab = value as ApplicationSettingsTabValue;
    if (nextTab === activeTab) return;
    if (activeTabIsDirty) {
      setPendingTab(nextTab);
      return;
    }
    setActiveTab(nextTab);
  }, [activeTab, activeTabIsDirty]);

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
      <Button variant="outline" onClick={() => void settingsQuery.refetch()} disabled={activeTabIsDirty}>
        {t("common:actions.refresh")}
      </Button>
    ),
    [activeTabIsDirty, settingsQuery, t],
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
      <PageContainer variant="form">
        <PageHeader
          title={t("settings:pages.application.title")}
          description={t("settings:pages.application.description")}
          actions={refreshAction}
        />
        <LoadingState />
      </PageContainer>
    );
  }

  return (
    <PageContainer variant="form">
      <UnsavedChangesGuard
        when={activeTabIsDirty}
        title={t("settings:unsaved.title")}
        description={t("settings:unsaved.description")}
        leaveText={t("settings:unsaved.leave")}
        stayText={t("settings:unsaved.stay")}
      />
      <PageHeader
        title={t("settings:pages.application.title")}
        description={t("settings:pages.application.description")}
        actions={refreshAction}
      />

      {isSystemReadOnly ? (
        <Alert><AlertDescription>{t("settings:readOnlyNotice")}</AlertDescription></Alert>
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
              isDirty={brandingIsDirty}
              saveSucceeded={brandingSaveSucceeded}
              errorMessage={brandingError ?? brandingSaveError}
              forgotPasswordUrlError={forgotPasswordUrlError}
              applicationNameError={applicationNameError}
              browserTitleError={browserTitleError}
              footerTextError={footerTextError}
              onApplicationNameChange={(value) => { clearBrandingSaveState(); updateApplicationName(value); }}
              onBrowserTitleChange={(value) => { clearBrandingSaveState(); updateBrowserTitle(value); }}
              onSelectLogo={(file) => { clearBrandingSaveState(); void handleLogoSelect(file); }}
              onSelectFavicon={(file) => { clearBrandingSaveState(); void handleFaviconSelect(file); }}
              onForgotPasswordUrlChange={(value) => { clearBrandingSaveState(); updateForgotPasswordUrl(value); }}
              onFooterTextChange={(value) => { clearBrandingSaveState(); updateFooterText(value); }}
              onSave={() => void saveBrandingSettings()}
            />
        </TabsContent>

        <TabsContent value="sessionSecurity">
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
                saveError={sessionSecuritySaveError}
                saveSucceeded={sessionSecuritySaveSucceeded}
                onDirtyChange={setSessionSecurityDirty}
                onChange={clearSessionSecuritySaveState}
                onSubmit={saveSessionSecuritySettings}
              />
            ) : null}
        </TabsContent>

        <TabsContent value="ldap">
            <LdapSettingsForm
              values={ldapForm}
              fieldErrors={ldapFieldErrors}
              hasBindPassword={hasBindPassword}
              hasSavedConfiguration={Boolean(settingsQuery.data?.ldap)}
              readOnly={isSystemReadOnly}
              savePending={isSavingLdap}
              testCandidatePending={isTestingCandidateLdap}
              testSavedPending={isTestingSavedLdap}
              canSave={canSaveLdap}
              isDirty={ldapFormIsDirty}
              candidateValidationIsCurrent={candidateLdapValidationIsCurrent}
              candidateValidation={candidateLdapValidation}
              savedValidation={savedLdapValidation}
              saveError={ldapSaveError}
              saveSucceeded={ldapSaveSucceeded}
              onChange={updateField}
              onTestCandidate={() => void testCandidateLdapSettings()}
              onTestSaved={() => void testSavedLdapSettings()}
              onSave={saveLdapSettings}
            />
        </TabsContent>
      </Tabs>

      <ConfirmDialog
        open={pendingTab !== null}
        title={t("settings:unsaved.title")}
        description={t("settings:unsaved.description")}
        confirmText={t("settings:unsaved.leave")}
        cancelText={t("settings:unsaved.stay")}
        variant="danger"
        onConfirm={() => {
          if (pendingTab) setActiveTab(pendingTab);
          setPendingTab(null);
        }}
        onOpenChange={(open) => { if (!open) setPendingTab(null); }}
      />
    </PageContainer>
  );
}
