import { useMemo, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Switch } from "@/components/ui/switch";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  buildUpdateAdManagementSettingsPayload,
} from "@/features/ad-management/ad-management-settings-payload";
import { resolveAdManagementApiMessage } from "@/features/ad-management/ad-management-api-message";
import type {
  AdManagementSettings,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";

export type AdManagementConnectionFormValues = {
  isEnabled: boolean;
  domainFqdn: string;
  netbiosDomainName: string;
  defaultNamingContext: string;
  baseDn: string;
  preferredDomainControllers: string;
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
  onSave: (payload: UpdateAdManagementSettingsRequest) => void;
};

function buildInitialValues(
  settings: AdManagementSettings | undefined,
): AdManagementConnectionFormValues {
  return {
    isEnabled: settings?.isEnabled ?? false,
    domainFqdn: settings?.domainFqdn ?? "",
    netbiosDomainName: settings?.netbiosDomainName ?? "",
    defaultNamingContext: settings?.defaultNamingContext ?? "",
    baseDn: settings?.baseDn ?? "",
    preferredDomainControllers:
      settings?.preferredDomainControllers?.join("\n") ?? "",
    serviceAccountUserName: settings?.serviceAccountUserName ?? "",
    serviceAccountPassword: "",
    clearServiceAccountPassword: false,
    powerShellHealthEnabled: settings?.powerShellHealthEnabled ?? false,
    powerShellTimeoutSeconds: String(settings?.powerShellTimeoutSeconds ?? 30),
  };
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function parsePreferredDcs(value: string): string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const rawLine of value.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line) continue;
    if (seen.has(line.toLowerCase())) continue;
    seen.add(line.toLowerCase());
    result.push(line);
  }
  return result;
}

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

function FormSection({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <section className="space-y-4 rounded-lg border bg-card p-4">
      <div>
        <h3 className="text-sm font-semibold">{title}</h3>
        {description ? (
          <p className="mt-1 text-xs text-muted-foreground">{description}</p>
        ) : null}
      </div>
      {children}
    </section>
  );
}

export function AdManagementConnectionForm({
  settings,
  readOnly,
  isSaving,
  onSave,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const [values, setValues] = useState<AdManagementConnectionFormValues>(
    () => buildInitialValues(settings),
  );

  const hasPassword = settings?.hasServiceAccountPassword ?? false;

  const canSubmit = useMemo(() => {
    const timeout = Number.parseInt(values.powerShellTimeoutSeconds, 10);
    if (!Number.isFinite(timeout) || timeout < 5 || timeout > 300) {
      return false;
    }

    if (!values.isEnabled) {
      return true;
    }

    const requiredFields = [
      values.domainFqdn,
      values.netbiosDomainName,
      values.defaultNamingContext,
      values.baseDn,
      values.serviceAccountUserName,
    ];
    if (requiredFields.some((field) => field.trim().length === 0)) {
      return false;
    }

    const hasServicePassword =
      values.serviceAccountPassword.trim().length > 0 ||
      (hasPassword && !values.clearServiceAccountPassword);
    if (!hasServicePassword) {
      return false;
    }

    return true;
  }, [
    hasPassword,
    values.baseDn,
    values.defaultNamingContext,
    values.domainFqdn,
    values.isEnabled,
    values.netbiosDomainName,
    values.powerShellTimeoutSeconds,
    values.serviceAccountPassword,
    values.serviceAccountUserName,
    values.clearServiceAccountPassword,
  ]);

  function update<K extends keyof AdManagementConnectionFormValues>(
    field: K,
    value: AdManagementConnectionFormValues[K],
  ) {
    setValues((prev) => ({ ...prev, [field]: value }));
  }

  function handleSave() {
    if (!settings) {
      return;
    }

    if (!canSubmit) {
      if (values.isEnabled) {
        toast.error(t("settings:adManagement.connection.messages.requiredFieldsMissing"));
      }
      return;
    }

    const timeout = Number.parseInt(values.powerShellTimeoutSeconds, 10);

    onSave(
      buildUpdateAdManagementSettingsPayload(settings, {
        isEnabled: values.isEnabled,
        domainFqdn: emptyToNull(values.domainFqdn),
        netbiosDomainName: emptyToNull(values.netbiosDomainName),
        defaultNamingContext: emptyToNull(values.defaultNamingContext),
        baseDn: emptyToNull(values.baseDn),
        preferredDomainControllers: parsePreferredDcs(values.preferredDomainControllers),
        serviceAccountUserName: emptyToNull(values.serviceAccountUserName),
        serviceAccountPassword: values.serviceAccountPassword.trim().length === 0
          ? null
          : values.serviceAccountPassword,
        clearServiceAccountPassword: values.clearServiceAccountPassword,
        powerShellHealthEnabled: values.powerShellHealthEnabled,
        powerShellTimeoutSeconds: timeout,
      }),
    );
  }

  return (
    <div className="space-y-4">
      <FormSection
        title={t("settings:adManagement.connection.sections.basic.title")}
        description={t("settings:adManagement.connection.sections.basic.description")}
      >
        <div className="space-y-1.5">
          <div className="flex items-center gap-3">
            <Switch
              id="ad-mgmt-is-enabled"
              checked={values.isEnabled}
              onCheckedChange={(checked) => update("isEnabled", checked)}
              disabled={readOnly}
            />
            <label htmlFor="ad-mgmt-is-enabled" className="cursor-pointer text-sm font-medium">
              {t("settings:adManagement.connection.fields.isEnabled")}
            </label>
          </div>
          <p className="text-xs text-muted-foreground">
            {t("settings:adManagement.connection.fields.isEnabledHelp")}
          </p>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <FieldText
            id="ad-mgmt-domain-fqdn"
            label={t("settings:adManagement.connection.fields.domainFqdn")}
            value={values.domainFqdn}
            onChange={(value) => update("domainFqdn", value)}
            readOnly={readOnly}
            placeholder="corp.example.com"
          />
          <FieldText
            id="ad-mgmt-netbios"
            label={t("settings:adManagement.connection.fields.netbiosDomainName")}
            value={values.netbiosDomainName}
            onChange={(value) => update("netbiosDomainName", value)}
            readOnly={readOnly}
            placeholder="CORP"
          />
          <FieldText
            id="ad-mgmt-default-nc"
            label={t("settings:adManagement.connection.fields.defaultNamingContext")}
            value={values.defaultNamingContext}
            onChange={(value) => update("defaultNamingContext", value)}
            readOnly={readOnly}
          />
          <FieldText
            id="ad-mgmt-base-dn"
            label={t("settings:adManagement.connection.fields.baseDn")}
            value={values.baseDn}
            onChange={(value) => update("baseDn", value)}
            readOnly={readOnly}
            placeholder="DC=corp,DC=example,DC=com"
          />
        </div>
      </FormSection>

      <FormSection
        title={t("settings:adManagement.connection.sections.serviceAccount.title")}
        description={t("settings:adManagement.connection.sections.serviceAccount.description")}
      >
        <p className="text-xs text-muted-foreground">
          {t("settings:adManagement.connection.fields.ldapsHelp")}
        </p>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="ad-mgmt-service-account">
              {t("settings:adManagement.connection.fields.serviceAccountUserName")}
            </Label>
            <Input
              id="ad-mgmt-service-account"
              value={values.serviceAccountUserName}
              onChange={(event) => update("serviceAccountUserName", event.target.value)}
              readOnly={readOnly}
              placeholder="svc_ad_mgmt"
            />
            <p className="text-xs text-muted-foreground">
              {t("settings:adManagement.connection.fields.serviceAccountUserNameHelp")}
            </p>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="ad-mgmt-service-password">
              {t("settings:adManagement.connection.fields.serviceAccountPassword")}
            </Label>
            <Input
              id="ad-mgmt-service-password"
              type="password"
              value={values.serviceAccountPassword}
              onChange={(event) => update("serviceAccountPassword", event.target.value)}
              readOnly={readOnly || values.clearServiceAccountPassword}
              autoComplete="new-password"
            />
            <div className="space-y-1 text-xs text-muted-foreground">
              {hasPassword ? (
                <span>{t("settings:adManagement.connection.passwordStored")}</span>
              ) : null}
              <span>{t("settings:adManagement.connection.passwordKeepHint")}</span>
            </div>
            {!readOnly ? (
              <div className="flex items-center gap-2 pt-1">
                <Checkbox
                  id="ad-mgmt-clear-password"
                  checked={values.clearServiceAccountPassword}
                  onChange={(event) => {
                    const next = event.target.checked;
                    update("clearServiceAccountPassword", next);
                    if (next) {
                      update("serviceAccountPassword", "");
                    }
                  }}
                />
                <label htmlFor="ad-mgmt-clear-password" className="cursor-pointer text-xs">
                  {t("settings:adManagement.connection.clearPassword")}
                </label>
              </div>
            ) : null}
          </div>
        </div>
      </FormSection>

      <FormSection
        title={t("settings:adManagement.connection.sections.domainControllers.title")}
        description={t("settings:adManagement.connection.sections.domainControllers.description")}
      >
        <div className="space-y-1.5">
          <Label htmlFor="ad-mgmt-preferred-dcs">
            {t("settings:adManagement.connection.fields.preferredDomainControllers")}
          </Label>
          <Textarea
            id="ad-mgmt-preferred-dcs"
            value={values.preferredDomainControllers}
            onChange={(event) => update("preferredDomainControllers", event.target.value)}
            readOnly={readOnly}
            rows={3}
            placeholder={"dc01.corp.example.com\ndc02.corp.example.com"}
          />
          <p className="text-xs text-muted-foreground">
            {t("settings:adManagement.connection.fields.preferredDomainControllersHelp")}
          </p>
        </div>
      </FormSection>

      <FormSection
        title={t("settings:adManagement.connection.sections.powerShell.title")}
        description={t("settings:adManagement.connection.sections.powerShell.description")}
      >
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="ad-mgmt-ps-enabled">
              {t("settings:adManagement.connection.fields.powerShellHealthEnabled")}
            </Label>
            <div className="flex h-9 items-center gap-2">
              <Checkbox
                id="ad-mgmt-ps-enabled"
                checked={values.powerShellHealthEnabled}
                onChange={(event) => update("powerShellHealthEnabled", event.target.checked)}
                disabled={readOnly}
              />
              <label
                htmlFor="ad-mgmt-ps-enabled"
                className="cursor-pointer text-sm text-muted-foreground"
              >
                {t("settings:adManagement.connection.fields.powerShellHealthEnabledHelp")}
              </label>
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="ad-mgmt-ps-timeout">
              {t("settings:adManagement.connection.fields.powerShellTimeoutSeconds")}
            </Label>
            <Input
              id="ad-mgmt-ps-timeout"
              type="number"
              min={5}
              max={300}
              value={values.powerShellTimeoutSeconds}
              onChange={(event) => update("powerShellTimeoutSeconds", event.target.value)}
              readOnly={readOnly}
            />
          </div>
        </div>
      </FormSection>

      {settings?.lastValidationStatus ? (
        <div className="rounded-md border border-dashed px-3 py-2 text-sm">
          <p className="font-medium">
            {t("settings:adManagement.connection.lastValidationTitle")}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {t("settings:adManagement.connection.lastValidation", {
              status: settings.lastValidationStatus,
              message: resolveLastValidationMessage(t, settings.lastValidationMessage),
            })}
          </p>
        </div>
      ) : null}

      {!readOnly ? (
        <div className="flex flex-wrap justify-end gap-2">
          <Button onClick={handleSave} disabled={!settings || !canSubmit || isSaving}>
            {t("common:actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

function FieldText({
  id,
  label,
  value,
  onChange,
  readOnly,
  placeholder,
  helpText,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  readOnly: boolean;
  placeholder?: string;
  helpText?: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        readOnly={readOnly}
        placeholder={placeholder}
      />
      {helpText ? (
        <p className="text-xs text-muted-foreground">{helpText}</p>
      ) : null}
    </div>
  );
}
