import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useTranslation } from "react-i18next";

type ApplicationSettingsFormProps = {
  nationalIdAttribute: string;
  readOnly: boolean;
  isSaving: boolean;
  errorMessage?: string;
  onNationalIdAttributeChange: (value: string) => void;
  onSave: () => void;
};

export function ApplicationSettingsForm({
  nationalIdAttribute,
  readOnly,
  isSaving,
  errorMessage,
  onNationalIdAttributeChange,
  onSave,
}: ApplicationSettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);

  return (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2">
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
