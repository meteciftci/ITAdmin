import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import {
  buildUpdateAdManagementSettingsPayload,
  defaultAdManagementNotificationSettings,
} from "@/features/ad-management/ad-management-settings-payload";
import {
  AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
  getAdAttributeMappings,
} from "@/features/ad-management/api";
import {
  AdManagementNotificationRuleDialog,
  type NotificationRuleDialogMode,
} from "@/features/ad-management/components/AdManagementNotificationRuleDialog";
import type {
  AdManagementNotificationRule,
  AdManagementNotificationSettings,
  AdManagementSettings,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";
import { AD_NOTIFICATION_CHANNELS } from "@/features/ad-management/types";
import {
  NOTIFICATION_TEMPLATES_QUERY_KEY,
  getNotificationTemplates,
} from "@/features/notification-templates/api";
import type { NotificationTemplateListItem } from "@/features/notification-templates/types";

type Props = {
  settings: AdManagementSettings | undefined;
  readOnly: boolean;
  isSaving: boolean;
  onSave: (
    payload: UpdateAdManagementSettingsRequest,
    meta: { successMessage: string },
  ) => void;
};

type TemplateReadiness = "ready" | "missing" | "passive";

type DialogState = {
  open: boolean;
  mode: NotificationRuleDialogMode;
  rule: AdManagementNotificationRule | null;
};

function cloneRules(settings: AdManagementNotificationSettings | undefined): AdManagementNotificationRule[] {
  const base = settings ?? defaultAdManagementNotificationSettings();
  return base.rules.map((rule) => ({
    ...rule,
    recipientSource: rule.recipientSource ? { ...rule.recipientSource } : null,
  }));
}

function resolveTemplateReadiness(
  templates: NotificationTemplateListItem[],
  eventKey: string,
  channel: string,
): TemplateReadiness {
  const match = templates.find(
    (item) => item.eventKey === eventKey && item.channel === channel,
  );
  if (!match) {
    return "missing";
  }

  return match.isEnabled ? "ready" : "passive";
}

function formatRecipientSourceLabel(
  rule: AdManagementNotificationRule,
  mappings: { id: string; displayName: string }[],
  t: (key: string) => string,
): string {
  const source = rule.recipientSource;
  if (!source?.type) {
    return "—";
  }

  if (source.type === "MappedAttribute" && source.value) {
    const mapping = mappings.find((item) => item.id === source.value);
    return mapping?.displayName ?? source.value;
  }

  if (source.type === "AdAttribute" && source.value) {
    return `${t("settings:adManagement.notifications.recipientTypes.adAttribute")}: ${source.value}`;
  }

  if (source.type === "UserPrincipalName") {
    return t("settings:adManagement.notifications.recipientTypes.userPrincipalName");
  }

  if (source.type === "MailAttribute") {
    return t("settings:adManagement.notifications.recipientTypes.mailAttribute");
  }

  return source.type;
}

export function AdManagementNotificationsForm({
  settings,
  readOnly,
  isSaving,
  onSave,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);
  const [rules, setRules] = useState<AdManagementNotificationRule[]>(() =>
    cloneRules(settings?.notificationSettings),
  );
  const [dialog, setDialog] = useState<DialogState>({
    open: false,
    mode: "create",
    rule: null,
  });

  const mappingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_MAPPINGS_QUERY_KEY,
    queryFn: getAdAttributeMappings,
  });

  const templatesQuery = useQuery({
    queryKey: [...NOTIFICATION_TEMPLATES_QUERY_KEY, "AdManagement"],
    queryFn: () => getNotificationTemplates({ moduleKey: "AdManagement" }),
  });

  const templates = templatesQuery.data ?? [];
  const mappings = mappingsQuery.data ?? [];

  const eventLabel = useMemo(
    () =>
      ({
        UserCreated: t("settings:adManagement.notifications.events.userCreated.label"),
        UserEnabled: t("settings:adManagement.notifications.events.userEnabled.label"),
        UserDisabled: t("settings:adManagement.notifications.events.userDisabled.label"),
        UserUnlocked: t("settings:adManagement.notifications.events.userUnlocked.label"),
      }) as Record<string, string>,
    [t],
  );

  function persistRules(nextRules: AdManagementNotificationRule[], successMessage: string) {
    if (!settings || readOnly) {
      return;
    }

    onSave(
      buildUpdateAdManagementSettingsPayload(settings, {
        notificationSettings: { rules: nextRules },
      }),
      { successMessage },
    );
  }

  function handleRuleSubmit(rule: AdManagementNotificationRule) {
    const nextRules =
      dialog.mode === "edit"
        ? rules.map((item) => (item.id === rule.id ? rule : item))
        : [...rules, rule];

    setRules(nextRules);
    persistRules(
      nextRules,
      dialog.mode === "edit"
        ? t("settings:adManagement.notifications.messages.ruleUpdated")
        : t("settings:adManagement.notifications.messages.ruleAdded"),
    );
  }

  function handleToggleEnabled(rule: AdManagementNotificationRule, enabled: boolean) {
    const nextRules = rules.map((item) =>
      item.id === rule.id ? { ...item, isEnabled: enabled } : item,
    );
    setRules(nextRules);
    persistRules(nextRules, t("settings:adManagement.notifications.messages.ruleUpdated"));
  }

  function handleRemove(rule: AdManagementNotificationRule) {
    const nextRules = rules.filter((item) => item.id !== rule.id);
    setRules(nextRules);
    persistRules(nextRules, t("settings:adManagement.notifications.messages.ruleRemoved"));
  }

  function templateStatusLabel(status: TemplateReadiness): string {
    if (status === "ready") {
      return t("settings:adManagement.notifications.templateStatus.ready");
    }

    if (status === "passive") {
      return t("settings:adManagement.notifications.templateStatus.passive");
    }

    return t("settings:adManagement.notifications.templateStatus.missing");
  }

  function channelLabel(channel: string): string {
    return channel === AD_NOTIFICATION_CHANNELS.email
      ? t("settings:adManagement.notifications.channels.email")
      : t("settings:adManagement.notifications.channels.sms");
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-base font-medium">
          {t("settings:adManagement.notifications.rulesTitle")}
        </h3>
        {!readOnly ? (
          <Button
            type="button"
            onClick={() => setDialog({ open: true, mode: "create", rule: null })}
          >
            {t("settings:adManagement.notifications.actions.add")}
          </Button>
        ) : null}
      </div>

      {rules.length === 0 ? (
        <div className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">
          {t("settings:adManagement.notifications.empty")}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full text-sm">
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.notifications.fields.event")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.notifications.fields.channel")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.notifications.fields.recipientSource")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.notifications.fields.templateStatus")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.notifications.fields.status")}
                </th>
                <th className="px-3 py-2 text-right">
                  {t("settings:adManagement.notifications.fields.actions")}
                </th>
              </tr>
            </thead>
            <tbody>
              {rules.map((rule) => {
                const templateStatus = resolveTemplateReadiness(
                  templates,
                  rule.eventKey,
                  rule.channel,
                );

                return (
                  <tr key={rule.id} className="border-t">
                    <td className="px-3 py-2">{eventLabel[rule.eventKey] ?? rule.eventKey}</td>
                    <td className="px-3 py-2">{channelLabel(rule.channel)}</td>
                    <td className="px-3 py-2">
                      {formatRecipientSourceLabel(rule, mappings, t)}
                    </td>
                    <td className="px-3 py-2">{templateStatusLabel(templateStatus)}</td>
                    <td className="px-3 py-2">
                      <Switch
                        checked={rule.isEnabled}
                        disabled={readOnly || isSaving}
                        onCheckedChange={(checked) => handleToggleEnabled(rule, checked)}
                        aria-label={t("settings:adManagement.notifications.fields.status")}
                      />
                    </td>
                    <td className="px-3 py-2 text-right">
                      <div className="flex justify-end gap-2">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={readOnly || isSaving}
                          onClick={() =>
                            setDialog({ open: true, mode: "edit", rule })}
                        >
                          {t("settings:adManagement.notifications.actions.edit")}
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={readOnly || isSaving}
                          onClick={() => handleRemove(rule)}
                        >
                          {t("settings:adManagement.notifications.actions.remove")}
                        </Button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <AdManagementNotificationRuleDialog
        open={dialog.open}
        mode={dialog.mode}
        initialRule={dialog.rule}
        existingRules={rules}
        mappings={mappings}
        readOnly={readOnly}
        onOpenChange={(open) => setDialog((prev) => ({ ...prev, open }))}
        onSubmit={handleRuleSubmit}
      />
    </div>
  );
}
