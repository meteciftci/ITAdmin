import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import {
  SecretInput,
  SettingsField,
  SettingsFormActions,
  SettingsSection,
  UnsavedChangesGuard,
} from "@/components/common/settings-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import {
  NOTIFICATION_EMAIL_SETTINGS_QUERY_KEY,
  getEmailProviderSettings,
  testEmailProvider,
  updateEmailProviderSettings,
} from "@/features/notification-providers/api";
import type {
  EmailProviderSettings,
  TestEmailProviderRequest,
  UpdateEmailProviderSettingsRequest,
} from "@/features/notification-providers/types";
import { validateEmailProviderForm } from "@/features/notification-providers/provider-form-utils";
import { useAuthStore } from "@/features/auth/auth-store";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";

type Props = { readOnly: boolean; onDirtyChange?: (dirty: boolean) => void };
type EmailFormState = {
  isEnabled: boolean;
  displayName: string;
  host: string;
  port: string;
  useSsl: boolean;
  userName: string;
  password: string;
  fromAddress: string;
  fromDisplayName: string;
  timeoutSeconds: string;
};

const mapSettingsToForm = (settings: EmailProviderSettings): EmailFormState => ({
  isEnabled: settings.isEnabled,
  displayName: settings.displayName ?? "",
  host: settings.host ?? "",
  port: String(settings.port || 587),
  useSsl: settings.useSsl,
  userName: settings.userName ?? "",
  password: "",
  fromAddress: settings.fromAddress ?? "",
  fromDisplayName: settings.fromDisplayName ?? "",
  timeoutSeconds: String(settings.timeoutSeconds || 30),
});

const buildUpdatePayload = (form: EmailFormState): UpdateEmailProviderSettingsRequest => ({
  isEnabled: form.isEnabled,
  displayName: form.displayName.trim() || null,
  host: form.host.trim(),
  port: Number.parseInt(form.port, 10),
  useSsl: form.useSsl,
  userName: form.userName.trim() || null,
  password: form.password || null,
  fromAddress: form.fromAddress.trim(),
  fromDisplayName: form.fromDisplayName.trim() || null,
  timeoutSeconds: Number.parseInt(form.timeoutSeconds, 10),
});

export function EmailProviderSettingsTab({ readOnly, onDirtyChange }: Props) {
  const { t } = useTranslation(["notificationProviders", "common"]);
  const query = useQuery({
    queryKey: NOTIFICATION_EMAIL_SETTINGS_QUERY_KEY,
    queryFn: getEmailProviderSettings,
    refetchOnWindowFocus: false,
  });
  if (query.isLoading) return <LoadingState />;
  if (query.isError || !query.data) {
    return <ErrorState title={t("notificationProviders:email.messages.loadFailed")} retry={<Button variant="outline" size="sm" onClick={() => void query.refetch()}>{t("common:retry")}</Button>} />;
  }
  return <EmailProviderSettingsForm key={query.data.lastValidatedAt ?? "email"} initialSettings={query.data} readOnly={readOnly} onDirtyChange={onDirtyChange} />;
}

function EmailProviderSettingsForm({ initialSettings, readOnly, onDirtyChange }: { initialSettings: EmailProviderSettings; readOnly: boolean; onDirtyChange?: (dirty: boolean) => void }) {
  const { t } = useTranslation(["notificationProviders", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, PermissionCodes.NotificationProviders.Update);
  const canTest = canAccess(user, PermissionCodes.NotificationProviders.Test);
  const isReadOnly = readOnly || !canUpdate;
  const initialForm = useMemo(() => mapSettingsToForm(initialSettings), [initialSettings]);
  const [form, setForm] = useState(initialForm);
  const [baseline, setBaseline] = useState(() => JSON.stringify(buildUpdatePayload(initialForm)));
  const [errors, setErrors] = useState<ReturnType<typeof validateEmailProviderForm>>({});
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [testForm, setTestForm] = useState<TestEmailProviderRequest>({ recipientEmail: "", subject: "", body: "" });
  const [testResult, setTestResult] = useState<{ ok: boolean; message: string } | null>(null);
  const isDirty = JSON.stringify(buildUpdatePayload(form)) !== baseline;
  useEffect(() => {
    onDirtyChange?.(isDirty);
    return () => onDirtyChange?.(false);
  }, [isDirty, onDirtyChange]);

  const updateMutation = useMutation({
    mutationFn: updateEmailProviderSettings,
    onSuccess: async (next) => {
      const nextForm = mapSettingsToForm(next);
      setForm(nextForm);
      setBaseline(JSON.stringify(buildUpdatePayload(nextForm)));
      setErrors({});
      setSaveError(null);
      setSaved(true);
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_EMAIL_SETTINGS_QUERY_KEY });
    },
    onError: (error: unknown) => {
      setSaved(false);
      setSaveError(getApiErrorMessage(error, t("notificationProviders:email.messages.saveFailed")));
    },
  });
  const testMutation = useMutation({
    mutationFn: testEmailProvider,
    onSuccess: (result) => setTestResult({ ok: true, message: result.message }),
    onError: (error: unknown) => setTestResult({ ok: false, message: getApiErrorMessage(error, t("notificationProviders:email.messages.testFailed")) }),
  });
  const updateField = <K extends keyof EmailFormState>(field: K, value: EmailFormState[K]) => {
    setSaved(false);
    setSaveError(null);
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };
  const fieldError = (key: keyof EmailFormState) => errors[key] ? t(`notificationProviders:validation.${errors[key]}`) : undefined;
  const save = () => {
    const nextErrors = validateEmailProviderForm(form);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length === 0) updateMutation.mutate(buildUpdatePayload(form));
  };
  const actionState = updateMutation.isPending ? "saving" : saveError ? "error" : isDirty ? "dirty" : saved ? "saved" : "pristine";

  return (
    <div className="space-y-6">
      <UnsavedChangesGuard when={isDirty && !updateMutation.isPending} title={t("notificationProviders:unsaved.title")} description={t("notificationProviders:unsaved.description")} leaveText={t("notificationProviders:unsaved.leave")} stayText={t("notificationProviders:unsaved.stay")} />
      <SettingsSection
        title={t("notificationProviders:email.sectionTitle")}
        description={t("notificationProviders:email.sectionDescription")}
        actions={<label className="flex items-center gap-2 text-sm font-medium"><span>{t("notificationProviders:fields.active")}</span><Switch checked={form.isEnabled} onCheckedChange={(value) => updateField("isEnabled", value)} disabled={isReadOnly} /></label>}
      >
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="email-provider" label={t("notificationProviders:fields.provider")} description={t("notificationProviders:email.providerFixedHint")}><Input id="email-provider" value={t("notificationProviders:email.providerName")} readOnly disabled /></SettingsField>
          <SettingsField id="email-display-name" label={t("notificationProviders:fields.displayName")} optional optionalLabel={t("notificationProviders:fields.optional")}><Input id="email-display-name" value={form.displayName} onChange={(e) => updateField("displayName", e.target.value)} readOnly={isReadOnly} /></SettingsField>
        </div>
      </SettingsSection>

      <SettingsSection title={t("notificationProviders:email.connectionSection")} description={t("notificationProviders:email.connectionDescription")}>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="email-host" label={t("notificationProviders:email.fields.host")} error={fieldError("host")}><Input id="email-host" value={form.host} aria-invalid={Boolean(errors.host)} onChange={(e) => updateField("host", e.target.value)} readOnly={isReadOnly} /></SettingsField>
          <SettingsField id="email-port" label={t("notificationProviders:email.fields.port")} error={fieldError("port")}><Input id="email-port" type="number" min={1} max={65535} value={form.port} aria-invalid={Boolean(errors.port)} onChange={(e) => updateField("port", e.target.value)} readOnly={isReadOnly} /></SettingsField>
          <SettingsField id="email-timeout" label={t("notificationProviders:fields.timeoutSeconds")} error={fieldError("timeoutSeconds")}><Input id="email-timeout" type="number" min={5} max={300} value={form.timeoutSeconds} aria-invalid={Boolean(errors.timeoutSeconds)} onChange={(e) => updateField("timeoutSeconds", e.target.value)} readOnly={isReadOnly} /></SettingsField>
          <div className="flex items-center gap-3 rounded-lg border bg-muted/25 px-4 py-3"><Switch id="email-use-ssl" checked={form.useSsl} onCheckedChange={(value) => updateField("useSsl", value)} disabled={isReadOnly} /><label htmlFor="email-use-ssl" className="text-sm font-medium">{t("notificationProviders:email.fields.useSsl")}</label></div>
        </div>
      </SettingsSection>

      <SettingsSection title={t("notificationProviders:email.identitySection")} description={t("notificationProviders:email.identityDescription")}>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="email-username" label={t("notificationProviders:email.fields.userName")} optional optionalLabel={t("notificationProviders:fields.optional")}><Input id="email-username" value={form.userName} onChange={(e) => updateField("userName", e.target.value)} readOnly={isReadOnly} autoComplete="username" /></SettingsField>
          <SettingsField id="email-password" label={t("notificationProviders:email.fields.password")} optional optionalLabel={t("notificationProviders:fields.optional")}><SecretInput id="email-password" value={form.password} onChange={(e) => updateField("password", e.target.value)} readOnly={isReadOnly} hasStoredValue={initialSettings.hasPassword} storedLabel={t("notificationProviders:secrets.storedLabel")} storedHint={t("notificationProviders:secrets.storedHint")} showLabel={t("notificationProviders:secrets.show")} hideLabel={t("notificationProviders:secrets.hide")} /></SettingsField>
          <SettingsField id="email-from" label={t("notificationProviders:email.fields.fromAddress")} error={fieldError("fromAddress")}><Input id="email-from" type="email" value={form.fromAddress} aria-invalid={Boolean(errors.fromAddress)} onChange={(e) => updateField("fromAddress", e.target.value)} readOnly={isReadOnly} /></SettingsField>
          <SettingsField id="email-from-name" label={t("notificationProviders:email.fields.fromDisplayName")} optional optionalLabel={t("notificationProviders:fields.optional")}><Input id="email-from-name" value={form.fromDisplayName} onChange={(e) => updateField("fromDisplayName", e.target.value)} readOnly={isReadOnly} /></SettingsField>
        </div>
      </SettingsSection>

      {canUpdate ? <SettingsFormActions state={actionState} stateLabel={t(`notificationProviders:saveStates.${actionState}`)} errorTitle={t("notificationProviders:saveStates.failedTitle")} errorMessage={saveError}><Button onClick={save} disabled={isReadOnly || updateMutation.isPending || !isDirty}>{updateMutation.isPending ? t("notificationProviders:actions.saving") : t("common:actions.save")}</Button></SettingsFormActions> : null}

      <SettingsSection title={t("notificationProviders:email.testSection")} description={t("notificationProviders:testDescription")}>
        {isDirty ? <Alert><AlertTitle>{t("notificationProviders:testNeedsSave.title")}</AlertTitle><AlertDescription>{t("notificationProviders:testNeedsSave.description")}</AlertDescription></Alert> : null}
        {testResult ? <Alert variant={testResult.ok ? "default" : "destructive"}><AlertTitle>{testResult.ok ? t("notificationProviders:testResult.success") : t("notificationProviders:testResult.failed")}</AlertTitle><AlertDescription>{testResult.message}</AlertDescription></Alert> : null}
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="email-test-recipient" label={t("notificationProviders:email.fields.testRecipientEmail")}><Input id="email-test-recipient" type="email" value={testForm.recipientEmail} onChange={(e) => setTestForm((c) => ({ ...c, recipientEmail: e.target.value }))} /></SettingsField>
          <SettingsField id="email-test-subject" label={t("notificationProviders:email.fields.testSubject")}><Input id="email-test-subject" value={testForm.subject} onChange={(e) => setTestForm((c) => ({ ...c, subject: e.target.value }))} /></SettingsField>
          <SettingsField id="email-test-body" label={t("notificationProviders:email.fields.testBody")} className="md:col-span-2"><Input id="email-test-body" value={testForm.body} onChange={(e) => setTestForm((c) => ({ ...c, body: e.target.value }))} /></SettingsField>
        </div>
        {canTest ? <Button variant="secondary" disabled={isDirty || !initialSettings.host || testMutation.isPending || !testForm.recipientEmail.trim()} onClick={() => { setTestResult(null); testMutation.mutate(testForm); }}>{testMutation.isPending ? t("notificationProviders:actions.testing") : t("notificationProviders:email.actions.test")}</Button> : null}
      </SettingsSection>
    </div>
  );
}
