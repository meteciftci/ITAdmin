import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useTranslation } from "react-i18next";

type DirectorySettingsFormProps = {
  nationalIdAttribute: string;
  readOnly: boolean;
  isSaving: boolean;
  errorMessage?: string;
  onNationalIdAttributeChange: (value: string) => void;
  onSave: () => void;
};

export function DirectorySettingsForm({
  nationalIdAttribute,
  readOnly,
  isSaving,
  errorMessage,
  onNationalIdAttributeChange,
  onSave,
}: DirectorySettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);

  return (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-1.5 md:col-span-2">
          <Label htmlFor="directory-national-id-attribute">
            {t("settings:directory.fields.nationalIdAttribute")}
          </Label>
          <Input
            id="directory-national-id-attribute"
            value={nationalIdAttribute}
            onChange={(event) => onNationalIdAttributeChange(event.target.value)}
            readOnly={readOnly}
            placeholder="employeeId"
            maxLength={100}
          />
          <p className="text-xs text-muted-foreground">
            {t("settings:directory.fields.nationalIdAttributeHelp")}
          </p>
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
