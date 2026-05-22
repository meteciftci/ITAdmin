import { useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { KeyValuePairsEditor } from "@/features/notification-providers/components/KeyValuePairsEditor";
import {
  NOTIFICATION_SMS_SETTINGS_QUERY_KEY,
  getSmsProviderSettings,
  testSmsProvider,
  updateSmsProviderSettings,
} from "@/features/notification-providers/api";
import type {
  SmsProviderSettings,
  TestSmsProviderRequest,
  UpdateSmsProviderSettingsRequest,
} from "@/features/notification-providers/types";
import { useAuthStore } from "@/features/auth/auth-store";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";

type Props = {
  readOnly: boolean;
};

type SmsFormState = {
  isEnabled: boolean;
  displayName: string;
  sender: string;
  timeoutSeconds: string;
  endpointUrl: string;
  method: string;
  contentType: string;
  authType: string;
  apiKeyName: string;
  basicUserName: string;
  basicPassword: string;
  bearerToken: string;
  apiKeyValue: string;
  headers: { key: string; value: string }[];
  queryParameters: { key: string; value: string }[];
  bodyTemplate: string;
  successStatusCodes: string;
  successBodyContains: string;
  turkishCharacterMode: string;
  testPhoneNumber: string;
  testMessage: string;
};

function mapSettingsToForm(settings: SmsProviderSettings): SmsFormState {
  return {
    isEnabled: settings.isEnabled,
    displayName: settings.displayName ?? "",
    sender: settings.sender ?? "",
    timeoutSeconds: String(settings.timeoutSeconds || 30),
    endpointUrl: settings.endpointUrl ?? "",
    method: settings.method || "POST",
    contentType: settings.contentType || "application/json",
    authType: settings.authType || "None",
    apiKeyName: settings.apiKeyName ?? "",
    basicUserName: "",
    basicPassword: "",
    bearerToken: "",
    apiKeyValue: "",
    headers: settings.headers ?? [],
    queryParameters: settings.queryParameters ?? [],
    bodyTemplate: settings.bodyTemplate ?? "",
    successStatusCodes: (settings.successStatusCodes ?? [200]).join(","),
    successBodyContains: settings.successBodyContains ?? "",
    turkishCharacterMode: settings.turkishCharacterMode || "Preserve",
    testPhoneNumber: "",
    testMessage: "",
  };
}

function parseStatusCodes(value: string): number[] {
  const parsed = value
    .split(",")
    .map((item) => Number.parseInt(item.trim(), 10))
    .filter((code) => !Number.isNaN(code));
  return parsed.length > 0 ? parsed : [200];
}

function buildSmsSettingsKey(settings: SmsProviderSettings): string {
  return [
    settings.isEnabled,
    settings.endpointUrl,
    settings.method,
    settings.authType,
    settings.hasBasicPassword,
    settings.hasBearerToken,
    settings.hasApiKey,
    settings.lastValidatedAt,
  ].join("::");
}

function buildUpdatePayload(form: SmsFormState): UpdateSmsProviderSettingsRequest {
  return {
    isEnabled: form.isEnabled,
    displayName: form.displayName || null,
    sender: form.sender || null,
    timeoutSeconds: Number.parseInt(form.timeoutSeconds, 10) || 30,
    endpointUrl: form.endpointUrl.trim(),
    method: form.method,
    contentType: form.contentType,
    authType: form.authType,
    apiKeyName: form.apiKeyName || null,
    basicUserName: form.basicUserName || null,
    basicPassword: form.basicPassword || null,
    bearerToken: form.bearerToken || null,
    apiKeyValue: form.apiKeyValue || null,
    headers: form.headers,
    queryParameters: form.queryParameters,
    bodyTemplate: form.bodyTemplate || null,
    successStatusCodes: parseStatusCodes(form.successStatusCodes),
    successBodyContains: form.successBodyContains || null,
    turkishCharacterMode: form.turkishCharacterMode,
  };
}

export function SmsProviderSettingsTab({ readOnly }: Props) {
  const { t } = useTranslation(["notificationProviders", "common"]);

  const settingsQuery = useQuery({
    queryKey: NOTIFICATION_SMS_SETTINGS_QUERY_KEY,
    queryFn: getSmsProviderSettings,
    refetchOnWindowFocus: false,
  });

  if (settingsQuery.isLoading) {
    return <LoadingState />;
  }

  if (settingsQuery.isError || !settingsQuery.data) {
    return (
      <ErrorState
        title={t("notificationProviders:sms.messages.loadFailed")}
        retry={
          <Button type="button" variant="outline" size="sm" onClick={() => void settingsQuery.refetch()}>
            {t("common:retry")}
          </Button>
        }
      />
    );
  }

  return (
    <SmsProviderSettingsForm
      key={buildSmsSettingsKey(settingsQuery.data)}
      initialSettings={settingsQuery.data}
      readOnly={readOnly}
    />
  );
}

type SmsFormProps = {
  initialSettings: SmsProviderSettings;
  readOnly: boolean;
};

function SmsProviderSettingsForm({ initialSettings, readOnly }: SmsFormProps) {
  const { t } = useTranslation(["notificationProviders", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, "NotificationProviders.Update");
  const canTest = canAccess(user, "NotificationProviders.Test");
  const isReadOnly = readOnly || !canUpdate;

  const [form, setForm] = useState<SmsFormState>(() => mapSettingsToForm(initialSettings));
  const [hasSavedSettings, setHasSavedSettings] = useState(() => Boolean(initialSettings.endpointUrl));

  const secretPlaceholders = useMemo(
    () => ({
      basicPassword: initialSettings.hasBasicPassword
        ? t("notificationProviders:secrets.storedPlaceholder")
        : undefined,
      bearerToken: initialSettings.hasBearerToken
        ? t("notificationProviders:secrets.storedPlaceholder")
        : undefined,
      apiKeyValue: initialSettings.hasApiKey
        ? t("notificationProviders:secrets.storedPlaceholder")
        : undefined,
    }),
    [initialSettings, t],
  );

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateSmsProviderSettingsRequest) => updateSmsProviderSettings(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_SMS_SETTINGS_QUERY_KEY });
      setHasSavedSettings(true);
      setForm((current) => ({
        ...current,
        basicPassword: "",
        bearerToken: "",
        apiKeyValue: "",
      }));
      toast.success(t("notificationProviders:sms.messages.saveSuccess"));
    },
    onError: (error: unknown) => {
      toast.error(
        getApiErrorMessage(error, t("notificationProviders:sms.messages.saveFailed")),
      );
    },
  });

  const testMutation = useMutation({
    mutationFn: (payload: TestSmsProviderRequest) => testSmsProvider(payload),
    onSuccess: (result) => {
      toast.success(result.message);
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("notificationProviders:sms.messages.testFailed")));
    },
  });

  const updateField = <K extends keyof SmsFormState>(field: K, value: SmsFormState[K]) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h3 className="text-base font-semibold">{t("notificationProviders:sms.sectionTitle")}</h3>
          <p className="text-sm text-muted-foreground">{t("notificationProviders:sms.sectionDescription")}</p>
        </div>
        <div className="flex items-center gap-2">
          <Label htmlFor="sms-enabled">{t("notificationProviders:fields.active")}</Label>
          <Switch
            id="sms-enabled"
            checked={form.isEnabled}
            onCheckedChange={(checked) => updateField("isEnabled", checked)}
            disabled={isReadOnly}
          />
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label={t("notificationProviders:fields.provider")}>
          <Input value={t("notificationProviders:sms.providerName")} readOnly disabled />
        </Field>
        <Field label={t("notificationProviders:fields.displayName")}>
          <Input
            value={form.displayName}
            onChange={(event) => updateField("displayName", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:sms.fields.sender")}>
          <Input
            value={form.sender}
            onChange={(event) => updateField("sender", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:fields.timeoutSeconds")}>
          <Input
            type="number"
            min={5}
            max={300}
            value={form.timeoutSeconds}
            onChange={(event) => updateField("timeoutSeconds", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
      </div>

      <div className="space-y-3">
        <h4 className="text-sm font-semibold">{t("notificationProviders:sms.httpSection")}</h4>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label={t("notificationProviders:sms.fields.endpointUrl")} className="md:col-span-2">
            <Input
              value={form.endpointUrl}
              onChange={(event) => updateField("endpointUrl", event.target.value)}
              readOnly={isReadOnly}
            />
          </Field>
          <Field label={t("notificationProviders:sms.fields.method")}>
            <Select
              value={form.method}
              onChange={(event) => updateField("method", event.target.value)}
              disabled={isReadOnly}
            >
              <option value="GET">GET</option>
              <option value="POST">POST</option>
            </Select>
          </Field>
          <Field label={t("notificationProviders:sms.fields.contentType")}>
            <Select
              value={form.contentType}
              onChange={(event) => updateField("contentType", event.target.value)}
              disabled={isReadOnly}
            >
              <option value="application/json">application/json</option>
              <option value="application/x-www-form-urlencoded">
                application/x-www-form-urlencoded
              </option>
              <option value="text/xml">text/xml</option>
              <option value="text/plain">text/plain</option>
            </Select>
          </Field>
        </div>
      </div>

      <div className="space-y-3">
        <h4 className="text-sm font-semibold">{t("notificationProviders:sms.authSection")}</h4>
        <Field label={t("notificationProviders:sms.fields.authType")}>
          <Select
            value={form.authType}
            onChange={(event) => updateField("authType", event.target.value)}
            disabled={isReadOnly}
          >
            <option value="None">{t("notificationProviders:sms.authTypes.none")}</option>
            <option value="Basic">{t("notificationProviders:sms.authTypes.basic")}</option>
            <option value="BearerToken">{t("notificationProviders:sms.authTypes.bearer")}</option>
            <option value="ApiKeyHeader">{t("notificationProviders:sms.authTypes.apiKeyHeader")}</option>
            <option value="ApiKeyQuery">{t("notificationProviders:sms.authTypes.apiKeyQuery")}</option>
          </Select>
        </Field>

        {form.authType === "Basic" ? (
          <div className="grid gap-4 md:grid-cols-2">
            <Field label={t("notificationProviders:sms.fields.basicUserName")}>
              <Input
                value={form.basicUserName}
                onChange={(event) => updateField("basicUserName", event.target.value)}
                readOnly={isReadOnly}
              />
            </Field>
            <Field label={t("notificationProviders:sms.fields.basicPassword")}>
              <Input
                type="password"
                value={form.basicPassword}
                onChange={(event) => updateField("basicPassword", event.target.value)}
                placeholder={secretPlaceholders.basicPassword}
                readOnly={isReadOnly}
              />
            </Field>
          </div>
        ) : null}

        {form.authType === "BearerToken" ? (
          <Field label={t("notificationProviders:sms.fields.bearerToken")}>
            <Input
              type="password"
              value={form.bearerToken}
              onChange={(event) => updateField("bearerToken", event.target.value)}
              placeholder={secretPlaceholders.bearerToken}
              readOnly={isReadOnly}
            />
          </Field>
        ) : null}

        {form.authType === "ApiKeyHeader" || form.authType === "ApiKeyQuery" ? (
          <div className="grid gap-4 md:grid-cols-2">
            <Field label={t("notificationProviders:sms.fields.apiKeyName")}>
              <Input
                value={form.apiKeyName}
                onChange={(event) => updateField("apiKeyName", event.target.value)}
                readOnly={isReadOnly}
              />
            </Field>
            <Field label={t("notificationProviders:sms.fields.apiKeyValue")}>
              <Input
                type="password"
                value={form.apiKeyValue}
                onChange={(event) => updateField("apiKeyValue", event.target.value)}
                placeholder={secretPlaceholders.apiKeyValue}
                readOnly={isReadOnly}
              />
            </Field>
          </div>
        ) : null}
      </div>

      <div className="space-y-4">
        <h4 className="text-sm font-semibold">{t("notificationProviders:sms.requestSection")}</h4>
        <KeyValuePairsEditor
          label={t("notificationProviders:sms.fields.headers")}
          pairs={form.headers}
          onChange={(pairs) => updateField("headers", pairs)}
          disabled={isReadOnly}
        />
        <KeyValuePairsEditor
          label={t("notificationProviders:sms.fields.queryParameters")}
          pairs={form.queryParameters}
          onChange={(pairs) => updateField("queryParameters", pairs)}
          disabled={isReadOnly}
        />
        <Field label={t("notificationProviders:sms.fields.bodyTemplate")}>
          <Textarea
            value={form.bodyTemplate}
            onChange={(event) => updateField("bodyTemplate", event.target.value)}
            rows={6}
            readOnly={isReadOnly}
          />
        </Field>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label={t("notificationProviders:sms.fields.successStatusCodes")}>
          <Input
            value={form.successStatusCodes}
            onChange={(event) => updateField("successStatusCodes", event.target.value)}
            placeholder="200,201"
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:sms.fields.successBodyContains")}>
          <Input
            value={form.successBodyContains}
            onChange={(event) => updateField("successBodyContains", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:sms.fields.turkishCharacterMode")}>
          <Select
            value={form.turkishCharacterMode}
            onChange={(event) => updateField("turkishCharacterMode", event.target.value)}
            disabled={isReadOnly}
          >
            <option value="Preserve">{t("notificationProviders:sms.turkishModes.preserve")}</option>
            <option value="TransliterateToAscii">
              {t("notificationProviders:sms.turkishModes.transliterate")}
            </option>
          </Select>
        </Field>
      </div>

      {canUpdate ? (
        <Button
          type="button"
          onClick={() => updateMutation.mutate(buildUpdatePayload(form))}
          disabled={isReadOnly || updateMutation.isPending}
        >
          {t("common:actions.save")}
        </Button>
      ) : null}

      <div className="space-y-3 rounded-md border p-4">
        <h4 className="text-sm font-semibold">{t("notificationProviders:sms.testSection")}</h4>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label={t("notificationProviders:sms.fields.testPhoneNumber")}>
            <Input
              value={form.testPhoneNumber}
              onChange={(event) => updateField("testPhoneNumber", event.target.value)}
            />
          </Field>
          <Field label={t("notificationProviders:sms.fields.testMessage")} className="md:col-span-2">
            <Textarea
              value={form.testMessage}
              onChange={(event) => updateField("testMessage", event.target.value)}
              rows={3}
            />
          </Field>
        </div>
        {canTest ? (
          <Button
            type="button"
            variant="secondary"
            disabled={!hasSavedSettings || testMutation.isPending}
            onClick={() => {
              if (!hasSavedSettings) {
                toast.error(t("notificationProviders:sms.messages.saveBeforeTest"));
                return;
              }

              testMutation.mutate({
                phoneNumber: form.testPhoneNumber,
                message: form.testMessage,
              });
            }}
          >
            {t("notificationProviders:sms.actions.test")}
          </Button>
        ) : null}
      </div>
    </div>
  );
}

function Field({
  label,
  children,
  className,
}: {
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={`space-y-2 ${className ?? ""}`}>
      <Label>{label}</Label>
      {children}
    </div>
  );
}
