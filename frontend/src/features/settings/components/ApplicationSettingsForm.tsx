import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import {
  SettingsField,
  SettingsFormActions,
  SettingsSection,
} from "@/components/common/settings-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { BRANDING_FOOTER_TEXT_MAX_LENGTH } from "@/features/settings/settings-constants";
import { resolveApiAssetUrl } from "@/lib/api-client";

function PreviewImage({ src, alt, compact = false }: { src: string; alt: string; compact?: boolean }) {
  const [failed, setFailed] = useState(false);
  if (failed) return <span className="px-2 text-center text-xs text-muted-foreground">{alt}</span>;
  return <img src={src} alt={alt} className={compact ? "size-8 object-contain" : "max-h-16 max-w-40 object-contain"} onError={() => setFailed(true)} />;
}

type Props = {
  applicationName: string;
  browserTitle: string;
  selectedLogoPreviewUrl: string | null;
  currentLogoUrl: string | null;
  selectedLogoFileName: string | null;
  selectedFaviconPreviewUrl: string | null;
  currentFaviconUrl: string | null;
  selectedFaviconFileName: string | null;
  forgotPasswordUrl: string;
  footerText: string;
  readOnly: boolean;
  isSaving: boolean;
  isDirty: boolean;
  saveSucceeded: boolean;
  errorMessage?: string | null;
  forgotPasswordUrlError?: string;
  applicationNameError?: string;
  browserTitleError?: string;
  footerTextError?: string;
  onApplicationNameChange: (value: string) => void;
  onBrowserTitleChange: (value: string) => void;
  onSelectLogo: (file: File | null) => void;
  onSelectFavicon: (file: File | null) => void;
  onForgotPasswordUrlChange: (value: string) => void;
  onFooterTextChange: (value: string) => void;
  onSave: () => void;
};

export function ApplicationSettingsForm({
  applicationName, browserTitle, selectedLogoPreviewUrl, currentLogoUrl,
  selectedLogoFileName, selectedFaviconPreviewUrl, currentFaviconUrl,
  selectedFaviconFileName, forgotPasswordUrl, footerText, readOnly, isSaving,
  isDirty, saveSucceeded, errorMessage, forgotPasswordUrlError,
  applicationNameError, browserTitleError, footerTextError,
  onApplicationNameChange, onBrowserTitleChange, onSelectLogo, onSelectFavicon,
  onForgotPasswordUrlChange, onFooterTextChange, onSave,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const logoInputRef = useRef<HTMLInputElement | null>(null);
  const faviconInputRef = useRef<HTMLInputElement | null>(null);
  const displayLogoUrl = selectedLogoPreviewUrl ?? resolveApiAssetUrl(currentLogoUrl);
  const displayFaviconUrl = selectedFaviconPreviewUrl ?? resolveApiAssetUrl(currentFaviconUrl);
  const actionState = isSaving ? "saving" : errorMessage ? "error" : isDirty ? "dirty" : saveSucceeded ? "saved" : "pristine";

  return (
    <div className="space-y-6">
      <SettingsSection title={t("settings:application.sections.identity")} description={t("settings:application.sections.identityDescription")}>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="branding-application-name" label={t("settings:application.fields.applicationName")} description={t("settings:application.help.applicationName")} error={applicationNameError}>
            <Input id="branding-application-name" value={applicationName} onChange={(event) => onApplicationNameChange(event.target.value)} readOnly={readOnly} maxLength={100} />
          </SettingsField>
          <SettingsField id="branding-browser-title" label={t("settings:application.fields.browserTitle")} description={t("settings:application.help.browserTitle")} error={browserTitleError}>
            <Input id="branding-browser-title" value={browserTitle} onChange={(event) => onBrowserTitleChange(event.target.value)} readOnly={readOnly} maxLength={100} />
          </SettingsField>
        </div>
      </SettingsSection>

      <SettingsSection title={t("settings:application.sections.assets")} description={t("settings:application.sections.assetsDescription")}>
        <div className="grid gap-5 md:grid-cols-2">
          <AssetField
            title={t("settings:application.fields.logo")}
            help={t("settings:application.help.logoFileTypes")}
            preview={displayLogoUrl ? <PreviewImage src={displayLogoUrl} alt={t("settings:application.logoPreview")} /> : null}
            emptyPreview={t("settings:application.logoPreview")}
            selectedFileName={selectedLogoFileName}
            readOnly={readOnly}
            isSaving={isSaving}
            inputRef={logoInputRef}
            accept=".png,.jpg,.jpeg,image/png,image/jpeg"
            actionLabel={t("settings:application.actions.selectLogoFile")}
            onSelect={onSelectLogo}
          />
          <AssetField
            title={t("settings:application.fields.favicon")}
            help={t("settings:application.help.faviconFileTypes")}
            preview={displayFaviconUrl ? <PreviewImage src={displayFaviconUrl} alt={t("settings:application.faviconPreview")} compact /> : null}
            emptyPreview={t("settings:application.faviconPreview")}
            selectedFileName={selectedFaviconFileName}
            readOnly={readOnly}
            isSaving={isSaving}
            inputRef={faviconInputRef}
            accept=".png,.jpg,.jpeg,.ico,image/png,image/jpeg,image/x-icon,image/vnd.microsoft.icon"
            actionLabel={t("settings:application.actions.selectFaviconFile")}
            onSelect={onSelectFavicon}
          />
        </div>
      </SettingsSection>

      <SettingsSection title={t("settings:application.sections.loginAndFooter")} description={t("settings:application.sections.loginAndFooterDescription")}>
        <div className="grid gap-5">
          <SettingsField id="branding-forgot-password" label={t("settings:application.fields.forgotPasswordUrl")} description={t("settings:application.help.forgotPasswordUrl")} optional optionalLabel={t("settings:fields.optional")} error={forgotPasswordUrlError}>
            <Input id="branding-forgot-password" type="url" value={forgotPasswordUrl} onChange={(event) => onForgotPasswordUrlChange(event.target.value)} readOnly={readOnly} maxLength={500} placeholder="https://" />
          </SettingsField>
          <SettingsField id="branding-footer" label={t("settings:application.fields.footerText")} description={t("settings:application.help.footerText")} optional optionalLabel={t("settings:fields.optional")} error={footerTextError}>
            <Input id="branding-footer" value={footerText} onChange={(event) => onFooterTextChange(event.target.value)} readOnly={readOnly} maxLength={BRANDING_FOOTER_TEXT_MAX_LENGTH} />
          </SettingsField>
        </div>
      </SettingsSection>

      {!readOnly ? (
        <SettingsFormActions state={actionState} stateLabel={t(`settings:saveStates.${actionState}`)} errorTitle={t("settings:saveStates.failedTitle")} errorMessage={errorMessage}>
          <Button type="button" onClick={onSave} disabled={isSaving || !isDirty}>
            {isSaving ? t("settings:actions.saving") : t("common:actions.save")}
          </Button>
        </SettingsFormActions>
      ) : null}
    </div>
  );
}

type AssetFieldProps = {
  title: string;
  help: string;
  preview: React.ReactNode;
  emptyPreview: string;
  selectedFileName: string | null;
  readOnly: boolean;
  isSaving: boolean;
  inputRef: React.RefObject<HTMLInputElement | null>;
  accept: string;
  actionLabel: string;
  onSelect: (file: File | null) => void;
};

function AssetField({ title, help, preview, emptyPreview, selectedFileName, readOnly, isSaving, inputRef, accept, actionLabel, onSelect }: AssetFieldProps) {
  const { t } = useTranslation("settings");
  return (
    <div className="space-y-4 rounded-xl border bg-muted/20 p-4">
      <div><h3 className="text-sm font-medium">{title}</h3><p className="mt-1 text-sm text-muted-foreground">{help}</p></div>
      <div className="flex min-h-24 items-center justify-center rounded-lg border bg-background p-4">{preview ?? <span className="text-sm text-muted-foreground">{emptyPreview}</span>}</div>
      {!readOnly ? (
        <>
          <Input ref={inputRef} type="file" accept={accept} className="hidden" tabIndex={-1} onChange={(event) => { onSelect(event.currentTarget.files?.[0] ?? null); event.currentTarget.value = ""; }} disabled={isSaving} />
          <Button type="button" variant="outline" size="sm" onClick={() => inputRef.current?.click()} disabled={isSaving}>{actionLabel}</Button>
        </>
      ) : null}
      <p className="truncate text-sm text-muted-foreground">{selectedFileName ? `${t("application.selectedFile")}: ${selectedFileName}` : t("application.noFileSelected")}</p>
    </div>
  );
}
