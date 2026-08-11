import { useTranslation } from "react-i18next";

import { ConnectionDiagnosticsPanel } from "@/components/common/ConnectionDiagnosticsPanel";
import {
  SecretInput,
  SettingsField,
  SettingsFormActions,
  SettingsSection,
} from "@/components/common/settings-form";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import type { ValidateLdapSettingsResponse } from "@/features/settings/types";

export type LdapFormValues = {
  name: string;
  host: string;
  baseDn: string;
  userSearchBase: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain: string;
  bindPassword: string;
  description: string;
  testUserName: string;
  testPassword: string;
};

type Props = {
  values: LdapFormValues;
  fieldErrors: Partial<Record<keyof LdapFormValues, string>>;
  hasBindPassword: boolean;
  hasSavedConfiguration: boolean;
  readOnly: boolean;
  savePending: boolean;
  testCandidatePending: boolean;
  testSavedPending: boolean;
  canSave: boolean;
  isDirty: boolean;
  candidateValidationIsCurrent: boolean;
  candidateValidation: ValidateLdapSettingsResponse | null;
  savedValidation: ValidateLdapSettingsResponse | null;
  saveError: string | null;
  saveSucceeded: boolean;
  onChange: <K extends keyof LdapFormValues>(field: K, value: LdapFormValues[K]) => void;
  onTestCandidate: () => void;
  onTestSaved: () => void;
  onSave: () => void;
};

export function LdapSettingsForm({
  values,
  fieldErrors,
  hasBindPassword,
  hasSavedConfiguration,
  readOnly,
  savePending,
  testCandidatePending,
  testSavedPending,
  canSave,
  isDirty,
  candidateValidationIsCurrent,
  candidateValidation,
  savedValidation,
  saveError,
  saveSucceeded,
  onChange,
  onTestCandidate,
  onTestSaved,
  onSave,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const busy = savePending || testCandidatePending || testSavedPending;
  const actionState = savePending
    ? "saving"
    : saveError
      ? "error"
      : isDirty
        ? "dirty"
        : saveSucceeded
          ? "saved"
          : "pristine";
  const diagnosticMessage = (messageKey: string, params?: Record<string, string | number | boolean> | null) => {
    const key = messageKey.replace("apiMessages.directoryDiagnostics.", "settings:ldap.diagnostics.messages.");
    return t(key, params ?? {});
  };

  return (
    <div className="space-y-6">
      <Alert>
        <AlertTitle>{t("settings:ldap.role.title")}</AlertTitle>
        <AlertDescription>{t("settings:ldap.role.description")}</AlertDescription>
      </Alert>

      <SettingsSection title={t("settings:ldap.sections.endpoint.title")} description={t("settings:ldap.sections.endpoint.description")}>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="ldap-name" label={t("settings:ldap.fields.name")} description={t("settings:ldap.help.name")} error={fieldErrors.name}>
            <Input id="ldap-name" value={values.name} onChange={(event) => onChange("name", event.target.value)} readOnly={readOnly} disabled={busy} />
          </SettingsField>
          <SettingsField id="ldap-host" label={t("settings:ldap.fields.host")} description={t("settings:ldap.help.host")} error={fieldErrors.host}>
            <Input id="ldap-host" value={values.host} onChange={(event) => onChange("host", event.target.value)} readOnly={readOnly} disabled={busy} placeholder="dc1.example.local" />
          </SettingsField>
          <SettingsField id="ldap-description" label={t("settings:ldap.fields.description")} description={t("settings:ldap.help.description")} optional optionalLabel={t("settings:fields.optional")} className="md:col-span-2">
            <Textarea id="ldap-description" value={values.description} onChange={(event) => onChange("description", event.target.value)} readOnly={readOnly} disabled={busy} rows={2} />
          </SettingsField>
        </div>
      </SettingsSection>

      <SettingsSection title={t("settings:ldap.sections.search.title")} description={t("settings:ldap.sections.search.description")}>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="ldap-base-dn" label={t("settings:ldap.fields.baseDn")} description={t("settings:ldap.help.baseDn")} error={fieldErrors.baseDn}>
            <Input id="ldap-base-dn" value={values.baseDn} onChange={(event) => onChange("baseDn", event.target.value)} readOnly={readOnly} disabled={busy} placeholder="DC=example,DC=local" />
          </SettingsField>
          <SettingsField id="ldap-user-search-base" label={t("settings:ldap.fields.userSearchBase")} description={t("settings:ldap.help.userSearchBase")} optional optionalLabel={t("settings:fields.optional")}>
            <Input id="ldap-user-search-base" value={values.userSearchBase} onChange={(event) => onChange("userSearchBase", event.target.value)} readOnly={readOnly} disabled={busy} />
          </SettingsField>
          <SettingsField id="ldap-user-search-filter" label={t("settings:ldap.fields.userSearchFilter")} description={t("settings:ldap.help.userSearchFilter")} error={fieldErrors.userSearchFilter} className="md:col-span-2">
            <Input id="ldap-user-search-filter" value={values.userSearchFilter} onChange={(event) => onChange("userSearchFilter", event.target.value)} readOnly={readOnly} disabled={busy} className="font-mono" />
          </SettingsField>
        </div>
      </SettingsSection>

      <SettingsSection title={t("settings:ldap.sections.identity.title")} description={t("settings:ldap.sections.identity.description")}>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="ldap-bind-user" label={t("settings:ldap.fields.bindUserName")} description={t("settings:ldap.help.bindUserName")} error={fieldErrors.bindUserName}>
            <Input id="ldap-bind-user" value={values.bindUserName} onChange={(event) => onChange("bindUserName", event.target.value)} readOnly={readOnly} disabled={busy} autoComplete="username" />
          </SettingsField>
          <SettingsField id="ldap-bind-domain" label={t("settings:ldap.fields.bindUserDomain")} description={t("settings:ldap.help.bindUserDomain")} optional optionalLabel={t("settings:fields.optional")}>
            <Input id="ldap-bind-domain" value={values.bindUserDomain} onChange={(event) => onChange("bindUserDomain", event.target.value)} readOnly={readOnly} disabled={busy} placeholder="EXAMPLE or example.local" />
          </SettingsField>
          <SettingsField id="ldap-bind-password" label={t("settings:ldap.fields.bindPassword")} description={t("settings:ldap.help.bindPassword")} className="md:col-span-2">
            <SecretInput
              id="ldap-bind-password"
              value={values.bindPassword}
              onChange={(event) => onChange("bindPassword", event.target.value)}
              readOnly={readOnly}
              disabled={busy}
              hasStoredValue={hasBindPassword}
              storedLabel={t("settings:ldap.secret.stored")}
              storedHint={t("settings:ldap.secret.preserveHint")}
              showLabel={t("settings:actions.showSecret")}
              hideLabel={t("settings:actions.hideSecret")}
            />
          </SettingsField>
        </div>
      </SettingsSection>

      <SettingsSection title={t("settings:ldap.sections.loginTest.title")} description={t("settings:ldap.sections.loginTest.description")}>
        <div className="grid gap-5 md:grid-cols-2">
          <SettingsField id="ldap-test-user" label={t("settings:ldap.fields.testUserName")} description={t("settings:ldap.help.testUserName")} optional optionalLabel={t("settings:fields.optional")} error={fieldErrors.testUserName}>
            <Input id="ldap-test-user" value={values.testUserName} onChange={(event) => onChange("testUserName", event.target.value)} disabled={busy || readOnly} autoComplete="off" placeholder="user or user@example.local" />
          </SettingsField>
          <SettingsField id="ldap-test-password" label={t("settings:ldap.fields.testPassword")} description={t("settings:ldap.help.testPassword")} optional optionalLabel={t("settings:fields.optional")} error={fieldErrors.testPassword}>
            <SecretInput id="ldap-test-password" value={values.testPassword} onChange={(event) => onChange("testPassword", event.target.value)} disabled={busy || readOnly} storedLabel="" storedHint="" showLabel={t("settings:actions.showSecret")} hideLabel={t("settings:actions.hideSecret")} />
          </SettingsField>
        </div>
      </SettingsSection>

      {candidateValidation ? (
        <ConnectionDiagnosticsPanel
          title={t("settings:ldap.diagnostics.candidateTitle")}
          description={candidateValidationIsCurrent ? t("settings:ldap.diagnostics.candidateCurrent") : t("settings:ldap.diagnostics.candidateStale")}
          isValid={candidateValidation.isValid && candidateValidationIsCurrent}
          details={candidateValidation.details}
          resolveMessage={(detail) => diagnosticMessage(detail.messageKey, detail.messageParams)}
          successLabel={t("settings:ldap.diagnostics.success")}
          failureLabel={t("settings:ldap.diagnostics.failed")}
          warningLabel={t("settings:ldap.diagnostics.warning")}
        />
      ) : null}

      {savedValidation ? (
        <ConnectionDiagnosticsPanel
          title={t("settings:ldap.diagnostics.savedTitle")}
          description={t("settings:ldap.diagnostics.savedDescription")}
          isValid={savedValidation.isValid}
          details={savedValidation.details}
          resolveMessage={(detail) => diagnosticMessage(detail.messageKey, detail.messageParams)}
          successLabel={t("settings:ldap.diagnostics.success")}
          failureLabel={t("settings:ldap.diagnostics.failed")}
          warningLabel={t("settings:ldap.diagnostics.warning")}
        />
      ) : null}

      {!readOnly ? (
        <SettingsFormActions state={actionState} stateLabel={t(`settings:saveStates.${actionState}`)} errorTitle={t("settings:saveStates.failedTitle")} errorMessage={saveError}>
          {hasSavedConfiguration ? (
            <Button type="button" variant="outline" onClick={onTestSaved} disabled={busy}>
              {testSavedPending ? t("settings:ldap.actions.testing") : t("settings:ldap.actions.testSaved")}
            </Button>
          ) : null}
          <Button type="button" variant="outline" onClick={onTestCandidate} disabled={busy}>
            {testCandidatePending ? t("settings:ldap.actions.testing") : t("settings:ldap.actions.testCandidate")}
          </Button>
          <Button type="button" onClick={onSave} disabled={!canSave || busy || !isDirty}>
            {savePending ? t("settings:actions.saving") : t("common:actions.save")}
          </Button>
        </SettingsFormActions>
      ) : null}
    </div>
  );
}
