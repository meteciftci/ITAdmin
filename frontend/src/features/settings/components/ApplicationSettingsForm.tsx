import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useTranslation } from "react-i18next";

type ApplicationSettingsFormProps = {
  nationalIdAttribute: string;
  applicationName: string;
  browserTitle: string;
  logoPreviewUrl: string | null;
  readOnly: boolean;
  isSaving: boolean;
  errorMessage?: string;
  onNationalIdAttributeChange: (value: string) => void;
  onApplicationNameChange: (value: string) => void;
  onBrowserTitleChange: (value: string) => void;
  onSelectLogo: (file: File | null) => void;
  onSave: () => void;
};

export function ApplicationSettingsForm({
  nationalIdAttribute,
  applicationName,
  browserTitle,
  logoPreviewUrl,
  readOnly,
  isSaving,
  errorMessage,
  onNationalIdAttributeChange,
  onApplicationNameChange,
  onBrowserTitleChange,
  onSelectLogo,
  onSave,
}: ApplicationSettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);

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
      </div>

      <div className="space-y-2">
        <Label>{t("settings:application.fields.logo")}</Label>
        <div className="flex items-center gap-4">
          <div className="flex h-20 w-20 items-center justify-center overflow-hidden rounded-md border bg-muted">
            {logoPreviewUrl ? (
              <img src={logoPreviewUrl} alt={t("settings:application.logoPreview")} className="h-full w-full object-contain" />
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
