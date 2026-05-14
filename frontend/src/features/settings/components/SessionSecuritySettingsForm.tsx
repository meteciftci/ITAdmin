import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

import type { SessionSecuritySettings, UpdateSessionSecuritySettingsRequest } from "../types";

const limits = {
  sessionDurationMinutes: { min: 5, max: 240 },
  idleWarningSeconds: { min: 10, max: 300 },
  sessionRefreshTokenHours: { min: 1, max: 24 },
  rememberMeRefreshTokenDays: { min: 1, max: 30 },
} as const;

type NumericFieldKey = keyof typeof limits;

type SessionSecurityFormValues = Omit<
  SessionSecuritySettings,
  "accessTokenMinutes" | "idleTimeoutMinutes"
> & {
  sessionDurationMinutes: number;
};

type FieldKey = keyof SessionSecurityFormValues;

type SessionSecuritySettingsFormProps = {
  initialValues: SessionSecuritySettings;
  readOnly: boolean;
  isSaving: boolean;
  onSubmit: (payload: UpdateSessionSecuritySettingsRequest) => void;
};

function collectErrors(
  values: SessionSecurityFormValues,
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

  const sessionDurationSeconds = values.sessionDurationMinutes * 60;
  if (
    Number.isFinite(values.idleWarningSeconds) &&
    Number.isFinite(values.sessionDurationMinutes) &&
    values.idleWarningSeconds >= sessionDurationSeconds
  ) {
    errors.idleWarningSeconds = t("settings:sessionSecurity.validation.warningLessThanSessionDuration");
  }

  return errors;
}

function toFormValues(initialValues: SessionSecuritySettings): SessionSecurityFormValues {
  const accessTokenMinutes = Number.isFinite(initialValues.accessTokenMinutes)
    ? initialValues.accessTokenMinutes
    : 30;
  const idleTimeoutMinutes = Number.isFinite(initialValues.idleTimeoutMinutes)
    ? initialValues.idleTimeoutMinutes
    : 30;

  return {
    sessionDurationMinutes: Math.min(accessTokenMinutes, idleTimeoutMinutes),
    idleWarningSeconds: initialValues.idleWarningSeconds,
    sessionRefreshTokenHours: initialValues.sessionRefreshTokenHours,
    rememberMeRefreshTokenDays: initialValues.rememberMeRefreshTokenDays,
    rememberMeEnabled: initialValues.rememberMeEnabled,
  };
}

export function SessionSecuritySettingsForm({
  initialValues,
  readOnly,
  isSaving,
  onSubmit,
}: SessionSecuritySettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);
  const [values, setValues] = useState<SessionSecurityFormValues>(() => toFormValues(initialValues));

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
    onSubmit({
      accessTokenMinutes: values.sessionDurationMinutes,
      idleTimeoutMinutes: values.sessionDurationMinutes,
      idleWarningSeconds: values.idleWarningSeconds,
      sessionRefreshTokenHours: values.sessionRefreshTokenHours,
      rememberMeRefreshTokenDays: values.rememberMeRefreshTokenDays,
      rememberMeEnabled: values.rememberMeEnabled,
    });
  };

  const field = (key: NumericFieldKey, unitKey: string) => (
    <div className="min-w-0 space-y-1.5">
      <Label htmlFor={`session-security-${key}`}>{t(`settings:sessionSecurity.fields.${key}.label`)}</Label>
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        <Input
          id={`session-security-${key}`}
          type="number"
          className="max-w-[11rem] shrink-0"
          min={limits[key].min}
          max={limits[key].max}
          step={1}
          value={numberDisplay(key)}
          onChange={(event) => setNumberField(key, event.target.value)}
          readOnly={readOnly}
          disabled={readOnly}
        />
        <span className="text-xs text-muted-foreground tabular-nums">
          {t(`settings:sessionSecurity.units.${unitKey}`)}
        </span>
      </div>
      <p className="text-xs text-muted-foreground">{t(`settings:sessionSecurity.fields.${key}.description`)}</p>
      {computedErrors[key] ? <p className="text-xs text-destructive">{computedErrors[key]}</p> : null}
    </div>
  );

  return (
    <div className="space-y-3">
      <div className="grid gap-3 md:grid-cols-2">
        {field("sessionDurationMinutes", "minutes")}
        {field("idleWarningSeconds", "seconds")}
        {field("sessionRefreshTokenHours", "hours")}
        {field("rememberMeRefreshTokenDays", "days")}

        <div className="flex items-start gap-3 md:col-span-2 md:pt-0.5">
          <Checkbox
            id="session-security-remember-me-enabled"
            checked={values.rememberMeEnabled}
            onChange={(event) =>
              setValues((prev) => ({ ...prev, rememberMeEnabled: event.target.checked }))
            }
            disabled={readOnly}
            className="mt-1 shrink-0"
          />
          <div className="min-w-0 space-y-1">
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
        <div className="flex justify-end pt-1">
          <Button onClick={handleSave} disabled={isSaving || hasBlockingErrors}>
            {t("common:actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
