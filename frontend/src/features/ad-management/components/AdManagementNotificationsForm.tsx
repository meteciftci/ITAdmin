import { useMemo, useState, useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useClientDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { Button } from "@/components/ui/button";
import { createAdNotificationRuleColumns } from "@/features/ad-management/ad-notification-rule-columns";
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

const PAGE_SIZE_OPTIONS = [10, 25, 50];
const DEFAULT_PAGE_SIZE = 10;

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
  const [search, setSearch] = useState("");
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

  const templates = useMemo(() => templatesQuery.data ?? [], [templatesQuery.data]);
  const mappings = useMemo(() => mappingsQuery.data ?? [], [mappingsQuery.data]);

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

  const persistRules = useCallback(
    (nextRules: AdManagementNotificationRule[], successMessage: string) => {
      if (!settings || readOnly) {
        return;
      }

      onSave(
        buildUpdateAdManagementSettingsPayload(settings, {
          notificationSettings: { rules: nextRules },
        }),
        { successMessage },
      );
    },
    [settings, readOnly, onSave],
  );

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

  const handleToggleEnabled = useCallback(
    (rule: AdManagementNotificationRule, enabled: boolean) => {
      const nextRules = rules.map((item) =>
        item.id === rule.id ? { ...item, isEnabled: enabled } : item,
      );
      setRules(nextRules);
      persistRules(nextRules, t("settings:adManagement.notifications.messages.ruleUpdated"));
    },
    [rules, persistRules, t],
  );

  const handleRemove = useCallback(
    (rule: AdManagementNotificationRule) => {
      const nextRules = rules.filter((item) => item.id !== rule.id);
      setRules(nextRules);
      persistRules(nextRules, t("settings:adManagement.notifications.messages.ruleRemoved"));
    },
    [rules, persistRules, t],
  );

  const templateStatusLabel = useCallback(
    (status: TemplateReadiness): string => {
      if (status === "ready") {
        return t("settings:adManagement.notifications.templateStatus.ready");
      }

      if (status === "passive") {
        return t("settings:adManagement.notifications.templateStatus.passive");
      }

      return t("settings:adManagement.notifications.templateStatus.missing");
    },
    [t],
  );

  const channelLabel = useCallback(
    (channel: string): string =>
      channel === AD_NOTIFICATION_CHANNELS.email
        ? t("settings:adManagement.notifications.channels.email")
        : t("settings:adManagement.notifications.channels.sms"),
    [t],
  );

  const resolveTemplateReadinessForRule = useCallback(
    (eventKey: string, channel: string) =>
      resolveTemplateReadiness(templates, eventKey, channel),
    [templates],
  );

  const columns = useMemo(
    () =>
      createAdNotificationRuleColumns({
        t,
        eventLabel,
        channelLabel,
        formatRecipient: (rule) => formatRecipientSourceLabel(rule, mappings, t),
        templateStatusLabel,
        resolveTemplateStatus: resolveTemplateReadinessForRule,
        readOnly,
        isSaving,
        onToggleEnabled: handleToggleEnabled,
        onEdit: (rule) => setDialog({ open: true, mode: "edit", rule }),
        onRemove: handleRemove,
      }),
    [
      t,
      eventLabel,
      channelLabel,
      readOnly,
      isSaving,
      mappings,
      resolveTemplateReadinessForRule,
      templateStatusLabel,
      handleToggleEnabled,
      handleRemove,
    ],
  );

  const getSearchableValue = useMemo(
    () => (rule: AdManagementNotificationRule) =>
      [
        eventLabel[rule.eventKey] ?? rule.eventKey,
        channelLabel(rule.channel),
        formatRecipientSourceLabel(rule, mappings, t),
        templateStatusLabel(resolveTemplateReadinessForRule(rule.eventKey, rule.channel)),
      ]
        .filter(Boolean)
        .join(" "),
    [
      eventLabel,
      channelLabel,
      mappings,
      t,
      templateStatusLabel,
      resolveTemplateReadinessForRule,
    ],
  );

  const table = useClientDataTable({
    data: rules,
    columns,
    globalFilter: search,
    enableGlobalFilter: true,
    getSearchableValue,
    initialPageSize: DEFAULT_PAGE_SIZE,
  });

  const hasRows = rules.length > 0;

  return (
    <div className="space-y-4">
      <h3 className="text-base font-medium">
        {t("settings:adManagement.notifications.rulesTitle")}
      </h3>

      <DataTableToolbar
        searchValue={search}
        onSearchChange={setSearch}
        searchPlaceholder={t("settings:adManagement.notifications.searchPlaceholder")}
        actions={
          !readOnly ? (
            <Button
              type="button"
              onClick={() => setDialog({ open: true, mode: "create", rule: null })}
            >
              {t("settings:adManagement.notifications.actions.add")}
            </Button>
          ) : null
        }
      />

      {rules.length === 0 ? (
        <EmptyState title={t("settings:adManagement.notifications.empty")} />
      ) : (
        <DataTable
          table={table}
          emptyMessage={t("common:dataTable.noResults")}
          footer={
            hasRows ? (
              <DataTablePagination
                mode="client"
                table={table}
                pageSizeOptions={PAGE_SIZE_OPTIONS}
              />
            ) : undefined
          }
        />
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
