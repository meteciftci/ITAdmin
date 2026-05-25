import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";
import { Link } from "react-router-dom";

import { DateTimeText } from "@/components/common/DateTimeText";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { StatusBadge } from "@/components/common/StatusBadge";
import { NotificationTemplateStatusSwitch } from "@/features/notification-settings/NotificationTemplateStatusSwitch";
import {
  getCatalogEventLabel,
  getCatalogModuleLabel,
  getChannelLabel,
} from "@/features/notification-settings/catalog-labels";
import type { NotificationTemplateListItem } from "@/features/notification-templates/types";
import { buttonVariants } from "@/components/ui/button-variants";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type CreateNotificationTemplateColumnsOptions = {
  t: TFunction;
  canUpdate: boolean;
  useCatalogLabels?: boolean;
  onEdit?: (item: NotificationTemplateListItem) => void;
  editHref?: (item: NotificationTemplateListItem) => string;
  editLabel?: string;
};

export function createNotificationTemplateColumns({
  t,
  canUpdate,
  useCatalogLabels = false,
  onEdit,
  editHref,
  editLabel,
}: CreateNotificationTemplateColumnsOptions): ColumnDef<NotificationTemplateListItem, unknown>[] {
  const columns: ColumnDef<NotificationTemplateListItem, unknown>[] = [
    {
      accessorKey: "moduleKey",
      header: () => t("notificationTemplates:columns.module"),
      cell: ({ row }) =>
        useCatalogLabels
          ? getCatalogModuleLabel(t, row.original.moduleKey)
          : row.original.moduleKey,
    },
    {
      accessorKey: "eventKey",
      header: () => t("notificationTemplates:columns.event"),
      cell: ({ row }) =>
        useCatalogLabels
          ? getCatalogEventLabel(t, row.original.moduleKey, row.original.eventKey)
          : row.original.eventKey,
    },
    {
      accessorKey: "channel",
      header: () => t("notificationTemplates:columns.channel"),
      cell: ({ row }) =>
        useCatalogLabels ? getChannelLabel(t, row.original.channel) : row.original.channel,
    },
    {
      accessorKey: "name",
      header: () => t("notificationTemplates:columns.name"),
    },
    {
      id: "status",
      header: () => t("notificationTemplates:columns.status"),
      cell: ({ row }) => (
        <div className="flex items-center gap-2">
          {canUpdate ? (
            <NotificationTemplateStatusSwitch
              key={`${row.original.id}-${row.original.isEnabled}`}
              templateId={row.original.id}
              isEnabled={row.original.isEnabled}
              canUpdate={canUpdate}
            />
          ) : (
            <StatusBadge isActive={row.original.isEnabled} />
          )}
        </div>
      ),
    },
    {
      id: "updatedAt",
      header: () => t("notificationTemplates:columns.updatedAt"),
      cell: ({ row }) =>
        row.original.updatedAt ? <DateTimeText value={row.original.updatedAt} /> : "-",
    },
  ];

  if (canUpdate) {
    columns.push({
      id: "actions",
      header: () => t("notificationTemplates:columns.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const item = row.original;
        if (editHref) {
          return (
            <Link
              to={editHref(item)}
              className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
            >
              {editLabel ?? t("notificationTemplates:actions.edit")}
            </Link>
          );
        }

        if (onEdit) {
          return (
            <Button size="sm" variant="outline" onClick={() => void onEdit(item)}>
              {editLabel ?? t("notificationTemplates:actions.edit")}
            </Button>
          );
        }

        return null;
      },
    });
  }

  return columns;
}
