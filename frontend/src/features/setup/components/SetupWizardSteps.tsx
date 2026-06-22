import axios from "axios";
import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import type { SetupPreflightResponse } from "@/features/setup/api";
import {
  searchSetupAdminUsers,
  validateSetupLdap,
} from "@/features/setup/api";
import { SetupOuPicker } from "@/features/setup/components/SetupOuPicker";
import {
  buildCompleteSetupLdapPayload,
  canAddAdminUser,
  shouldFetchAdminUserSearchResults,
  type SetupAdminUserSelection,
  type SetupAdManagementFormValues,
  type SetupLdapFormValues,
  type SetupModulesFormValues,
  type SetupWizardFormValues,
} from "@/features/setup/setup-form";
import { resolvePreflightMessageKey } from "@/features/setup/setup-wizard-state";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

type FieldErrors = Partial<Record<string, string>>;

function FieldHint({ children }: { children: string }) {
  return <p className="text-xs text-muted-foreground">{children}</p>;
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="text-xs text-destructive">{message}</p>;
}

type SetupKeyStepProps = {
  setupKey: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  error?: string;
};

export function SetupKeyStep({ setupKey, onChange, disabled, error }: SetupKeyStepProps) {
  const { t } = useTranslation(["setup"]);

  return (
    <SectionCard title={t("setup:steps.setupKey.title")} description={t("setup:steps.setupKey.description")}>
      <div className="space-y-2">
        <Label htmlFor="setupKey">{t("setup:fields.setupKey")}</Label>
        <Input
          id="setupKey"
          type="password"
          autoComplete="off"
          value={setupKey}
          onChange={(event) => onChange(event.target.value)}
          disabled={disabled}
        />
        <FieldError message={error} />
      </div>
    </SectionCard>
  );
}

type ServerCheckStepProps = {
  preflight: SetupPreflightResponse | null;
  isLoading: boolean;
  errorMessage: string | null;
  onRetry: () => void;
};

export function ServerCheckStep({ preflight, isLoading, errorMessage, onRetry }: ServerCheckStepProps) {
  const { t } = useTranslation(["setup", "common"]);

  if (isLoading) {
    return <LoadingState text={t("setup:steps.serverCheck.loading")} />;
  }

  if (errorMessage) {
    return (
      <ErrorState
        title={t("setup:steps.serverCheck.errorTitle")}
        description={errorMessage}
        retry={
          <Button type="button" variant="outline" size="sm" onClick={onRetry}>
            {t("setup:actions.retry")}
          </Button>
        }
      />
    );
  }

  if (!preflight) {
    return <EmptyState title={t("setup:steps.serverCheck.emptyTitle")} description={t("setup:steps.serverCheck.emptyDescription")} />;
  }

  return (
    <SectionCard title={t("setup:steps.serverCheck.title")} description={t("setup:steps.serverCheck.description")}>
      <div className="space-y-3">
        {preflight.checks.map((check) => (
          <div
            key={check.key}
            className={cn(
              "rounded-lg border px-3 py-2",
              check.status === "ok" && "border-emerald-500/30 bg-emerald-500/5",
              check.status === "warning" && "border-amber-500/30 bg-amber-500/5",
              check.status === "error" && "border-destructive/30 bg-destructive/5",
            )}
          >
            <p className="text-sm font-medium">
              {t(resolvePreflightMessageKey(check.messageKey), { defaultValue: check.messageKey })}
            </p>
            {check.detail ? (
              <p className="mt-1 font-mono text-xs text-muted-foreground">{check.detail}</p>
            ) : null}
          </div>
        ))}
      </div>
      <div className="mt-4 flex justify-end">
        <Button type="button" variant="outline" onClick={onRetry}>
          {t("setup:actions.retry")}
        </Button>
      </div>
    </SectionCard>
  );
}

type LdapConnectionStepProps = {
  setupKey: string;
  ldap: SetupLdapFormValues;
  onChange: (ldap: SetupLdapFormValues) => void;
  ldapValidated: boolean;
  onValidatedChange: (validated: boolean) => void;
  disabled?: boolean;
  fieldErrors: FieldErrors;
};

export function LdapConnectionStep({
  setupKey,
  ldap,
  onChange,
  ldapValidated,
  onValidatedChange,
  disabled,
  fieldErrors,
}: LdapConnectionStepProps) {
  const { t } = useTranslation(["setup"]);
  const [isValidating, setIsValidating] = useState(false);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);

  const updateField = <K extends keyof SetupLdapFormValues>(field: K, value: SetupLdapFormValues[K]) => {
    setValidationMessage(null);
    onChange({ ...ldap, [field]: value });
  };

  const handleValidate = async () => {
    setIsValidating(true);
    setValidationMessage(null);
    try {
      const payload = buildCompleteSetupLdapPayload(ldap);
      const response = await validateSetupLdap({
        setupKey,
        host: payload.host,
        baseDn: payload.baseDn,
        userSearchFilter: payload.userSearchFilter,
        bindUserName: payload.bindUserName,
        bindUserDomain: payload.bindUserDomain,
        bindPassword: payload.bindPassword,
      });

      onValidatedChange(response.isValid);
      setValidationMessage(response.message);
    } catch (error) {
      onValidatedChange(false);
      const fallback = t("setup:steps.ldapConnection.validateFailed");
      setValidationMessage(axios.isAxiosError(error) ? getApiErrorMessage(error, fallback) : fallback);
    } finally {
      setIsValidating(false);
    }
  };

  return (
    <SectionCard title={t("setup:steps.ldapConnection.title")} description={t("setup:steps.ldapConnection.description")}>
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="ldapHost">{t("setup:fields.host")}</Label>
          <Input
            id="ldapHost"
            value={ldap.host}
            onChange={(event) => updateField("host", event.target.value)}
            disabled={disabled}
          />
          <FieldHint>{t("setup:helpers.host")}</FieldHint>
          <FieldError message={fieldErrors["ldap.host"]} />
        </div>

        <div className="space-y-2 md:col-span-2">
          <FieldHint>{t("setup:helpers.ldapsHelp")}</FieldHint>
        </div>

        <div className="space-y-2 md:col-span-2">
          <Label htmlFor="ldapBaseDn">{t("setup:fields.baseDn")}</Label>
          <Input
            id="ldapBaseDn"
            value={ldap.baseDn}
            onChange={(event) => updateField("baseDn", event.target.value)}
            disabled={disabled}
          />
          <FieldError message={fieldErrors["ldap.baseDn"]} />
        </div>

        <div className="space-y-2 md:col-span-2">
          <Label htmlFor="ldapUserSearchFilter">{t("setup:fields.userSearchFilter")}</Label>
          <Input
            id="ldapUserSearchFilter"
            value={ldap.userSearchFilter}
            onChange={(event) => updateField("userSearchFilter", event.target.value)}
            disabled={disabled}
          />
          <FieldHint>{t("setup:helpers.userSearchFilter")}</FieldHint>
          <FieldError message={fieldErrors["ldap.userSearchFilter"]} />
        </div>

        <div className="space-y-2">
          <Label htmlFor="ldapBindUserName">{t("setup:fields.bindUserName")}</Label>
          <Input
            id="ldapBindUserName"
            value={ldap.bindUserName}
            onChange={(event) => updateField("bindUserName", event.target.value)}
            disabled={disabled}
          />
          <FieldHint>{t("setup:helpers.bindUserName")}</FieldHint>
          <FieldError message={fieldErrors["ldap.bindUserName"]} />
        </div>

        <div className="space-y-2">
          <Label htmlFor="ldapBindUserDomain">{t("setup:fields.bindUserDomain")}</Label>
          <Input
            id="ldapBindUserDomain"
            value={ldap.bindUserDomain}
            onChange={(event) => updateField("bindUserDomain", event.target.value)}
            disabled={disabled}
          />
          <FieldHint>{t("setup:helpers.bindUserDomain")}</FieldHint>
        </div>

        <div className="space-y-2 md:col-span-2">
          <Label htmlFor="ldapBindPassword">{t("setup:fields.bindPassword")}</Label>
          <Input
            id="ldapBindPassword"
            type="password"
            autoComplete="off"
            value={ldap.bindPassword}
            onChange={(event) => updateField("bindPassword", event.target.value)}
            disabled={disabled}
          />
          <FieldError message={fieldErrors["ldap.bindPassword"]} />
        </div>
      </div>

      <div className="mt-4 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          {validationMessage ? (
            <p className={cn("text-sm", ldapValidated ? "text-emerald-600 dark:text-emerald-400" : "text-destructive")}>
              {validationMessage}
            </p>
          ) : null}
          {!ldapValidated ? <FieldHint>{t("setup:steps.ldapConnection.validateRequired")}</FieldHint> : null}
        </div>
        <Button type="button" variant="outline" onClick={() => void handleValidate()} disabled={disabled || isValidating}>
          {isValidating ? t("setup:actions.validatingLdap") : t("setup:actions.validateLdap")}
        </Button>
      </div>
    </SectionCard>
  );
}

type ModulesStepProps = {
  setupKey: string;
  ldap: SetupLdapFormValues;
  modules: SetupModulesFormValues;
  onChange: (modules: SetupModulesFormValues) => void;
  ldapValidated: boolean;
  disabled?: boolean;
};

function updateAdManagement(
  current: SetupAdManagementFormValues,
  patch: Partial<SetupAdManagementFormValues>,
): SetupAdManagementFormValues {
  return { ...current, ...patch };
}

export function ModulesStep({
  setupKey,
  ldap,
  modules,
  onChange,
  ldapValidated,
  disabled,
}: ModulesStepProps) {
  const { t } = useTranslation(["setup"]);
  const adManagement = modules.adManagement;
  const pickerDisabled = disabled || !ldapValidated;

  const setAdManagement = (next: SetupAdManagementFormValues) => {
    onChange({ adManagement: next });
  };

  return (
    <SectionCard title={t("setup:steps.modules.title")} description={t("setup:steps.modules.description")}>
      {!ldapValidated ? <FieldHint>{t("setup:steps.modules.ldapRequired")}</FieldHint> : null}

      <div className="flex items-center justify-between rounded-lg border px-3 py-3">
        <div>
          <p className="text-sm font-medium">{t("setup:modules.adManagement.enable")}</p>
          <p className="text-xs text-muted-foreground">{t("setup:modules.adManagement.enableDescription")}</p>
        </div>
        <Switch
          checked={adManagement.isEnabled}
          onCheckedChange={(checked) =>
            setAdManagement(updateAdManagement(adManagement, { isEnabled: checked }))
          }
          disabled={disabled}
        />
      </div>

      {adManagement.isEnabled ? (
        <div className="mt-4 grid gap-4">
          <SetupOuPicker
            id="usersSearchBase"
            label={t("setup:modules.adManagement.usersSearchBase")}
            value={adManagement.usersSearchBase}
            onChange={(value) => setAdManagement(updateAdManagement(adManagement, { usersSearchBase: value }))}
            setupKey={setupKey}
            ldap={ldap}
            disabled={pickerDisabled}
            required
          />
          <SetupOuPicker
            id="groupsSearchBase"
            label={t("setup:modules.adManagement.groupsSearchBase")}
            value={adManagement.groupsSearchBase}
            onChange={(value) => setAdManagement(updateAdManagement(adManagement, { groupsSearchBase: value }))}
            setupKey={setupKey}
            ldap={ldap}
            disabled={pickerDisabled}
            required
          />
          <SetupOuPicker
            id="computersSearchBase"
            label={t("setup:modules.adManagement.computersSearchBase")}
            value={adManagement.computersSearchBase}
            onChange={(value) => setAdManagement(updateAdManagement(adManagement, { computersSearchBase: value }))}
            setupKey={setupKey}
            ldap={ldap}
            disabled={pickerDisabled}
            required
          />
          <div className="space-y-2">
            <SetupOuPicker
              id="disabledUsersOu"
              label={t("setup:modules.adManagement.disabledUsersOu")}
              value={adManagement.disabledUsersOu}
              onChange={(value) => setAdManagement(updateAdManagement(adManagement, { disabledUsersOu: value }))}
              setupKey={setupKey}
              ldap={ldap}
              disabled={pickerDisabled}
            />
            <FieldHint>{t("setup:modules.adManagement.disabledUsersOuHelper")}</FieldHint>
          </div>
          <SetupOuPicker
            id="defaultUserOu"
            label={t("setup:modules.adManagement.defaultUserOu")}
            value={adManagement.defaultUserOu}
            onChange={(value) => setAdManagement(updateAdManagement(adManagement, { defaultUserOu: value }))}
            setupKey={setupKey}
            ldap={ldap}
            disabled={pickerDisabled}
          />
          <SetupOuPicker
            id="defaultGroupOu"
            label={t("setup:modules.adManagement.defaultGroupOu")}
            value={adManagement.defaultGroupOu}
            onChange={(value) => setAdManagement(updateAdManagement(adManagement, { defaultGroupOu: value }))}
            setupKey={setupKey}
            ldap={ldap}
            disabled={pickerDisabled}
          />
          <SetupOuPicker
            id="defaultComputerOu"
            label={t("setup:modules.adManagement.defaultComputerOu")}
            value={adManagement.defaultComputerOu}
            onChange={(value) => setAdManagement(updateAdManagement(adManagement, { defaultComputerOu: value }))}
            setupKey={setupKey}
            ldap={ldap}
            disabled={pickerDisabled}
          />
          <div className="flex items-center justify-between rounded-lg border px-3 py-3">
            <div>
              <p className="text-sm font-medium">{t("setup:modules.adManagement.deletedObjectsEnabled")}</p>
            </div>
            <Switch
              checked={adManagement.deletedObjectsEnabled}
              onCheckedChange={(checked) =>
                setAdManagement(updateAdManagement(adManagement, { deletedObjectsEnabled: checked }))
              }
              disabled={disabled}
            />
          </div>
        </div>
      ) : null}
    </SectionCard>
  );
}

type AdminUsersStepProps = {
  setupKey: string;
  ldap: SetupLdapFormValues;
  adminUsers: SetupAdminUserSelection[];
  onChange: (adminUsers: SetupAdminUserSelection[]) => void;
  ldapValidated: boolean;
  disabled?: boolean;
};

export function AdminUsersStep({
  setupKey,
  ldap,
  adminUsers,
  onChange,
  ldapValidated,
  disabled,
}: AdminUsersStepProps) {
  const { t } = useTranslation(["setup", "common"]);
  const [search, setSearch] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [results, setResults] = useState<SetupAdminUserSelection[]>([]);

  const runSearch = useCallback(
    async (query: string) => {
      setIsSearching(true);
      setErrorMessage(null);
      try {
        const response = await searchSetupAdminUsers({
          setupKey,
          ldap: buildCompleteSetupLdapPayload(ldap),
          search: query,
        });

        setResults(
          response.users.map((user) => ({
            userName: user.userName,
            displayName: user.displayName,
            email: user.email,
            distinguishedName: user.distinguishedName,
            directoryObjectId: user.directoryObjectId,
          })),
        );
      } catch (error) {
        const fallback = t("setup:steps.adminUsers.searchFailed");
        setErrorMessage(axios.isAxiosError(error) ? getApiErrorMessage(error, fallback) : fallback);
        setResults([]);
      } finally {
        setIsSearching(false);
      }
    },
    [ldap, setupKey, t],
  );

  useEffect(() => {
    if (!shouldFetchAdminUserSearchResults(ldapValidated, search)) {
      return;
    }

    const query = search.trim();
    const timeoutId = window.setTimeout(() => {
      void runSearch(query);
    }, 300);

    return () => window.clearTimeout(timeoutId);
  }, [ldapValidated, runSearch, search]);

  const handleSearchChange = (nextSearch: string) => {
    setSearch(nextSearch);
    if (!shouldFetchAdminUserSearchResults(ldapValidated, nextSearch)) {
      setResults([]);
      setErrorMessage(null);
    }
  };

  const visibleResults = shouldFetchAdminUserSearchResults(ldapValidated, search) ? results : [];

  const handleSelect = (candidate: SetupAdminUserSelection) => {
    if (!canAddAdminUser(adminUsers, candidate)) {
      return;
    }

    onChange([...adminUsers, candidate]);
  };

  const handleRemove = (userName: string) => {
    onChange(adminUsers.filter((user) => user.userName !== userName));
  };

  const searchDisabled = disabled || !ldapValidated;

  return (
    <SectionCard title={t("setup:steps.adminUsers.title")} description={t("setup:steps.adminUsers.description")}>
      {!ldapValidated ? <FieldHint>{t("setup:steps.adminUsers.ldapRequired")}</FieldHint> : null}

      <div className="space-y-2">
        <Label htmlFor="adminUserSearch">{t("setup:actions.search")}</Label>
        <Input
          id="adminUserSearch"
          value={search}
          onChange={(event) => handleSearchChange(event.target.value)}
          disabled={searchDisabled}
          placeholder={t("setup:steps.adminUsers.searchPlaceholder")}
        />
        <FieldHint>{t("setup:steps.adminUsers.minSearchLength")}</FieldHint>
      </div>

      {isSearching ? <LoadingState text={t("setup:steps.adminUsers.searching")} /> : null}
      {errorMessage ? <FieldError message={errorMessage} /> : null}

      {!isSearching && visibleResults.length > 0 ? (
        <ul className="space-y-2">
          {visibleResults.map((user) => {
            const isSelected = !canAddAdminUser(adminUsers, user);
            return (
              <li key={`${user.userName}-${user.directoryObjectId ?? "no-id"}`} className="rounded-lg border px-3 py-2">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-sm font-medium">{user.displayName}</p>
                    <p className="text-sm text-muted-foreground">{user.userName}</p>
                    {user.email ? <p className="text-xs text-muted-foreground">{user.email}</p> : null}
                    {user.distinguishedName ? (
                      <p className="truncate font-mono text-xs text-muted-foreground">{user.distinguishedName}</p>
                    ) : null}
                  </div>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    disabled={searchDisabled || isSelected}
                    onClick={() => handleSelect(user)}
                  >
                    {t("setup:actions.select")}
                  </Button>
                </div>
              </li>
            );
          })}
        </ul>
      ) : null}

      <div className="mt-4 space-y-2">
        <p className="text-sm font-medium">{t("setup:steps.adminUsers.selectedTitle")}</p>
        {adminUsers.length === 0 ? (
          <EmptyState title={t("setup:steps.adminUsers.emptySelectedTitle")} description={t("setup:steps.adminUsers.emptySelectedDescription")} />
        ) : (
          <ul className="space-y-2">
            {adminUsers.map((user) => (
              <li key={user.userName} className="flex items-center justify-between rounded-lg border px-3 py-2">
                <div>
                  <p className="text-sm font-medium">{user.displayName}</p>
                  <p className="text-sm text-muted-foreground">{user.userName}</p>
                </div>
                <Button type="button" size="sm" variant="outline" onClick={() => handleRemove(user.userName)} disabled={disabled}>
                  {t("setup:actions.remove")}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </SectionCard>
  );
}

type SummaryStepProps = {
  values: SetupWizardFormValues;
  preflight: SetupPreflightResponse | null;
};

export function SummaryStep({ values, preflight }: SummaryStepProps) {
  const { t } = useTranslation(["setup", "common"]);
  const adManagement = values.modules.adManagement;

  return (
    <SectionCard title={t("setup:steps.summary.title")} description={t("setup:steps.summary.description")}>
      <div className="space-y-4 text-sm">
        <div>
          <p className="font-medium">{t("setup:steps.summary.ldapTitle")}</p>
          <ul className="mt-2 space-y-1 text-muted-foreground">
            <li>{t("setup:fields.host")}: {values.ldap.host}</li>
            <li>{t("setup:fields.baseDn")}: {values.ldap.baseDn}</li>
            <li>{t("setup:fields.bindUserName")}: {values.ldap.bindUserName}</li>
            <li>{t("setup:fields.bindUserDomain")}: {values.ldap.bindUserDomain || t("common:notAvailable")}</li>
          </ul>
        </div>

        <div>
          <p className="font-medium">{t("setup:steps.summary.adManagementTitle")}</p>
          <p className="text-muted-foreground">
            {adManagement.isEnabled ? t("common:status.active") : t("common:status.passive")}
          </p>
          {adManagement.isEnabled ? (
            <ul className="mt-2 space-y-1 text-muted-foreground">
              <li>{t("setup:modules.adManagement.usersSearchBase")}: {adManagement.usersSearchBase?.label ?? "-"}</li>
              <li>{t("setup:modules.adManagement.groupsSearchBase")}: {adManagement.groupsSearchBase?.label ?? "-"}</li>
              <li>{t("setup:modules.adManagement.computersSearchBase")}: {adManagement.computersSearchBase?.label ?? "-"}</li>
              <li>
                {t("setup:modules.adManagement.disabledUsersOu")}:{" "}
                {adManagement.disabledUsersOu ? (
                  <span title={adManagement.disabledUsersOu.distinguishedName}>
                    {adManagement.disabledUsersOu.label}
                    <span className="font-mono text-xs"> ({adManagement.disabledUsersOu.distinguishedName})</span>
                  </span>
                ) : (
                  t("setup:steps.summary.notSelected")
                )}
              </li>
              {adManagement.defaultUserOu ? <li>{t("setup:modules.adManagement.defaultUserOu")}: {adManagement.defaultUserOu.label}</li> : null}
              {adManagement.defaultGroupOu ? <li>{t("setup:modules.adManagement.defaultGroupOu")}: {adManagement.defaultGroupOu.label}</li> : null}
              {adManagement.defaultComputerOu ? <li>{t("setup:modules.adManagement.defaultComputerOu")}: {adManagement.defaultComputerOu.label}</li> : null}
            </ul>
          ) : null}
        </div>

        <div>
          <p className="font-medium">{t("setup:steps.summary.adminUsersTitle")}</p>
          <ul className="mt-2 space-y-1 text-muted-foreground">
            {values.adminUsers.map((user) => (
              <li key={user.userName}>{user.displayName} ({user.userName})</li>
            ))}
          </ul>
        </div>

        <div>
          <p className="font-medium">{t("setup:steps.summary.preflightTitle")}</p>
          <p className="text-muted-foreground">
            {preflight?.canContinue ? t("setup:steps.summary.preflightReady") : t("setup:steps.summary.preflightBlocked")}
          </p>
        </div>
      </div>
    </SectionCard>
  );
}
