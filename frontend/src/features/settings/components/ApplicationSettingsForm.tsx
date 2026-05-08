import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { resolveApiAssetUrl } from "@/lib/api-client";
import { useTranslation } from "react-i18next";

type ApplicationSettingsFormProps = {
  nationalIdAttribute: string;
  applicationName: string;
  browserTitle: string;
  selectedLogoPreviewUrl: string | null;
  currentLogoUrl: string | null;
  selectedFaviconPreviewUrl: string | null;
  currentFaviconUrl: string | null;
  forgotPasswordUrl: string;
  readOnly: boolean;
  isSaving: boolean;
  errorMessage?: string;
  forgotPasswordUrlError?: string;
  onNationalIdAttributeChange: (value: string) => void;
  onApplicationNameChange: (value: string) => void;
  onBrowserTitleChange: (value: string) => void;
  onSelectLogo: (file: File | null) => void;
  onSelectFavicon: (file: File | null) => void;
  onForgotPasswordUrlChange: (value: string) => void;
  onSave: () => void;
};

export function ApplicationSettingsForm({
  nationalIdAttribute,
  applicationName,
  browserTitle,
  selectedLogoPreviewUrl,
  currentLogoUrl,
  selectedFaviconPreviewUrl,
  currentFaviconUrl,
  forgotPasswordUrl,
  readOnly,
  isSaving,
  errorMessage,
  forgotPasswordUrlError,
  onNationalIdAttributeChange,
  onApplicationNameChange,
  onBrowserTitleChange,
  onSelectLogo,
  onSelectFavicon,
  onForgotPasswordUrlChange,
  onSave,
}: ApplicationSettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);
  const resolvedCurrentLogoUrl = resolveApiAssetUrl(currentLogoUrl);
  const displayLogoUrl = selectedLogoPreviewUrl ?? resolvedCurrentLogoUrl;
  const resolvedCurrentFaviconUrl = resolveApiAssetUrl(currentFaviconUrl);
  const displayFaviconUrl = selectedFaviconPreviewUrl ?? resolvedCurrentFaviconUrl;

  return (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-1.5">
          <Label>{t("settings:application.fields.applicationName")}</Label>
          <Input
            value={applicationName}
            onChange={(event) => onApplicationNameChange(event.target.value)}
            readOnly={readOnly}
            maxLength={100}
          />
        </div>
        <div className="space-y-1.5">
          <Label>{t("settings:application.fields.browserTitle")}</Label>
          <Input
            value={browserTitle}
            onChange={(event) => onBrowserTitleChange(event.target.value)}
            readOnly={readOnly}
            maxLength={100}
          />
        </div>
        <div className="space-y-1.5">
          <Label>{t("settings:application.fields.nationalIdAttribute")}</Label>
          <Input
            value={nationalIdAttribute}
            onChange={(event) => onNationalIdAttributeChange(event.target.value)}
            readOnly={readOnly}
          />
          {errorMessage ? <p className="text-xs text-destructive">{errorMessage}</p> : null}
        </div>
        <div className="space-y-1.5">
          <Label>{t("settings:application.fields.forgotPasswordUrl")}</Label>
          <Input
            type="url"
            value={forgotPasswordUrl}
            onChange={(event) => onForgotPasswordUrlChange(event.target.value)}
            readOnly={readOnly}
            maxLength={500}
            placeholder="https://"
          />
          {forgotPasswordUrlError ? (
            <p className="text-xs text-destructive">{forgotPasswordUrlError}</p>
          ) : null}
        </div>
      </div>

      <div className="space-y-2">
        <Label>{t("settings:application.fields.logo")}</Label>
        <div className="flex items-center gap-4">
          <div className="flex h-20 w-20 items-center justify-center overflow-hidden rounded-md border bg-muted">
            {displayLogoUrl ? (
              <img src={displayLogoUrl} alt={t("settings:application.logoPreview")} className="h-full w-full object-contain" />
            ) : (
              <span className="text-xs text-muted-foreground">{t("settings:application.logoPreview")}</span>
            )}
          </div>
          {!readOnly ? (
            <Input
              type="file"
              accept=".png,.jpg,.jpeg,image/png,image/jpeg"
              onChange={(event) => onSelectLogo(event.target.files?.[0] ?? null)}
              disabled={isSaving}
            />
          ) : null}
        </div>
      </div>

      <div className="space-y-2">
        <Label>{t("settings:application.fields.favicon")}</Label>
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center overflow-hidden rounded-md border bg-muted">
            {displayFaviconUrl ? (
              <img
                src={displayFaviconUrl}
                alt={t("settings:application.faviconPreview")}
                className="h-full w-full object-contain"
              />
            ) : (
              <span className="text-[10px] text-muted-foreground">
                {t("settings:application.faviconPreview")}
              </span>
            )}
          </div>
          {!readOnly ? (
            <Input
              type="file"
              accept=".png,.jpg,.jpeg,image/png,image/jpeg"
              onChange={(event) => onSelectFavicon(event.target.files?.[0] ?? null)}
              disabled={isSaving}
            />
          ) : null}
        </div>
      </div>

      {!readOnly ? (
        <div className="flex justify-end">
          <Button onClick={onSave} disabled={isSaving}>
            {t("common:actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
