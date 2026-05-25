import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import type { AdManagementNotificationRule } from "@/features/ad-management/types";

type TemplateReadiness = "ready" | "missing" | "passive";

type CreateAdNotificationRuleColumnsOptions = {
  t: TFunction;
  eventLabel: Record<string, string>;
  channelLabel: (channel: string) => string;
  formatRecipient: (rule: AdManagementNotificationRule) => string;
  templateStatusLabel: (status: TemplateReadiness) => string;
  resolveTemplateStatus: (eventKey: string, channel: string) => TemplateReadiness;
  readOnly: boolean;
  isSaving: boolean;
  onToggleEnabled: (rule: AdManagementNotificationRule, enabled: boolean) => void;
  onEdit: (rule: AdManagementNotificationRule) => void;
  onRemove: (rule: AdManagementNotificationRule) => void;
};

export function createAdNotificationRuleColumns({
  t,
  eventLabel,
  channelLabel,
  formatRecipient,
  templateStatusLabel,
  resolveTemplateStatus,
  readOnly,
  isSaving,
  onToggleEnabled,
  onEdit,
  onRemove,
}: CreateAdNotificationRuleColumnsOptions): ColumnDef<AdManagementNotificationRule, unknown>[] {
  return [
    {
      id: "event",
      header: () => t("settings:adManagement.notifications.fields.event"),
      cell: ({ row }) => eventLabel[row.original.eventKey] ?? row.original.eventKey,
    },
    {
      id: "channel",
      header: () => t("settings:adManagement.notifications.fields.channel"),
      cell: ({ row }) => channelLabel(row.original.channel),
    },
    {
      id: "recipient",
      header: () => t("settings:adManagement.notifications.fields.recipientSource"),
      cell: ({ row }) => formatRecipient(row.original),
    },
    {
      id: "templateStatus",
      header: () => t("settings:adManagement.notifications.fields.templateStatus"),
      cell: ({ row }) =>
        templateStatusLabel(
          resolveTemplateStatus(row.original.eventKey, row.original.channel),
        ),
    },
    {
      id: "status",
      header: () => t("settings:adManagement.notifications.fields.status"),
      cell: ({ row }) => (
        <Switch
          checked={row.original.isEnabled}
          disabled={readOnly || isSaving}
          onCheckedChange={(checked) => onToggleEnabled(row.original, checked)}
          aria-label={t("settings:adManagement.notifications.fields.status")}
        />
      ),
    },
    {
      id: "actions",
      header: () => t("settings:adManagement.notifications.fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={readOnly || isSaving}
            onClick={() => onEdit(row.original)}
          >
            {t("settings:adManagement.notifications.actions.edit")}
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={readOnly || isSaving}
            onClick={() => onRemove(row.original)}
          >
            {t("settings:adManagement.notifications.actions.remove")}
          </Button>
        </div>
      ),
    },
  ];
}
