import { useEffect, useMemo, useState } from "react";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { useTranslation } from "react-i18next";

import { ConnectionDiagnosticsPanel } from "@/components/common/ConnectionDiagnosticsPanel";
import {
  SecretInput,
  SettingsField,
  SettingsFormActions,
  SettingsSection,
  UnsavedChangesGuard,
} from "@/components/common/settings-form";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { buildUpdateAdManagementSettingsPayload } from "@/features/ad-management/ad-management-settings-payload";
import { resolveAdManagementApiMessage } from "@/features/ad-management/ad-management-api-message";
import type {
  AdManagementSettings,
  AdManagementValidationResult,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";

export type AdManagementConnectionFormValues = {
  isEnabled: boolean;
  domainFqdn: string;
  netbiosDomainName: string;
  defaultNamingContext: string;
  baseDn: string;
  preferredDomainControllers: string[];
  serviceAccountUserName: string;
  serviceAccountPassword: string;
  clearServiceAccountPassword: boolean;
  powerShellHealthEnabled: boolean;
  powerShellTimeoutSeconds: string;
};

type Props = {
  settings: AdManagementSettings | undefined;
  readOnly: boolean;
  isSaving: boolean;
  saveError: string | null;
  candidateValidation: AdManagementValidationResult | null;
  savedValidation: AdManagementValidationResult | null;
  isTestingCandidate: boolean;
  isTestingSaved: boolean;
  validationError: string | null;
  onDirtyChange: (dirty: boolean) => void;
  onTestCandidate: (payload: UpdateAdManagementSettingsRequest) => Promise<AdManagementValidationResult>;
  onTestSaved: () => Promise<AdManagementValidationResult>;
  onSave: (payload: UpdateAdManagementSettingsRequest) => void;
};

/// Renders a persisted validation message. Stored messages are i18n keys; anything else is legacy
/// or unexpected content and is deliberately not echoed back into the UI.
function resolveLastValidationMessage(
  t: ReturnType<typeof useTranslation>["t"],
  raw: string | null | undefined,
): string {
  const trimmed = raw?.trim();
  if (!trimmed) {
    return "-";
  }

  if (!trimmed.startsWith("apiMessages.")) {
    return t("settings:adManagement.connection.lastValidationUnknown");
  }

  return resolveAdManagementApiMessage(
    t,
    { messageKey: trimmed },
    "settings:adManagement.connection.lastValidationUnknown",
  );
}

function buildInitialValues(settings: AdManagementSettings | undefined): AdManagementConnectionFormValues {
  return {
    isEnabled: settings?.isEnabled ?? false,
    domainFqdn: settings?.domainFqdn ?? "",
    netbiosDomainName: settings?.netbiosDomainName ?? "",
    defaultNamingContext: settings?.defaultNamingContext ?? "",
    baseDn: settings?.baseDn ?? "",
    preferredDomainControllers: settings?.preferredDomainControllers?.length
      ? [...settings.preferredDomainControllers]
      : [""],
    serviceAccountUserName: settings?.serviceAccountUserName ?? "",
    serviceAccountPassword: "",
    clearServiceAccountPassword: false,
    powerShellHealthEnabled: settings?.powerShellHealthEnabled ?? false,
    powerShellTimeoutSeconds: String(settings?.powerShellTimeoutSeconds ?? 30),
  };
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

function fingerprint(values: AdManagementConnectionFormValues): string {
  return JSON.stringify(values);
}

export function AdManagementConnectionForm({
  settings,
  readOnly,
  isSaving,
  saveError,
  candidateValidation,
  savedValidation,
  isTestingCandidate,
  isTestingSaved,
  validationError,
  onDirtyChange,
  onTestCandidate,
  onTestSaved,
  onSave,
}: Props) {
  const { t } = useTranslation(["settings", "common", "adManagement"]);
  const initialValues = useMemo(() => buildInitialValues(settings), [settings]);
  const [values, setValues] = useState(initialValues);
  const [validationAttempted, setValidationAttempted] = useState(false);
  const [testedFingerprint, setTestedFingerprint] = useState<string | null>(null);
  const busy = isSaving || isTestingCandidate || isTestingSaved;
  const isDirty = fingerprint(values) !== fingerprint(initialValues);
  const candidateIsCurrent = testedFingerprint === fingerprint(values);
  const hasPassword = settings?.hasServiceAccountPassword ?? false;
  const timeout = Number.parseInt(values.powerShellTimeoutSeconds, 10);
  const errors = {
    domainFqdn: values.isEnabled && !values.domainFqdn.trim() ? t("settings:adManagement.connection.validation.required") : undefined,
    netbiosDomainName: values.isEnabled && !values.netbiosDomainName.trim() ? t("settings:adManagement.connection.validation.required") : undefined,
    defaultNamingContext: values.isEnabled && !values.defaultNamingContext.trim() ? t("settings:adManagement.connection.validation.required") : undefined,
    baseDn: values.isEnabled && !values.baseDn.trim() ? t("settings:adManagement.connection.validation.required") : undefined,
    serviceAccountUserName: values.isEnabled && !values.serviceAccountUserName.trim() ? t("settings:adManagement.connection.validation.required") : undefined,
    serviceAccountPassword: values.isEnabled && !values.serviceAccountPassword && (!hasPassword || values.clearServiceAccountPassword)
      ? t("settings:adManagement.connection.validation.passwordRequired")
      : undefined,
    powerShellTimeoutSeconds: !Number.isFinite(timeout) || timeout < 5 || timeout > 300
      ? t("settings:adManagement.connection.validation.timeout")
      : undefined,
  };
  const formValid = !Object.values(errors).some(Boolean);

  useEffect(() => {
    onDirtyChange(isDirty);
    return () => onDirtyChange(false);
  }, [isDirty, onDirtyChange]);

  function update<K extends keyof AdManagementConnectionFormValues>(field: K, value: AdManagementConnectionFormValues[K]) {
    setValues((current) => ({ ...current, [field]: value }));
  }

  function buildPayload(): UpdateAdManagementSettingsRequest | null {
    if (!settings || !formValid) {
      setValidationAttempted(true);
      return null;
    }
    return buildUpdateAdManagementSettingsPayload(settings, {
      isEnabled: values.isEnabled,
      domainFqdn: emptyToNull(values.domainFqdn),
      netbiosDomainName: emptyToNull(values.netbiosDomainName),
      defaultNamingContext: emptyToNull(values.defaultNamingContext),
      baseDn: emptyToNull(values.baseDn),
      preferredDomainControllers: values.preferredDomainControllers.map((host) => host.trim()).filter(Boolean),
      serviceAccountUserName: emptyToNull(values.serviceAccountUserName),
      serviceAccountPassword: values.serviceAccountPassword || null,
      clearServiceAccountPassword: values.clearServiceAccountPassword,
      powerShellHealthEnabled: values.powerShellHealthEnabled,
      powerShellTimeoutSeconds: timeout,
    });
  }

  async function testCandidate() {
    const payload = buildPayload();
    if (!payload) return;
    const tested = fingerprint(values);
    try {
      const result = await onTestCandidate(payload);
      setTestedFingerprint(result.isValid ? tested : null);
    } catch {
      setTestedFingerprint(null);
    }
  }

  function save() {
    const payload = buildPayload();
    if (!payload || (values.isEnabled && !candidateIsCurrent)) return;
    onSave(payload);
  }

  const diagnosticMessage = (messageKey: string, params?: Record<string, string | number | boolean> | null) =>
    resolveAdManagementApiMessage(t, { messageKey, messageParams: params }, "settings:adManagement.connection.lastValidationUnknown");

  // Persisted outcome of the last saved-configuration validation. Shown before any test is run in
  // this session so the administrator can see the recorded state without re-probing the directory.
  const lastValidationSummary = resolveLastValidationMessage(t, settings?.lastValidationMessage);

  return (
    <div className="space-y-6">
      <UnsavedChangesGuard when={isDirty} title={t("settings:unsaved.title")} description={t("settings:unsaved.description")} leaveText={t("settings:unsaved.leave")} stayText={t("settings:unsaved.stay")} />

      <SettingsSection title={t("settings:adManagement.connection.sections.basic.title")} description={t("settings:adManagement.connection.sections.basic.description")}>
        <label className="flex items-start gap-3 rounded-lg border bg-muted/25 p-4" htmlFor="ad-mgmt-is-enabled">
          <Switch id="ad-mgmt-is-enabled" checked={values.isEnabled} onCheckedChange={(checked) => update("isEnabled", checked)} disabled={readOnly || busy} />
          <span><span className="block text-sm font-medium">{t("settings:adManagement.connection.fields.isEnabled")}</span><span className="mt-1 block text-sm text-muted-foreground">{t("settings:adManagement.connection.fields.isEnabledHelp")}</span></span>
        </label>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="ad-mgmt-domain-fqdn" label={t("settings:adManagement.connection.fields.domainFqdn")} description={t("settings:adManagement.connection.fields.domainFqdnHelp")} error={validationAttempted ? errors.domainFqdn : undefined}>
            <Input id="ad-mgmt-domain-fqdn" value={values.domainFqdn} onChange={(event) => update("domainFqdn", event.target.value)} readOnly={readOnly} disabled={busy} placeholder="example.local" />
          </SettingsField>
          <SettingsField id="ad-mgmt-netbios" label={t("settings:adManagement.connection.fields.netbiosDomainName")} description={t("settings:adManagement.connection.fields.netbiosDomainNameHelp")} error={validationAttempted ? errors.netbiosDomainName : undefined}>
            <Input id="ad-mgmt-netbios" value={values.netbiosDomainName} onChange={(event) => update("netbiosDomainName", event.target.value)} readOnly={readOnly} disabled={busy} placeholder="EXAMPLE" />
          </SettingsField>
          <SettingsField id="ad-mgmt-default-nc" label={t("settings:adManagement.connection.fields.defaultNamingContext")} description={t("settings:adManagement.connection.fields.defaultNamingContextHelp")} error={validationAttempted ? errors.defaultNamingContext : undefined}>
            <Input id="ad-mgmt-default-nc" value={values.defaultNamingContext} onChange={(event) => update("defaultNamingContext", event.target.value)} readOnly={readOnly} disabled={busy} />
          </SettingsField>
          <SettingsField id="ad-mgmt-base-dn" label={t("settings:adManagement.connection.fields.baseDn")} description={t("settings:adManagement.connection.fields.baseDnHelp")} error={validationAttempted ? errors.baseDn : undefined}>
            <Input id="ad-mgmt-base-dn" value={values.baseDn} onChange={(event) => update("baseDn", event.target.value)} readOnly={readOnly} disabled={busy} placeholder="DC=example,DC=local" />
          </SettingsField>
        </div>
      </SettingsSection>

      <SettingsSection title={t("settings:adManagement.connection.sections.domainControllers.title")} description={t("settings:adManagement.connection.sections.domainControllers.description")}>
        <div className="space-y-3">
          {values.preferredDomainControllers.map((host, index) => (
            <div key={index} className="grid gap-2 rounded-lg border bg-muted/20 p-3 sm:grid-cols-[auto_1fr_auto] sm:items-center">
              <span className="text-xs font-medium text-muted-foreground">{index === 0 ? t("settings:adManagement.connection.domainControllers.primary") : t("settings:adManagement.connection.domainControllers.fallback", { index })}</span>
              <Input aria-label={t("settings:adManagement.connection.domainControllers.hostname", { index: index + 1 })} value={host} onChange={(event) => update("preferredDomainControllers", values.preferredDomainControllers.map((item, itemIndex) => itemIndex === index ? event.target.value : item))} disabled={readOnly || busy} placeholder={`dc${index + 1}.example.local`} />
              {!readOnly ? <div className="flex gap-1">
                <Button type="button" size="icon" variant="ghost" aria-label={t("settings:adManagement.connection.domainControllers.moveUp")} disabled={busy || index === 0} onClick={() => { const next = [...values.preferredDomainControllers]; [next[index - 1], next[index]] = [next[index], next[index - 1]]; update("preferredDomainControllers", next); }}><ArrowUp className="size-4" /></Button>
                <Button type="button" size="icon" variant="ghost" aria-label={t("settings:adManagement.connection.domainControllers.moveDown")} disabled={busy || index === values.preferredDomainControllers.length - 1} onClick={() => { const next = [...values.preferredDomainControllers]; [next[index], next[index + 1]] = [next[index + 1], next[index]]; update("preferredDomainControllers", next); }}><ArrowDown className="size-4" /></Button>
                <Button type="button" size="icon" variant="ghost" aria-label={t("settings:adManagement.connection.domainControllers.remove")} disabled={busy || values.preferredDomainControllers.length === 1} onClick={() => update("preferredDomainControllers", values.preferredDomainControllers.filter((_, itemIndex) => itemIndex !== index))}><Trash2 className="size-4" /></Button>
              </div> : null}
            </div>
          ))}
          {!readOnly ? <Button type="button" variant="outline" onClick={() => update("preferredDomainControllers", [...values.preferredDomainControllers, ""])} disabled={busy}><Plus className="size-4" />{t("settings:adManagement.connection.domainControllers.add")}</Button> : null}
          <p className="text-sm text-muted-foreground">{t("settings:adManagement.connection.fields.preferredDomainControllersHelp")}</p>
        </div>
      </SettingsSection>

      <SettingsSection title={t("settings:adManagement.connection.sections.serviceAccount.title")} description={t("settings:adManagement.connection.sections.serviceAccount.description")}>
        <Alert><AlertDescription>{t("settings:adManagement.connection.fields.ldapsHelp")}</AlertDescription></Alert>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="ad-mgmt-service-account" label={t("settings:adManagement.connection.fields.serviceAccountUserName")} description={t("settings:adManagement.connection.fields.serviceAccountUserNameHelp")} error={validationAttempted ? errors.serviceAccountUserName : undefined}>
            <Input id="ad-mgmt-service-account" value={values.serviceAccountUserName} onChange={(event) => update("serviceAccountUserName", event.target.value)} readOnly={readOnly} disabled={busy} autoComplete="username" />
          </SettingsField>
          <SettingsField id="ad-mgmt-service-password" label={t("settings:adManagement.connection.fields.serviceAccountPassword")} description={t("settings:adManagement.connection.fields.serviceAccountPasswordHelp")} error={validationAttempted ? errors.serviceAccountPassword : undefined}>
            <SecretInput id="ad-mgmt-service-password" value={values.serviceAccountPassword} onChange={(event) => update("serviceAccountPassword", event.target.value)} readOnly={readOnly || values.clearServiceAccountPassword} disabled={busy || values.clearServiceAccountPassword} hasStoredValue={hasPassword} storedLabel={t("settings:adManagement.connection.passwordStored")} storedHint={t("settings:adManagement.connection.passwordKeepHint")} showLabel={t("settings:actions.showSecret")} hideLabel={t("settings:actions.hideSecret")} />
          </SettingsField>
        </div>
        {!readOnly ? <label className="flex items-center gap-2 text-sm" htmlFor="ad-mgmt-clear-password"><Checkbox id="ad-mgmt-clear-password" checked={values.clearServiceAccountPassword} onChange={(event) => { update("clearServiceAccountPassword", event.target.checked); if (event.target.checked) update("serviceAccountPassword", ""); }} disabled={busy} />{t("settings:adManagement.connection.clearPassword")}</label> : null}
      </SettingsSection>

      <SettingsSection title={t("settings:adManagement.connection.sections.powerShell.title")} description={t("settings:adManagement.connection.sections.powerShell.description")}>
        <div className="grid gap-5 md:grid-cols-2">
          <label className="flex items-start gap-3 rounded-lg border bg-muted/25 p-4" htmlFor="ad-mgmt-ps-enabled"><Checkbox id="ad-mgmt-ps-enabled" checked={values.powerShellHealthEnabled} onChange={(event) => update("powerShellHealthEnabled", event.target.checked)} disabled={readOnly || busy} /><span className="text-sm text-muted-foreground">{t("settings:adManagement.connection.fields.powerShellHealthEnabledHelp")}</span></label>
          <SettingsField id="ad-mgmt-ps-timeout" label={t("settings:adManagement.connection.fields.powerShellTimeoutSeconds")} error={validationAttempted ? errors.powerShellTimeoutSeconds : undefined}>
            <Input id="ad-mgmt-ps-timeout" type="number" min={5} max={300} value={values.powerShellTimeoutSeconds} onChange={(event) => update("powerShellTimeoutSeconds", event.target.value)} readOnly={readOnly} disabled={busy} />
          </SettingsField>
        </div>
      </SettingsSection>

      {settings?.lastValidationStatus ? (
        <div className="rounded-md border border-dashed px-3 py-2 text-sm">
          <p className="font-medium">{t("settings:adManagement.connection.lastValidationTitle")}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {t("settings:adManagement.connection.lastValidation", {
              status: settings.lastValidationStatus,
              message: lastValidationSummary,
            })}
          </p>
        </div>
      ) : null}

      {candidateValidation ? <ConnectionDiagnosticsPanel title={t("settings:adManagement.connection.diagnostics.candidateTitle")} description={candidateIsCurrent ? t("settings:adManagement.connection.diagnostics.candidateCurrent") : t("settings:adManagement.connection.diagnostics.candidateStale")} isValid={candidateValidation.isValid && candidateIsCurrent} checkedAt={candidateValidation.checkedAt} details={candidateValidation.details} resolveMessage={(detail) => diagnosticMessage(detail.messageKey, detail.messageParams)} successLabel={t("settings:adManagement.connection.diagnostics.success")} failureLabel={t("settings:adManagement.connection.diagnostics.failed")} warningLabel={t("settings:adManagement.connection.diagnostics.warning")} checkedAtLabel={t("settings:adManagement.connection.diagnostics.checkedAt")} /> : null}
      {savedValidation ? <ConnectionDiagnosticsPanel title={t("settings:adManagement.connection.diagnostics.savedTitle")} description={t("settings:adManagement.connection.diagnostics.savedDescription")} isValid={savedValidation.isValid} checkedAt={savedValidation.checkedAt} details={savedValidation.details} resolveMessage={(detail) => diagnosticMessage(detail.messageKey, detail.messageParams)} successLabel={t("settings:adManagement.connection.diagnostics.success")} failureLabel={t("settings:adManagement.connection.diagnostics.failed")} warningLabel={t("settings:adManagement.connection.diagnostics.warning")} checkedAtLabel={t("settings:adManagement.connection.diagnostics.checkedAt")} /> : null}

      {!readOnly ? <SettingsFormActions state={isSaving ? "saving" : saveError || validationError ? "error" : isDirty ? "dirty" : "pristine"} stateLabel={t(`settings:saveStates.${isSaving ? "saving" : saveError || validationError ? "error" : isDirty ? "dirty" : "pristine"}`)} errorTitle={t("settings:saveStates.failedTitle")} errorMessage={saveError ?? validationError}>
        {settings?.isConfigured ? <Button type="button" variant="outline" onClick={() => void onTestSaved()} disabled={busy}>{isTestingSaved ? t("settings:adManagement.connection.actions.testing") : t("settings:adManagement.connection.actions.testSaved")}</Button> : null}
        <Button type="button" variant="outline" onClick={() => void testCandidate()} disabled={busy || !settings}>{isTestingCandidate ? t("settings:adManagement.connection.actions.testing") : t("settings:adManagement.connection.actions.testCandidate")}</Button>
        <Button type="button" onClick={save} disabled={busy || !settings || !formValid || !isDirty || (values.isEnabled && !candidateIsCurrent)}>{t("common:actions.save")}</Button>
      </SettingsFormActions> : null}
    </div>
  );
}
