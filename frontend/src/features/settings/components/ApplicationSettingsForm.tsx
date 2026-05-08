import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { resolveApiAssetUrl } from "@/lib/api-client";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";

type ApplicationSettingsFormProps = {
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
  onApplicationNameChange: (value: string) => void;
  onBrowserTitleChange: (value: string) => void;
  onSelectLogo: (file: File | null) => void;
  onSelectFavicon: (file: File | null) => void;
  onForgotPasswordUrlChange: (value: string) => void;
  onSave: () => void;
};

export function ApplicationSettingsForm({
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
  onApplicationNameChange,
  onBrowserTitleChange,
  onSelectLogo,
  onSelectFavicon,
  onForgotPasswordUrlChange,
  onSave,
}: ApplicationSettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);
  const logoInputRef = useRef<HTMLInputElement | null>(null);
  const faviconInputRef = useRef<HTMLInputElement | null>(null);
  const [selectedLogoFileName, setSelectedLogoFileName] = useState<string | null>(null);
  const [selectedFaviconFileName, setSelectedFaviconFileName] = useState<string | null>(null);
  const [faviconPreviewFailedUrl, setFaviconPreviewFailedUrl] = useState<string | null>(null);
  const resolvedCurrentLogoUrl = resolveApiAssetUrl(currentLogoUrl);
  const displayLogoUrl = selectedLogoPreviewUrl ?? resolvedCurrentLogoUrl;
  const resolvedCurrentFaviconUrl = resolveApiAssetUrl(currentFaviconUrl);
  const displayFaviconUrl = selectedFaviconPreviewUrl ?? resolvedCurrentFaviconUrl;
  const canShowFaviconPreview =
    Boolean(displayFaviconUrl) && faviconPreviewFailedUrl !== displayFaviconUrl;

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
                      const file = event.target.files?.[0] ?? null;
                      setSelectedLogoFileName(file?.name ?? null);
                      onSelectLogo(file);
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
              {canShowFaviconPreview ? (
                <img
                  src={displayFaviconUrl ?? ""}
                  alt={t("settings:application.faviconPreview")}
                  className="h-full w-full object-contain"
                  onError={() => setFaviconPreviewFailedUrl(displayFaviconUrl)}
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
                      const file = event.target.files?.[0] ?? null;
                      setSelectedFaviconFileName(file?.name ?? null);
                      setFaviconPreviewFailedUrl(null);
                      onSelectFavicon(file);
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
