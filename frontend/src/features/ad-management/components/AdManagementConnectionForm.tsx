import { useMemo, useState } from "react";
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
  defaultAdManagementNotificationSettings,
} from "@/features/ad-management/ad-management-settings-payload";
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
  usersRootOu: string;
  disabledUsersOu: string;
  groupsSearchBase: string;
  computersSearchBase: string;
  preferredDomainControllers: string;
  useSsl: boolean;
  ldapPort: string;
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
    usersRootOu: settings?.usersRootOu ?? "",
    disabledUsersOu: settings?.disabledUsersOu ?? "",
    groupsSearchBase: settings?.groupsSearchBase ?? "",
    computersSearchBase: settings?.computersSearchBase ?? "",
    preferredDomainControllers:
      settings?.preferredDomainControllers?.join("\n") ?? "",
    useSsl: settings?.useSsl ?? true,
    ldapPort: String(settings?.ldapPort ?? 636),
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
    const port = Number.parseInt(values.ldapPort, 10);
    if (!Number.isFinite(port) || port < 1 || port > 65535) {
      return false;
    }
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
      values.usersRootOu,
      values.disabledUsersOu,
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
    values.disabledUsersOu,
    values.domainFqdn,
    values.isEnabled,
    values.ldapPort,
    values.netbiosDomainName,
    values.powerShellTimeoutSeconds,
    values.serviceAccountPassword,
    values.serviceAccountUserName,
    values.usersRootOu,
    values.clearServiceAccountPassword,
  ]);

  function update<K extends keyof AdManagementConnectionFormValues>(
    field: K,
    value: AdManagementConnectionFormValues[K],
  ) {
    setValues((prev) => ({ ...prev, [field]: value }));
  }

  function handleSave() {
    if (!canSubmit) {
      if (values.isEnabled) {
        toast.error(t("settings:adManagement.connection.messages.requiredFieldsMissing"));
      }
      return;
    }

    const ldapPort = Number.parseInt(values.ldapPort, 10);
    const timeout = Number.parseInt(values.powerShellTimeoutSeconds, 10);

    const baseSettings: AdManagementSettings = settings ?? {
      isConfigured: false,
      isEnabled: false,
      domainFqdn: null,
      defaultUserCreationUpnSuffix: null,
      netbiosDomainName: null,
      defaultNamingContext: null,
      baseDn: null,
      usersRootOu: null,
      disabledUsersOu: null,
      groupsSearchBase: null,
      computersSearchBase: null,
      preferredDomainControllers: [],
      useSsl: true,
      ldapPort: 636,
      serviceAccountUserName: null,
      hasServiceAccountPassword: false,
      powerShellHealthEnabled: false,
      powerShellTimeoutSeconds: 30,
      lastValidatedAt: null,
      lastValidationStatus: null,
      lastValidationMessage: null,
      notificationSettings: defaultAdManagementNotificationSettings(),
    };

    onSave(
      buildUpdateAdManagementSettingsPayload(baseSettings, {
        isEnabled: values.isEnabled,
        domainFqdn: emptyToNull(values.domainFqdn),
        netbiosDomainName: emptyToNull(values.netbiosDomainName),
        defaultNamingContext: emptyToNull(values.defaultNamingContext),
        baseDn: emptyToNull(values.baseDn),
        usersRootOu: emptyToNull(values.usersRootOu),
        disabledUsersOu: emptyToNull(values.disabledUsersOu),
        groupsSearchBase: emptyToNull(values.groupsSearchBase),
        computersSearchBase: emptyToNull(values.computersSearchBase),
        preferredDomainControllers: parsePreferredDcs(values.preferredDomainControllers),
        useSsl: values.useSsl,
        ldapPort,
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
          id="ad-mgmt-base-dn"
          label={t("settings:adManagement.connection.fields.baseDn")}
          value={values.baseDn}
          onChange={(value) => update("baseDn", value)}
          readOnly={readOnly}
          placeholder="DC=corp,DC=example,DC=com"
        />
        <FieldText
          id="ad-mgmt-default-nc"
          label={t("settings:adManagement.connection.fields.defaultNamingContext")}
          value={values.defaultNamingContext}
          onChange={(value) => update("defaultNamingContext", value)}
          readOnly={readOnly}
        />
        <FieldText
          id="ad-mgmt-users-root-ou"
          label={t("settings:adManagement.connection.fields.usersRootOu")}
          value={values.usersRootOu}
          onChange={(value) => update("usersRootOu", value)}
          readOnly={readOnly}
          placeholder="OU=Users,DC=corp,DC=example,DC=com"
        />
        <FieldText
          id="ad-mgmt-disabled-users-ou"
          label={t("settings:adManagement.connection.fields.disabledUsersOu")}
          value={values.disabledUsersOu}
          onChange={(value) => update("disabledUsersOu", value)}
          readOnly={readOnly}
        />
        <FieldText
          id="ad-mgmt-groups-search-base"
          label={t("settings:adManagement.connection.fields.groupsSearchBase")}
          value={values.groupsSearchBase}
          onChange={(value) => update("groupsSearchBase", value)}
          readOnly={readOnly}
        />
        <FieldText
          id="ad-mgmt-computers-search-base"
          label={t("settings:adManagement.connection.fields.computersSearchBase")}
          value={values.computersSearchBase}
          onChange={(value) => update("computersSearchBase", value)}
          readOnly={readOnly}
        />
        <div className="space-y-1.5 md:col-span-2">
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

        <div className="space-y-1.5">
          <Label htmlFor="ad-mgmt-ldap-port">
            {t("settings:adManagement.connection.fields.ldapPort")}
          </Label>
          <Input
            id="ad-mgmt-ldap-port"
            type="number"
            min={1}
            max={65535}
            value={values.ldapPort}
            onChange={(event) => update("ldapPort", event.target.value)}
            readOnly={readOnly}
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="ad-mgmt-use-ssl">
            {t("settings:adManagement.connection.fields.useSsl")}
          </Label>
          <div className="flex h-9 items-center gap-2">
            <Checkbox
              id="ad-mgmt-use-ssl"
              checked={values.useSsl}
              onChange={(event) => update("useSsl", event.target.checked)}
              disabled={readOnly}
            />
            <label
              htmlFor="ad-mgmt-use-ssl"
              className="cursor-pointer text-sm text-muted-foreground"
            >
              {t("settings:adManagement.connection.fields.useSslHelp")}
            </label>
          </div>
        </div>

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

      {settings?.lastValidationStatus ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-xs text-muted-foreground">
          {t("settings:adManagement.connection.lastValidation", {
            status: settings.lastValidationStatus,
            message: settings.lastValidationMessage ?? "-",
          })}
        </p>
      ) : null}

      {!readOnly ? (
        <div className="flex flex-wrap justify-end gap-2">
          <Button onClick={handleSave} disabled={!canSubmit || isSaving}>
            {t("settings:adManagement.connection.actions.save")}
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
