import { useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
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
import { useAuthStore } from "@/features/auth/auth-store";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";

type Props = {
  readOnly: boolean;
};

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
  testRecipientEmail: string;
  testSubject: string;
  testBody: string;
};

function mapSettingsToForm(settings: EmailProviderSettings): EmailFormState {
  return {
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
    testRecipientEmail: "",
    testSubject: "",
    testBody: "",
  };
}

function buildEmailSettingsKey(settings: EmailProviderSettings): string {
  return [
    settings.isEnabled,
    settings.host,
    settings.port,
    settings.useSsl,
    settings.hasPassword,
    settings.lastValidatedAt,
  ].join("::");
}

function buildUpdatePayload(form: EmailFormState): UpdateEmailProviderSettingsRequest {
  return {
    isEnabled: form.isEnabled,
    displayName: form.displayName || null,
    host: form.host.trim(),
    port: Number.parseInt(form.port, 10) || 587,
    useSsl: form.useSsl,
    userName: form.userName || null,
    password: form.password || null,
    fromAddress: form.fromAddress.trim(),
    fromDisplayName: form.fromDisplayName || null,
    timeoutSeconds: Number.parseInt(form.timeoutSeconds, 10) || 30,
  };
}

export function EmailProviderSettingsTab({ readOnly }: Props) {
  const { t } = useTranslation(["notificationProviders", "common"]);

  const settingsQuery = useQuery({
    queryKey: NOTIFICATION_EMAIL_SETTINGS_QUERY_KEY,
    queryFn: getEmailProviderSettings,
    refetchOnWindowFocus: false,
  });

  if (settingsQuery.isLoading) {
    return <LoadingState />;
  }

  if (settingsQuery.isError || !settingsQuery.data) {
    return (
      <ErrorState
        title={t("notificationProviders:email.messages.loadFailed")}
        retry={
          <Button type="button" variant="outline" size="sm" onClick={() => void settingsQuery.refetch()}>
            {t("common:retry")}
          </Button>
        }
      />
    );
  }

  return (
    <EmailProviderSettingsForm
      key={buildEmailSettingsKey(settingsQuery.data)}
      initialSettings={settingsQuery.data}
      readOnly={readOnly}
    />
  );
}

type EmailFormProps = {
  initialSettings: EmailProviderSettings;
  readOnly: boolean;
};

function EmailProviderSettingsForm({ initialSettings, readOnly }: EmailFormProps) {
  const { t } = useTranslation(["notificationProviders", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canUpdate = canAccess(user, "NotificationProviders.Update");
  const canTest = canAccess(user, "NotificationProviders.Test");
  const isReadOnly = readOnly || !canUpdate;

  const [form, setForm] = useState<EmailFormState>(() => mapSettingsToForm(initialSettings));
  const [hasSavedSettings, setHasSavedSettings] = useState(() => Boolean(initialSettings.host));

  const passwordPlaceholder = useMemo(
    () =>
      initialSettings.hasPassword
        ? t("notificationProviders:secrets.storedPlaceholder")
        : undefined,
    [initialSettings.hasPassword, t],
  );

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateEmailProviderSettingsRequest) =>
      updateEmailProviderSettings(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_EMAIL_SETTINGS_QUERY_KEY });
      setHasSavedSettings(true);
      setForm((current) => ({ ...current, password: "" }));
      toast.success(t("notificationProviders:email.messages.saveSuccess"));
    },
    onError: (error: unknown) => {
      toast.error(
        getApiErrorMessage(error, t("notificationProviders:email.messages.saveFailed")),
      );
    },
  });

  const testMutation = useMutation({
    mutationFn: (payload: TestEmailProviderRequest) => testEmailProvider(payload),
    onSuccess: (result) => {
      toast.success(result.message);
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("notificationProviders:email.messages.testFailed")));
    },
  });

  const updateField = <K extends keyof EmailFormState>(field: K, value: EmailFormState[K]) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h3 className="text-base font-semibold">{t("notificationProviders:email.sectionTitle")}</h3>
          <p className="text-sm text-muted-foreground">
            {t("notificationProviders:email.sectionDescription")}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Label htmlFor="email-enabled">{t("notificationProviders:fields.active")}</Label>
          <Switch
            id="email-enabled"
            checked={form.isEnabled}
            onCheckedChange={(checked) => updateField("isEnabled", checked)}
            disabled={isReadOnly}
          />
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Field label={t("notificationProviders:fields.provider")}>
          <Input value={t("notificationProviders:email.providerName")} readOnly disabled />
        </Field>
        <Field label={t("notificationProviders:fields.displayName")}>
          <Input
            value={form.displayName}
            onChange={(event) => updateField("displayName", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:email.fields.host")}>
          <Input
            value={form.host}
            onChange={(event) => updateField("host", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:email.fields.port")}>
          <Input
            type="number"
            min={1}
            max={65535}
            value={form.port}
            onChange={(event) => updateField("port", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:email.fields.userName")}>
          <Input
            value={form.userName}
            onChange={(event) => updateField("userName", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:email.fields.password")}>
          <Input
            type="password"
            value={form.password}
            onChange={(event) => updateField("password", event.target.value)}
            placeholder={passwordPlaceholder}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:email.fields.fromAddress")}>
          <Input
            value={form.fromAddress}
            onChange={(event) => updateField("fromAddress", event.target.value)}
            readOnly={isReadOnly}
          />
        </Field>
        <Field label={t("notificationProviders:email.fields.fromDisplayName")}>
          <Input
            value={form.fromDisplayName}
            onChange={(event) => updateField("fromDisplayName", event.target.value)}
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
        <div className="flex items-center gap-2 pt-6">
          <Switch
            id="email-use-ssl"
            checked={form.useSsl}
            onCheckedChange={(checked) => updateField("useSsl", checked)}
            disabled={isReadOnly}
          />
          <Label htmlFor="email-use-ssl">{t("notificationProviders:email.fields.useSsl")}</Label>
        </div>
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
        <h4 className="text-sm font-semibold">{t("notificationProviders:email.testSection")}</h4>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label={t("notificationProviders:email.fields.testRecipientEmail")}>
            <Input
              value={form.testRecipientEmail}
              onChange={(event) => updateField("testRecipientEmail", event.target.value)}
            />
          </Field>
          <Field label={t("notificationProviders:email.fields.testSubject")}>
            <Input
              value={form.testSubject}
              onChange={(event) => updateField("testSubject", event.target.value)}
            />
          </Field>
          <Field label={t("notificationProviders:email.fields.testBody")} className="md:col-span-2">
            <Input
              value={form.testBody}
              onChange={(event) => updateField("testBody", event.target.value)}
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
                toast.error(t("notificationProviders:email.messages.saveBeforeTest"));
                return;
              }

              testMutation.mutate({
                recipientEmail: form.testRecipientEmail,
                subject: form.testSubject,
                body: form.testBody,
              });
            }}
          >
            {t("notificationProviders:email.actions.test")}
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
