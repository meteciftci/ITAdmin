import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import {
  SettingsField,
  SettingsFormActions,
  SettingsSection,
} from "@/components/common/settings-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import type { SessionSecuritySettings, UpdateSessionSecuritySettingsRequest } from "../types";

const limits = {
  sessionDurationMinutes: { min: 5, max: 240 },
  idleWarningSeconds: { min: 10, max: 300 },
  sessionRefreshTokenHours: { min: 1, max: 24 },
  rememberMeRefreshTokenDays: { min: 1, max: 30 },
} as const;

type NumericFieldKey = keyof typeof limits;
type SessionSecurityFormValues = Omit<SessionSecuritySettings, "accessTokenMinutes" | "idleTimeoutMinutes"> & {
  sessionDurationMinutes: number;
};
type FieldKey = keyof SessionSecurityFormValues;

type Props = {
  initialValues: SessionSecuritySettings;
  readOnly: boolean;
  isSaving: boolean;
  saveError: string | null;
  saveSucceeded: boolean;
  onDirtyChange: (dirty: boolean) => void;
  onChange: () => void;
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
  if (
    Number.isFinite(values.idleWarningSeconds) &&
    Number.isFinite(values.sessionDurationMinutes) &&
    values.idleWarningSeconds >= values.sessionDurationMinutes * 60
  ) {
    errors.idleWarningSeconds = t("settings:sessionSecurity.validation.warningLessThanSessionDuration");
  }
  return errors;
}

function toFormValues(initialValues: SessionSecuritySettings): SessionSecurityFormValues {
  const accessTokenMinutes = Number.isFinite(initialValues.accessTokenMinutes) ? initialValues.accessTokenMinutes : 30;
  const idleTimeoutMinutes = Number.isFinite(initialValues.idleTimeoutMinutes) ? initialValues.idleTimeoutMinutes : 30;
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
  saveError,
  saveSucceeded,
  onDirtyChange,
  onChange,
  onSubmit,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const initialFormValues = useMemo(() => toFormValues(initialValues), [initialValues]);
  const [values, setValues] = useState(initialFormValues);
  const errors = useMemo(() => collectErrors(values, t), [values, t]);
  const hasBlockingErrors = Object.keys(errors).length > 0;
  const isDirty = JSON.stringify(values) !== JSON.stringify(initialFormValues);

  useEffect(() => {
    onDirtyChange(isDirty);
    return () => onDirtyChange(false);
  }, [isDirty, onDirtyChange]);

  const setValue = <K extends keyof SessionSecurityFormValues>(field: K, value: SessionSecurityFormValues[K]) => {
    onChange();
    setValues((current) => ({ ...current, [field]: value }));
  };
  const setNumberField = (field: NumericFieldKey, raw: string) => {
    const parsed = raw.trim() === "" ? Number.NaN : Number.parseInt(raw, 10);
    setValue(field, Number.isNaN(parsed) ? Number.NaN : parsed);
  };
  const numberDisplay = (field: NumericFieldKey) => Number.isFinite(values[field]) ? String(values[field]) : "";
  const submit = () => {
    if (readOnly || hasBlockingErrors) return;
    onSubmit({
      accessTokenMinutes: values.sessionDurationMinutes,
      idleTimeoutMinutes: values.sessionDurationMinutes,
      idleWarningSeconds: values.idleWarningSeconds,
      sessionRefreshTokenHours: values.sessionRefreshTokenHours,
      rememberMeRefreshTokenDays: values.rememberMeRefreshTokenDays,
      rememberMeEnabled: values.rememberMeEnabled,
    });
  };
  const actionState = isSaving ? "saving" : saveError ? "error" : isDirty ? "dirty" : saveSucceeded ? "saved" : "pristine";

  const numericField = (key: NumericFieldKey, unitKey: string) => (
    <SettingsField
      id={`session-security-${key}`}
      label={t(`settings:sessionSecurity.fields.${key}.label`)}
      description={t(`settings:sessionSecurity.fields.${key}.description`)}
      error={errors[key]}
    >
      <div className="flex flex-wrap items-center gap-2">
        <Input
          id={`session-security-${key}`}
          type="number"
          className="max-w-[12rem]"
          min={limits[key].min}
          max={limits[key].max}
          step={1}
          value={numberDisplay(key)}
          onChange={(event) => setNumberField(key, event.target.value)}
          aria-describedby={`session-security-${key}-description${errors[key] ? ` session-security-${key}-error` : ""}`}
          aria-invalid={Boolean(errors[key])}
          readOnly={readOnly}
          disabled={readOnly}
        />
        <span className="text-sm tabular-nums text-muted-foreground">{t(`settings:sessionSecurity.units.${unitKey}`)}</span>
      </div>
    </SettingsField>
  );

  return (
    <div className="space-y-6">
      <Alert>
        <AlertTitle>{t("settings:sessionSecurity.effect.title")}</AlertTitle>
        <AlertDescription>{t("settings:sessionSecurity.effect.description")}</AlertDescription>
      </Alert>

      <SettingsSection title={t("settings:sessionSecurity.sections.activeSession")} description={t("settings:sessionSecurity.sections.activeSessionDescription")}>
        <div className="grid gap-5 md:grid-cols-2">
          {numericField("sessionDurationMinutes", "minutes")}
          {numericField("idleWarningSeconds", "seconds")}
        </div>
      </SettingsSection>

      <SettingsSection title={t("settings:sessionSecurity.sections.refreshTokens")} description={t("settings:sessionSecurity.sections.refreshTokensDescription")}>
        <div className="grid gap-5 md:grid-cols-2">
          {numericField("sessionRefreshTokenHours", "hours")}
          {numericField("rememberMeRefreshTokenDays", "days")}
          <label className="flex items-start gap-3 rounded-lg border bg-muted/25 p-4 md:col-span-2">
            <Checkbox
              id="session-security-remember-me-enabled"
              checked={values.rememberMeEnabled}
              onChange={(event) => setValue("rememberMeEnabled", event.target.checked)}
              disabled={readOnly}
              className="mt-1 shrink-0"
            />
            <span className="space-y-1">
              <span className="block text-sm font-medium">{t("settings:sessionSecurity.fields.rememberMeEnabled.label")}</span>
              <span className="block text-sm leading-5 text-muted-foreground">{t("settings:sessionSecurity.fields.rememberMeEnabled.description")}</span>
            </span>
          </label>
        </div>
      </SettingsSection>

      {!readOnly ? (
        <SettingsFormActions state={actionState} stateLabel={t(`settings:saveStates.${actionState}`)} errorTitle={t("settings:saveStates.failedTitle")} errorMessage={saveError}>
          <Button type="button" onClick={submit} disabled={isSaving || hasBlockingErrors || !isDirty}>
            {isSaving ? t("settings:actions.saving") : t("common:actions.save")}
          </Button>
        </SettingsFormActions>
      ) : null}
    </div>
  );
}
