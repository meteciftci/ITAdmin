import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { NotificationOutboxListItem } from "@/features/notification-outbox/types";

type StatusBadgeVariant = "default" | "secondary" | "destructive" | "outline" | "success";

function getStatusVariant(status: string): StatusBadgeVariant {
  switch (status) {
    case "Sent":
      return "success";
    case "Failed":
      return "destructive";
    case "Processing":
      return "outline";
    case "Cancelled":
      return "secondary";
    default:
      return "default";
  }
}

type CreateNotificationOutboxColumnsOptions = {
  t: TFunction;
  canRetry: boolean;
  canCancel: boolean;
  onDetail: (item: NotificationOutboxListItem) => void;
  onRetry: (item: NotificationOutboxListItem) => void;
  onCancel: (item: NotificationOutboxListItem) => void;
};

export function createNotificationOutboxColumns({
  t,
  canRetry,
  canCancel,
  onDetail,
  onRetry,
  onCancel,
}: CreateNotificationOutboxColumnsOptions): ColumnDef<NotificationOutboxListItem, unknown>[] {
  return [
    {
      id: "createdAt",
      header: () => t("notificationOutbox:columns.createdAt"),
      cell: ({ row }) => <DateTimeText value={row.original.createdAt} />,
    },
    {
      accessorKey: "channel",
      header: () => t("notificationOutbox:columns.channel"),
    },
    {
      accessorKey: "providerKey",
      header: () => t("notificationOutbox:columns.provider"),
    },
    {
      accessorKey: "recipientMasked",
      header: () => t("notificationOutbox:columns.recipient"),
    },
    {
      accessorKey: "subject",
      header: () => t("notificationOutbox:columns.subject"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.subject ?? "-",
    },
    {
      id: "status",
      header: () => t("common:fields.status"),
      cell: ({ row }) => (
        <Badge variant={getStatusVariant(row.original.status)}>
          {t(`notificationOutbox:statuses.${row.original.status.toLowerCase()}`, {
            defaultValue: row.original.status,
          })}
        </Badge>
      ),
    },
    {
      id: "attempts",
      header: () => t("notificationOutbox:columns.attempts"),
      cell: ({ row }) => `${row.original.attemptCount}/${row.original.maxAttempts}`,
    },
    {
      id: "nextAttempt",
      header: () => t("notificationOutbox:columns.nextAttempt"),
      cell: ({ row }) =>
        row.original.nextAttemptAt ? (
          <DateTimeText value={row.original.nextAttemptAt} />
        ) : (
          "-"
        ),
    },
    {
      id: "related",
      header: () => t("notificationOutbox:columns.related"),
      cell: ({ row }) =>
        row.original.relatedModule
          ? `${row.original.relatedModule}/${row.original.relatedEvent ?? "-"}`
          : "-",
    },
    {
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const item = row.original;
        return (
          <div className="flex flex-wrap gap-2">
            <Button size="sm" variant="outline" onClick={() => onDetail(item)}>
              {t("common:actions.detail")}
            </Button>
            {canRetry && item.status === "Failed" ? (
              <Button size="sm" variant="secondary" onClick={() => onRetry(item)}>
                {t("notificationOutbox:actions.retry")}
              </Button>
            ) : null}
            {canCancel && (item.status === "Pending" || item.status === "Failed") ? (
              <Button size="sm" variant="destructive" onClick={() => onCancel(item)}>
                {t("notificationOutbox:actions.cancel")}
              </Button>
            ) : null}
          </div>
        );
      },
    },
  ];
}
