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
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { useAuthStore } from "@/features/auth/auth-store";
import {
  NOTIFICATION_SMS_SETTINGS_QUERY_KEY,
  getSmsProviderSettings,
  testSmsProvider,
  updateSmsProviderSettings,
} from "@/features/notification-providers/api";
import { KeyValuePairsEditor } from "@/features/notification-providers/components/KeyValuePairsEditor";
import { parseSmsStatusCodes, validateSmsProviderForm } from "@/features/notification-providers/provider-form-utils";
import type {
  SmsProviderSettings,
  TestSmsProviderRequest,
  UpdateSmsProviderSettingsRequest,
} from "@/features/notification-providers/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { PermissionCodes } from "@/lib/permission-codes";
import { canAccess } from "@/lib/permissions";

type Props = { readOnly: boolean; onDirtyChange?: (dirty: boolean) => void };
type SmsFormState = {
  isEnabled: boolean; displayName: string; sender: string; timeoutSeconds: string;
  endpointUrl: string; method: string; contentType: string; authType: string;
  apiKeyName: string; basicUserName: string; basicPassword: string; bearerToken: string; apiKeyValue: string;
  headers: { key: string; value: string }[]; queryParameters: { key: string; value: string }[];
  bodyTemplate: string; successStatusCodes: string; successBodyContains: string; turkishCharacterMode: string;
};

const mapSettingsToForm = (settings: SmsProviderSettings): SmsFormState => ({
  isEnabled: settings.isEnabled,
  displayName: settings.displayName ?? "",
  sender: settings.sender ?? "",
  timeoutSeconds: String(settings.timeoutSeconds || 30),
  endpointUrl: settings.endpointUrl ?? "",
  method: settings.method || "POST",
  contentType: settings.contentType || "application/json",
  authType: settings.authType || "None",
  apiKeyName: settings.apiKeyName ?? "",
  basicUserName: "", basicPassword: "", bearerToken: "", apiKeyValue: "",
  headers: settings.headers ?? [], queryParameters: settings.queryParameters ?? [],
  bodyTemplate: settings.bodyTemplate ?? "",
  successStatusCodes: (settings.successStatusCodes ?? [200]).join(","),
  successBodyContains: settings.successBodyContains ?? "",
  turkishCharacterMode: settings.turkishCharacterMode || "Preserve",
});

const buildUpdatePayload = (form: SmsFormState): UpdateSmsProviderSettingsRequest => ({
  isEnabled: form.isEnabled,
  displayName: form.displayName.trim() || null,
  sender: form.sender.trim() || null,
  timeoutSeconds: Number.parseInt(form.timeoutSeconds, 10),
  endpointUrl: form.endpointUrl.trim(), method: form.method, contentType: form.contentType, authType: form.authType,
  apiKeyName: form.apiKeyName.trim() || null,
  basicUserName: form.basicUserName || null, basicPassword: form.basicPassword || null,
  bearerToken: form.bearerToken || null, apiKeyValue: form.apiKeyValue || null,
  headers: form.headers, queryParameters: form.queryParameters,
  bodyTemplate: form.bodyTemplate || null,
  successStatusCodes: parseSmsStatusCodes(form.successStatusCodes),
  successBodyContains: form.successBodyContains.trim() || null,
  turkishCharacterMode: form.turkishCharacterMode,
});

export function SmsProviderSettingsTab({ readOnly, onDirtyChange }: Props) {
  const { t } = useTranslation(["notificationProviders", "common"]);
  const query = useQuery({ queryKey: NOTIFICATION_SMS_SETTINGS_QUERY_KEY, queryFn: getSmsProviderSettings, refetchOnWindowFocus: false });
  if (query.isLoading) return <LoadingState />;
  if (query.isError || !query.data) return <ErrorState title={t("notificationProviders:sms.messages.loadFailed")} retry={<Button variant="outline" size="sm" onClick={() => void query.refetch()}>{t("common:retry")}</Button>} />;
  return <SmsProviderSettingsForm key={query.data.lastValidatedAt ?? "sms"} initialSettings={query.data} readOnly={readOnly} onDirtyChange={onDirtyChange} />;
}

function SmsProviderSettingsForm({ initialSettings, readOnly, onDirtyChange }: { initialSettings: SmsProviderSettings; readOnly: boolean; onDirtyChange?: (dirty: boolean) => void }) {
  const { t } = useTranslation(["notificationProviders", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, PermissionCodes.NotificationProviders.Update);
  const canTest = canAccess(user, PermissionCodes.NotificationProviders.Test);
  const isReadOnly = readOnly || !canUpdate;
  const initialForm = useMemo(() => mapSettingsToForm(initialSettings), [initialSettings]);
  const [form, setForm] = useState(initialForm);
  const [baseline, setBaseline] = useState(() => JSON.stringify(buildUpdatePayload(initialForm)));
  const [errors, setErrors] = useState<ReturnType<typeof validateSmsProviderForm>>({});
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [testForm, setTestForm] = useState<TestSmsProviderRequest>({ phoneNumber: "", message: "" });
  const [testResult, setTestResult] = useState<{ ok: boolean; message: string } | null>(null);
  const isDirty = JSON.stringify(buildUpdatePayload(form)) !== baseline;
  useEffect(() => {
    onDirtyChange?.(isDirty);
    return () => onDirtyChange?.(false);
  }, [isDirty, onDirtyChange]);
  const updateField = <K extends keyof SmsFormState>(field: K, value: SmsFormState[K]) => {
    setSaved(false); setSaveError(null); setForm((current) => ({ ...current, [field]: value })); setErrors((current) => ({ ...current, [field]: undefined }));
  };
  const updateMutation = useMutation({
    mutationFn: updateSmsProviderSettings,
    onSuccess: async (next) => {
      const nextForm = mapSettingsToForm(next); setForm(nextForm); setBaseline(JSON.stringify(buildUpdatePayload(nextForm)));
      setErrors({}); setSaveError(null); setSaved(true);
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_SMS_SETTINGS_QUERY_KEY });
    },
    onError: (error: unknown) => { setSaved(false); setSaveError(getApiErrorMessage(error, t("notificationProviders:sms.messages.saveFailed"))); },
  });
  const testMutation = useMutation({
    mutationFn: testSmsProvider,
    onSuccess: (result) => setTestResult({ ok: true, message: result.message }),
    onError: (error: unknown) => setTestResult({ ok: false, message: getApiErrorMessage(error, t("notificationProviders:sms.messages.testFailed")) }),
  });
  const fieldError = (key: keyof SmsFormState) => errors[key] ? t(`notificationProviders:validation.${errors[key]}`) : undefined;
  const save = () => { const nextErrors = validateSmsProviderForm(form); setErrors(nextErrors); if (Object.keys(nextErrors).length === 0) updateMutation.mutate(buildUpdatePayload(form)); };
  const actionState = updateMutation.isPending ? "saving" : saveError ? "error" : isDirty ? "dirty" : saved ? "saved" : "pristine";
  const secretProps = { storedLabel: t("notificationProviders:secrets.storedLabel"), storedHint: t("notificationProviders:secrets.storedHint"), showLabel: t("notificationProviders:secrets.show"), hideLabel: t("notificationProviders:secrets.hide") };

  return <div className="space-y-6">
    <UnsavedChangesGuard when={isDirty && !updateMutation.isPending} title={t("notificationProviders:unsaved.title")} description={t("notificationProviders:unsaved.description")} leaveText={t("notificationProviders:unsaved.leave")} stayText={t("notificationProviders:unsaved.stay")} />
    <SettingsSection title={t("notificationProviders:sms.sectionTitle")} description={t("notificationProviders:sms.sectionDescription")} actions={<label className="flex items-center gap-2 text-sm font-medium"><span>{t("notificationProviders:fields.active")}</span><Switch checked={form.isEnabled} onCheckedChange={(value) => updateField("isEnabled", value)} disabled={isReadOnly} /></label>}>
      <div className="grid gap-5 md:grid-cols-2">
        <SettingsField id="sms-provider" label={t("notificationProviders:fields.provider")} description={t("notificationProviders:sms.providerFixedHint")}><Input id="sms-provider" value={t("notificationProviders:sms.providerName")} readOnly disabled /></SettingsField>
        <SettingsField id="sms-display-name" label={t("notificationProviders:fields.displayName")} optional optionalLabel={t("notificationProviders:fields.optional")}><Input id="sms-display-name" value={form.displayName} onChange={(e) => updateField("displayName", e.target.value)} readOnly={isReadOnly} /></SettingsField>
        <SettingsField id="sms-sender" label={t("notificationProviders:sms.fields.sender")} optional optionalLabel={t("notificationProviders:fields.optional")}><Input id="sms-sender" value={form.sender} onChange={(e) => updateField("sender", e.target.value)} readOnly={isReadOnly} /></SettingsField>
        <SettingsField id="sms-timeout" label={t("notificationProviders:fields.timeoutSeconds")} error={fieldError("timeoutSeconds")}><Input id="sms-timeout" type="number" min={5} max={300} value={form.timeoutSeconds} aria-invalid={Boolean(errors.timeoutSeconds)} onChange={(e) => updateField("timeoutSeconds", e.target.value)} readOnly={isReadOnly} /></SettingsField>
      </div>
    </SettingsSection>

    <SettingsSection title={t("notificationProviders:sms.httpSection")} description={t("notificationProviders:sms.httpDescription")}>
      <div className="grid gap-5 md:grid-cols-2">
        <SettingsField id="sms-endpoint" label={t("notificationProviders:sms.fields.endpointUrl")} error={fieldError("endpointUrl")} className="md:col-span-2"><Input id="sms-endpoint" type="url" value={form.endpointUrl} aria-invalid={Boolean(errors.endpointUrl)} onChange={(e) => updateField("endpointUrl", e.target.value)} readOnly={isReadOnly} /></SettingsField>
        <SettingsField id="sms-method" label={t("notificationProviders:sms.fields.method")}><Select id="sms-method" value={form.method} onChange={(e) => updateField("method", e.target.value)} disabled={isReadOnly}><option value="GET">GET</option><option value="POST">POST</option></Select></SettingsField>
        <SettingsField id="sms-content-type" label={t("notificationProviders:sms.fields.contentType")}><Select id="sms-content-type" value={form.contentType} onChange={(e) => updateField("contentType", e.target.value)} disabled={isReadOnly}><option value="application/json">application/json</option><option value="application/x-www-form-urlencoded">application/x-www-form-urlencoded</option><option value="text/xml">text/xml</option><option value="text/plain">text/plain</option></Select></SettingsField>
      </div>
    </SettingsSection>

    <SettingsSection title={t("notificationProviders:sms.authSection")} description={t("notificationProviders:sms.authDescription")}>
      <SettingsField id="sms-auth-type" label={t("notificationProviders:sms.fields.authType")}><Select id="sms-auth-type" value={form.authType} onChange={(e) => updateField("authType", e.target.value)} disabled={isReadOnly}><option value="None">{t("notificationProviders:sms.authTypes.none")}</option><option value="Basic">{t("notificationProviders:sms.authTypes.basic")}</option><option value="BearerToken">{t("notificationProviders:sms.authTypes.bearer")}</option><option value="ApiKeyHeader">{t("notificationProviders:sms.authTypes.apiKeyHeader")}</option><option value="ApiKeyQuery">{t("notificationProviders:sms.authTypes.apiKeyQuery")}</option></Select></SettingsField>
      {form.authType === "Basic" ? <div className="grid gap-5 md:grid-cols-2"><SettingsField id="sms-basic-user" label={t("notificationProviders:sms.fields.basicUserName")}><SecretInput id="sms-basic-user" value={form.basicUserName} onChange={(e) => updateField("basicUserName", e.target.value)} readOnly={isReadOnly} hasStoredValue={initialSettings.hasBasicPassword} {...secretProps} /></SettingsField><SettingsField id="sms-basic-password" label={t("notificationProviders:sms.fields.basicPassword")}><SecretInput id="sms-basic-password" value={form.basicPassword} onChange={(e) => updateField("basicPassword", e.target.value)} readOnly={isReadOnly} hasStoredValue={initialSettings.hasBasicPassword} {...secretProps} /></SettingsField></div> : null}
      {form.authType === "BearerToken" ? <SettingsField id="sms-bearer" label={t("notificationProviders:sms.fields.bearerToken")}><SecretInput id="sms-bearer" value={form.bearerToken} onChange={(e) => updateField("bearerToken", e.target.value)} readOnly={isReadOnly} hasStoredValue={initialSettings.hasBearerToken} {...secretProps} /></SettingsField> : null}
      {form.authType === "ApiKeyHeader" || form.authType === "ApiKeyQuery" ? <div className="grid gap-5 md:grid-cols-2"><SettingsField id="sms-api-name" label={t("notificationProviders:sms.fields.apiKeyName")} error={fieldError("apiKeyName")}><Input id="sms-api-name" value={form.apiKeyName} aria-invalid={Boolean(errors.apiKeyName)} onChange={(e) => updateField("apiKeyName", e.target.value)} readOnly={isReadOnly} /></SettingsField><SettingsField id="sms-api-value" label={t("notificationProviders:sms.fields.apiKeyValue")}><SecretInput id="sms-api-value" value={form.apiKeyValue} onChange={(e) => updateField("apiKeyValue", e.target.value)} readOnly={isReadOnly} hasStoredValue={initialSettings.hasApiKey} {...secretProps} /></SettingsField></div> : null}
    </SettingsSection>

    <SettingsSection title={t("notificationProviders:sms.requestSection")} description={t("notificationProviders:sms.requestDescription")}>
      <KeyValuePairsEditor label={t("notificationProviders:sms.fields.headers")} pairs={form.headers} onChange={(value) => updateField("headers", value)} disabled={isReadOnly} />
      <KeyValuePairsEditor label={t("notificationProviders:sms.fields.queryParameters")} pairs={form.queryParameters} onChange={(value) => updateField("queryParameters", value)} disabled={isReadOnly} />
      <SettingsField id="sms-body-template" label={t("notificationProviders:sms.fields.bodyTemplate")} description={t("notificationProviders:sms.bodyTemplateHint")} optional optionalLabel={t("notificationProviders:fields.optional")}><Textarea id="sms-body-template" value={form.bodyTemplate} onChange={(e) => updateField("bodyTemplate", e.target.value)} rows={6} readOnly={isReadOnly} /></SettingsField>
      <div className="grid gap-5 md:grid-cols-2">
        <SettingsField id="sms-success-codes" label={t("notificationProviders:sms.fields.successStatusCodes")} description={t("notificationProviders:sms.successCodesHint")} error={fieldError("successStatusCodes")}><Input id="sms-success-codes" value={form.successStatusCodes} aria-invalid={Boolean(errors.successStatusCodes)} onChange={(e) => updateField("successStatusCodes", e.target.value)} placeholder="200,201" readOnly={isReadOnly} /></SettingsField>
        <SettingsField id="sms-success-body" label={t("notificationProviders:sms.fields.successBodyContains")} optional optionalLabel={t("notificationProviders:fields.optional")}><Input id="sms-success-body" value={form.successBodyContains} onChange={(e) => updateField("successBodyContains", e.target.value)} readOnly={isReadOnly} /></SettingsField>
        <SettingsField id="sms-turkish-mode" label={t("notificationProviders:sms.fields.turkishCharacterMode")}><Select id="sms-turkish-mode" value={form.turkishCharacterMode} onChange={(e) => updateField("turkishCharacterMode", e.target.value)} disabled={isReadOnly}><option value="Preserve">{t("notificationProviders:sms.turkishModes.preserve")}</option><option value="TransliterateToAscii">{t("notificationProviders:sms.turkishModes.transliterate")}</option></Select></SettingsField>
      </div>
    </SettingsSection>

    {canUpdate ? <SettingsFormActions state={actionState} stateLabel={t(`notificationProviders:saveStates.${actionState}`)} errorTitle={t("notificationProviders:saveStates.failedTitle")} errorMessage={saveError}><Button onClick={save} disabled={isReadOnly || updateMutation.isPending || !isDirty}>{updateMutation.isPending ? t("notificationProviders:actions.saving") : t("common:actions.save")}</Button></SettingsFormActions> : null}

    <SettingsSection title={t("notificationProviders:sms.testSection")} description={t("notificationProviders:testDescription")}>
      {isDirty ? <Alert><AlertTitle>{t("notificationProviders:testNeedsSave.title")}</AlertTitle><AlertDescription>{t("notificationProviders:testNeedsSave.description")}</AlertDescription></Alert> : null}
      {testResult ? <Alert variant={testResult.ok ? "default" : "destructive"}><AlertTitle>{testResult.ok ? t("notificationProviders:testResult.success") : t("notificationProviders:testResult.failed")}</AlertTitle><AlertDescription>{testResult.message}</AlertDescription></Alert> : null}
      <div className="grid gap-5 md:grid-cols-2"><SettingsField id="sms-test-phone" label={t("notificationProviders:sms.fields.testPhoneNumber")}><Input id="sms-test-phone" type="tel" value={testForm.phoneNumber} onChange={(e) => setTestForm((c) => ({ ...c, phoneNumber: e.target.value }))} /></SettingsField><SettingsField id="sms-test-message" label={t("notificationProviders:sms.fields.testMessage")} className="md:col-span-2"><Textarea id="sms-test-message" value={testForm.message} onChange={(e) => setTestForm((c) => ({ ...c, message: e.target.value }))} rows={3} /></SettingsField></div>
      {canTest ? <Button variant="secondary" disabled={isDirty || !initialSettings.endpointUrl || testMutation.isPending || !testForm.phoneNumber.trim() || !testForm.message.trim()} onClick={() => { setTestResult(null); testMutation.mutate(testForm); }}>{testMutation.isPending ? t("notificationProviders:actions.testing") : t("notificationProviders:sms.actions.test")}</Button> : null}
    </SettingsSection>
  </div>;
}
