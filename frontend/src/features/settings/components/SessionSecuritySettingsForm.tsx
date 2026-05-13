import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

import type { SessionSecuritySettings, UpdateSessionSecuritySettingsRequest } from "../types";

const limits = {
  accessTokenMinutes: { min: 5, max: 240 },
  idleTimeoutMinutes: { min: 5, max: 480 },
  idleWarningSeconds: { min: 10, max: 300 },
  sessionRefreshTokenHours: { min: 1, max: 24 },
  rememberMeRefreshTokenDays: { min: 1, max: 30 },
} as const;

type NumericFieldKey = keyof typeof limits;

type FieldKey = keyof SessionSecuritySettings;

type SessionSecuritySettingsFormProps = {
  initialValues: SessionSecuritySettings;
  readOnly: boolean;
  isSaving: boolean;
  onSubmit: (payload: UpdateSessionSecuritySettingsRequest) => void;
};

function collectErrors(
  values: SessionSecuritySettings,
  t: (key: string, options?: Record<string, unknown>) => string,
): Partial<Record<FieldKey, string>> {
  const errors: Partial<Record<FieldKey, string>> = {};

  (Object.keys(limits) as NumericFieldKey[]).forEach((field) => {
    const value = values[field];
    const { min, max } = limits[field];
    if (!Number.isFinite(value) || !Number.isInteger(value) || value < min || value > max) {
      errors[field] = t("settings:sessionSecurity.validation.range", { min, max });
    }
  });

  const idleCap = values.idleTimeoutMinutes * 60;
  if (
    Number.isFinite(values.idleWarningSeconds) &&
    Number.isFinite(values.idleTimeoutMinutes) &&
    values.idleWarningSeconds >= idleCap
  ) {
    errors.idleWarningSeconds = t("settings:sessionSecurity.validation.warningLessThanIdle");
  }

  return errors;
}

export function SessionSecuritySettingsForm({
  initialValues,
  readOnly,
  isSaving,
  onSubmit,
}: SessionSecuritySettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);
  const [values, setValues] = useState<SessionSecuritySettings>(() => initialValues);

  const computedErrors = useMemo(() => collectErrors(values, t), [values, t]);
  const hasBlockingErrors = Object.keys(computedErrors).length > 0;

  const setNumberField = (field: NumericFieldKey, raw: string) => {
    const trimmed = raw.trim();
    if (trimmed === "") {
      setValues((prev) => ({ ...prev, [field]: Number.NaN }));
      return;
    }
    const parsed = Number.parseInt(trimmed, 10);
    setValues((prev) => ({ ...prev, [field]: Number.isNaN(parsed) ? Number.NaN : parsed }));
  };

  const numberDisplay = (field: NumericFieldKey): string =>
    Number.isFinite(values[field]) ? String(values[field]) : "";

  const handleSave = () => {
    if (readOnly) return;
    const errs = collectErrors(values, t);
    if (Object.keys(errs).length > 0) return;
    onSubmit({ ...values });
  };

  const field = (key: NumericFieldKey, unitKey: string) => (
    <div className="space-y-1.5">
      <Label htmlFor={`session-security-${key}`}>{t(`settings:sessionSecurity.fields.${key}.label`)}</Label>
      <Input
        id={`session-security-${key}`}
        type="number"
        className="max-w-[11rem]"
        min={limits[key].min}
        max={limits[key].max}
        step={1}
        value={numberDisplay(key)}
        onChange={(event) => setNumberField(key, event.target.value)}
        readOnly={readOnly}
        disabled={readOnly}
      />
      <p className="text-xs text-muted-foreground">{t(`settings:sessionSecurity.fields.${key}.description`)}</p>
      <p className="text-xs text-muted-foreground">{t(`settings:sessionSecurity.units.${unitKey}`)}</p>
      {computedErrors[key] ? <p className="text-xs text-destructive">{computedErrors[key]}</p> : null}
    </div>
  );

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">{t("settings:sessionSecurity.description")}</p>
      <p className="rounded-md border border-dashed px-3 py-2 text-xs text-muted-foreground">
        {t("settings:sessionSecurity.futureNote")}
      </p>

      <div className="grid gap-4 md:grid-cols-2">
        {field("accessTokenMinutes", "minutes")}
        {field("idleTimeoutMinutes", "minutes")}
        {field("idleWarningSeconds", "seconds")}
        {field("sessionRefreshTokenHours", "hours")}
        {field("rememberMeRefreshTokenDays", "days")}
      </div>

      <div className="space-y-2 md:col-span-2">
        <div className="flex items-start gap-3">
          <Checkbox
            id="session-security-remember-me-enabled"
            checked={values.rememberMeEnabled}
            onChange={(event) =>
              setValues((prev) => ({ ...prev, rememberMeEnabled: event.target.checked }))
            }
            disabled={readOnly}
            className="mt-1"
          />
          <div className="space-y-1">
            <Label htmlFor="session-security-remember-me-enabled" className="cursor-pointer font-normal">
              {t("settings:sessionSecurity.fields.rememberMeEnabled.label")}
            </Label>
            <p className="text-xs text-muted-foreground">
              {t("settings:sessionSecurity.fields.rememberMeEnabled.description")}
            </p>
          </div>
        </div>
      </div>

      {!readOnly ? (
        <div className="flex justify-end">
          <Button onClick={handleSave} disabled={isSaving || hasBlockingErrors}>
            {t("common:actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
