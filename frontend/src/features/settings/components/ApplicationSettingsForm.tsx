import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { BRANDING_FOOTER_TEXT_MAX_LENGTH } from "@/features/settings/settings-constants";
import { resolveApiAssetUrl } from "@/lib/api-client";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";

type FaviconPreviewImageProps = {
  src: string;
  alt: string;
};

function FaviconPreviewImage({ src, alt }: FaviconPreviewImageProps) {
  const [isFailed, setIsFailed] = useState(false);

  if (isFailed) {
    return (
      <span className="px-1 text-center text-[10px] text-muted-foreground">{alt}</span>
    );
  }

  return (
    <img
      src={src}
      alt={alt}
      className="h-full w-full object-contain"
      onError={() => setIsFailed(true)}
    />
  );
}

type ApplicationSettingsFormProps = {
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
  errorMessage?: string;
  forgotPasswordUrlError?: string;
  onApplicationNameChange: (value: string) => void;
  onBrowserTitleChange: (value: string) => void;
  onSelectLogo: (file: File | null) => void;
  onSelectFavicon: (file: File | null) => void;
  onForgotPasswordUrlChange: (value: string) => void;
  onFooterTextChange: (value: string) => void;
  onSave: () => void;
};

export function ApplicationSettingsForm({
  applicationName,
  browserTitle,
  selectedLogoPreviewUrl,
  currentLogoUrl,
  selectedLogoFileName,
  selectedFaviconPreviewUrl,
  currentFaviconUrl,
  selectedFaviconFileName,
  forgotPasswordUrl,
  footerText,
  readOnly,
  isSaving,
  errorMessage,
  forgotPasswordUrlError,
  onApplicationNameChange,
  onBrowserTitleChange,
  onSelectLogo,
  onSelectFavicon,
  onForgotPasswordUrlChange,
  onFooterTextChange,
  onSave,
}: ApplicationSettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);
  const logoInputRef = useRef<HTMLInputElement | null>(null);
  const faviconInputRef = useRef<HTMLInputElement | null>(null);
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
        <div className="space-y-1.5 md:col-span-2">
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
        <div className="space-y-1.5 md:col-span-2">
          <Label>{t("settings:application.fields.footerText")}</Label>
          <Input
            value={footerText}
            onChange={(event) => onFooterTextChange(event.target.value)}
            readOnly={readOnly}
            maxLength={BRANDING_FOOTER_TEXT_MAX_LENGTH}
          />
          <p className="text-xs text-muted-foreground">{t("settings:application.help.footerText")}</p>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-3 rounded-lg border bg-muted/20 p-4">
          <div className="space-y-1">
            <Label>{t("settings:application.fields.logo")}</Label>
            <p className="text-xs text-muted-foreground">{t("settings:application.help.logoFileTypes")}</p>
          </div>
          <div className="flex items-start gap-3">
            <div className="flex h-20 w-20 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-background">
              {displayLogoUrl ? (
                <img
                  src={displayLogoUrl}
                  alt={t("settings:application.logoPreview")}
                  className="h-full w-full object-contain"
                />
              ) : (
                <span className="px-1 text-center text-[10px] text-muted-foreground">{t("settings:application.logoPreview")}</span>
              )}
            </div>
            <div className="min-w-0 space-y-2">
              {!readOnly ? (
                <>
                  <Input
                    ref={logoInputRef}
                    type="file"
                    accept=".png,.jpg,.jpeg,image/png,image/jpeg"
                    className="hidden"
                    onChange={(event) => {
                      const file = event.currentTarget.files?.[0] ?? null;
                      onSelectLogo(file);
                      event.currentTarget.value = "";
                    }}
                    disabled={isSaving}
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => logoInputRef.current?.click()}
                    disabled={isSaving}
                  >
                    {t("settings:application.actions.selectLogoFile")}
                  </Button>
                </>
              ) : null}
              <p className="max-w-[180px] truncate text-xs text-muted-foreground">
                {selectedLogoFileName
                  ? `${t("settings:application.selectedFile")}: ${selectedLogoFileName}`
                  : t("settings:application.noFileSelected")}
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-3 rounded-lg border bg-muted/20 p-4">
          <div className="space-y-1">
            <Label>{t("settings:application.fields.favicon")}</Label>
            <p className="text-xs text-muted-foreground">{t("settings:application.help.faviconFileTypes")}</p>
          </div>
          <div className="flex items-start gap-3">
            <div className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-background">
              {displayFaviconUrl ? (
                <FaviconPreviewImage
                  key={displayFaviconUrl}
                  src={displayFaviconUrl}
                  alt={t("settings:application.faviconPreview")}
                />
              ) : (
                <span className="px-1 text-center text-[10px] text-muted-foreground">{t("settings:application.faviconPreview")}</span>
              )}
            </div>
            <div className="min-w-0 space-y-2">
              {!readOnly ? (
                <>
                  <Input
                    ref={faviconInputRef}
                    type="file"
                    accept=".png,.jpg,.jpeg,.ico,image/png,image/jpeg,image/x-icon,image/vnd.microsoft.icon"
                    className="hidden"
                    onChange={(event) => {
                      const file = event.currentTarget.files?.[0] ?? null;
                      onSelectFavicon(file);
                      event.currentTarget.value = "";
                    }}
                    disabled={isSaving}
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => faviconInputRef.current?.click()}
                    disabled={isSaving}
                  >
                    {t("settings:application.actions.selectFaviconFile")}
                  </Button>
                </>
              ) : null}
              <p className="max-w-[180px] truncate text-xs text-muted-foreground">
                {selectedFaviconFileName
                  ? `${t("settings:application.selectedFile")}: ${selectedFaviconFileName}`
                  : t("settings:application.noFileSelected")}
              </p>
            </div>
          </div>
        </div>
      </div>

      {errorMessage ? <p className="text-xs text-destructive">{errorMessage}</p> : null}

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
