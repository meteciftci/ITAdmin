import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";
import {
  Eye,
  RotateCcw,
  XCircle,
} from "lucide-react";

import { DateTimeText } from "@/components/common/DateTimeText";
import { RowActions } from "@/components/common/RowActions";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import {
  NotificationChannelBadge,
  NotificationDeliveryStatusBadge,
} from "@/features/notification-outbox/NotificationDeliveryBadges";
import type { NotificationOutboxListItem } from "@/features/notification-outbox/types";

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
      id: "delivery",
      header: () => t("notificationOutbox:columns.delivery"),
      cell: ({ row }) => (
        <div className="space-y-1.5">
          <NotificationChannelBadge channel={row.original.channel} t={t} />
          <p className="whitespace-nowrap text-xs text-muted-foreground">
            {row.original.recipientMasked}
          </p>
        </div>
      ),
    },
    {
      id: "message",
      header: () => t("notificationOutbox:columns.message"),
      meta: { cellClassName: "min-w-52" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <div className="max-w-80 space-y-1">
          <p className="line-clamp-2 font-medium">
            {row.original.subject || t("notificationOutbox:columns.noSubject")}
          </p>
          <p className="truncate text-xs text-muted-foreground">{row.original.providerKey}</p>
        </div>
      ),
    },
    {
      id: "deliveryState",
      header: () => t("notificationOutbox:columns.deliveryState"),
      meta: { cellClassName: "min-w-48" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <div className="max-w-72 space-y-1.5">
          <NotificationDeliveryStatusBadge status={row.original.status} t={t} />
          {row.original.status === "Failed" && row.original.lastErrorMessage ? (
            <p className="line-clamp-2 text-xs leading-5 text-muted-foreground">
              {row.original.lastErrorMessage}
            </p>
          ) : null}
        </div>
      ),
    },
    {
      id: "attempts",
      header: () => t("notificationOutbox:columns.attempts"),
      cell: ({ row }) => (
        <div className="space-y-1">
          <p className="whitespace-nowrap font-medium">
            {t("notificationOutbox:columns.attemptProgress", {
              current: row.original.attemptCount,
              max: row.original.maxAttempts,
            })}
          </p>
          {row.original.nextAttemptAt ? (
            <p className="whitespace-nowrap text-xs text-muted-foreground">
              {t("notificationOutbox:columns.nextAttemptShort")}: {" "}
              <DateTimeText value={row.original.nextAttemptAt} />
            </p>
          ) : null}
        </div>
      ),
    },
    {
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const item = row.original;
        return (
          <RowActions
            ariaLabel={t("notificationOutbox:actions.actionsFor", {
              recipient: item.recipientMasked,
            })}
          >
            <DropdownMenuItem onClick={() => onDetail(item)}>
              <Eye className="mr-2 size-4" aria-hidden />
              {t("common:actions.detail")}
            </DropdownMenuItem>
            {canRetry && item.status === "Failed" ? (
              <DropdownMenuItem onClick={() => onRetry(item)}>
                <RotateCcw className="mr-2 size-4" aria-hidden />
                {t("notificationOutbox:actions.retry")}
              </DropdownMenuItem>
            ) : null}
            {canCancel && (item.status === "Pending" || item.status === "Failed") ? (
              <DropdownMenuItem
                onClick={() => onCancel(item)}
                className="text-destructive hover:bg-destructive/10"
              >
                <XCircle className="mr-2 size-4" aria-hidden />
                {t("notificationOutbox:actions.cancel")}
              </DropdownMenuItem>
            ) : null}
          </RowActions>
        );
      },
    },
  ];
}
