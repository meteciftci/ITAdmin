import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { DateTimeText } from "@/components/common/DateTimeText";
import { DataToolbar } from "@/components/common/DataToolbar";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { LogDetailDialog } from "@/components/common/LogDetailDialog";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { TablePagination } from "@/components/common/TablePagination";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import {
  NOTIFICATION_OUTBOX_QUERY_KEY,
  cancelNotificationOutboxItem,
  getNotificationOutboxDetail,
  getNotificationOutboxItems,
  retryNotificationOutboxItem,
} from "@/features/notification-outbox/api";
import type { NotificationOutboxListItem } from "@/features/notification-outbox/types";
import { useAuthStore } from "@/features/auth/auth-store";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";

const MIN_SEARCH_LENGTH = 3;

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

export function NotificationOutboxPage() {
  const { t } = useTranslation(["notificationOutbox", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canRetry = canAccess(user, "NotificationOutbox.Retry");
  const canCancel = canAccess(user, "NotificationOutbox.Cancel");

  const [channel, setChannel] = useState("");
  const [status, setStatus] = useState("");
  const [search, setSearch] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selectedItem, setSelectedItem] = useState<NotificationOutboxListItem | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [confirmAction, setConfirmAction] = useState<"retry" | "cancel" | null>(null);

  const debouncedSearch = useDebouncedValue(search, 400);
  const effectiveSearch =
    debouncedSearch.trim().length >= MIN_SEARCH_LENGTH ? debouncedSearch.trim() : undefined;

  const listQuery = useQuery({
    queryKey: [
      ...NOTIFICATION_OUTBOX_QUERY_KEY,
      channel,
      status,
      effectiveSearch,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getNotificationOutboxItems({
        channel: channel || undefined,
        status: status || undefined,
        search: effectiveSearch,
        pageNumber,
        pageSize,
      }),
  });

  const detailQuery = useQuery({
    queryKey: [...NOTIFICATION_OUTBOX_QUERY_KEY, "detail", selectedItem?.id],
    queryFn: () => getNotificationOutboxDetail(selectedItem!.id),
    enabled: detailOpen && Boolean(selectedItem?.id),
  });

  const retryMutation = useMutation({
    mutationFn: retryNotificationOutboxItem,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_OUTBOX_QUERY_KEY });
      toast.success(t("notificationOutbox:messages.retrySuccess"));
      setConfirmAction(null);
      setDetailOpen(false);
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("notificationOutbox:messages.retryFailed")));
    },
  });

  const cancelMutation = useMutation({
    mutationFn: cancelNotificationOutboxItem,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_OUTBOX_QUERY_KEY });
      toast.success(t("notificationOutbox:messages.cancelSuccess"));
      setConfirmAction(null);
      setDetailOpen(false);
    },
    onError: (error: unknown) => {
      toast.error(getApiErrorMessage(error, t("notificationOutbox:messages.cancelFailed")));
    },
  });

  const items = useMemo(() => listQuery.data?.items ?? [], [listQuery.data]);

  const openDetail = (item: NotificationOutboxListItem) => {
    setSelectedItem(item);
    setDetailOpen(true);
  };

  const detail = detailQuery.data ?? selectedItem;
  const detailBody = detailQuery.data?.body ?? null;

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("notificationOutbox:title")}
        description={t("notificationOutbox:description")}
      />

      <SectionCard title={t("notificationOutbox:sections.list")}>
        <div className="space-y-4">
          <DataToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("notificationOutbox:filters.searchPlaceholder")}
            actions={
              <Button variant="outline" onClick={() => void listQuery.refetch()}>
                {t("common:actions.refresh")}
              </Button>
            }
          >
            <Select
              value={channel}
              onChange={(event) => {
                setChannel(event.target.value);
                setPageNumber(1);
              }}
            >
              <option value="">{t("notificationOutbox:filters.channelAll")}</option>
              <option value="Sms">{t("notificationOutbox:channels.sms")}</option>
              <option value="Email">{t("notificationOutbox:channels.email")}</option>
            </Select>
            <Select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPageNumber(1);
              }}
            >
              <option value="">{t("notificationOutbox:filters.statusAll")}</option>
              <option value="Pending">{t("notificationOutbox:statuses.pending")}</option>
              <option value="Processing">{t("notificationOutbox:statuses.processing")}</option>
              <option value="Sent">{t("notificationOutbox:statuses.sent")}</option>
              <option value="Failed">{t("notificationOutbox:statuses.failed")}</option>
              <option value="Cancelled">{t("notificationOutbox:statuses.cancelled")}</option>
            </Select>
          </DataToolbar>

          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && items.length === 0 ? (
            <EmptyState title={t("notificationOutbox:empty.title")} />
          ) : null}

          {!listQuery.isLoading && items.length > 0 ? (
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full min-w-[960px] text-sm">
                <thead className="bg-muted/40 text-left">
                  <tr>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.createdAt")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.channel")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.provider")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.recipient")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.subject")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.status")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.attempts")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.nextAttempt")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.related")}</th>
                    <th className="px-3 py-2">{t("notificationOutbox:columns.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id} className="border-t">
                      <td className="px-3 py-2">
                        <DateTimeText value={item.createdAt} />
                      </td>
                      <td className="px-3 py-2">{item.channel}</td>
                      <td className="px-3 py-2">{item.providerKey}</td>
                      <td className="px-3 py-2">{item.recipientMasked}</td>
                      <td className="px-3 py-2">{item.subject ?? "-"}</td>
                      <td className="px-3 py-2">
                        <Badge variant={getStatusVariant(item.status)}>
                          {t(`notificationOutbox:statuses.${item.status.toLowerCase()}`, {
                            defaultValue: item.status,
                          })}
                        </Badge>
                      </td>
                      <td className="px-3 py-2">
                        {item.attemptCount}/{item.maxAttempts}
                      </td>
                      <td className="px-3 py-2">
                        {item.nextAttemptAt ? <DateTimeText value={item.nextAttemptAt} /> : "-"}
                      </td>
                      <td className="px-3 py-2">
                        {item.relatedModule
                          ? `${item.relatedModule}/${item.relatedEvent ?? "-"}`
                          : "-"}
                      </td>
                      <td className="px-3 py-2">
                        <div className="flex flex-wrap gap-2">
                          <Button size="sm" variant="outline" onClick={() => openDetail(item)}>
                            {t("notificationOutbox:actions.detail")}
                          </Button>
                          {canRetry && item.status === "Failed" ? (
                            <Button
                              size="sm"
                              variant="secondary"
                              onClick={() => {
                                setSelectedItem(item);
                                setConfirmAction("retry");
                              }}
                            >
                              {t("notificationOutbox:actions.retry")}
                            </Button>
                          ) : null}
                          {canCancel &&
                          (item.status === "Pending" || item.status === "Failed") ? (
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() => {
                                setSelectedItem(item);
                                setConfirmAction("cancel");
                              }}
                            >
                              {t("notificationOutbox:actions.cancel")}
                            </Button>
                          ) : null}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}

          {listQuery.data ? (
            <TablePagination
              pageNumber={listQuery.data.pageNumber}
              pageSize={listQuery.data.pageSize}
              totalCount={listQuery.data.totalCount}
              totalPages={listQuery.data.totalPages}
              onPageChange={setPageNumber}
              onPageSizeChange={(value) => {
                setPageSize(value);
                setPageNumber(1);
              }}
            />
          ) : null}
        </div>
      </SectionCard>

      <LogDetailDialog
        open={detailOpen}
        onOpenChange={setDetailOpen}
        title={t("notificationOutbox:detail.title")}
        descriptionLabel={t("notificationOutbox:detail.body")}
        description={detailBody}
        closeLabel={t("common:actions.close")}
        rows={[
          { label: t("notificationOutbox:columns.recipient"), value: detail?.recipientMasked },
          { label: t("notificationOutbox:columns.channel"), value: detail?.channel },
          { label: t("notificationOutbox:columns.provider"), value: detail?.providerKey },
          { label: t("notificationOutbox:columns.subject"), value: detail?.subject ?? "-" },
          {
            label: t("notificationOutbox:columns.status"),
            value: detail?.status,
          },
          {
            label: t("notificationOutbox:columns.attempts"),
            value: detail ? `${detail.attemptCount}/${detail.maxAttempts}` : "-",
          },
          {
            label: t("notificationOutbox:detail.lastError"),
            value: detail?.lastErrorMessage ?? "-",
          },
          {
            label: t("notificationOutbox:detail.providerSummary"),
            value: detail?.providerSummary ?? "-",
          },
          {
            label: t("notificationOutbox:columns.related"),
            value: detail?.relatedModule
              ? `${detail.relatedModule} / ${detail.relatedEvent ?? "-"}`
              : "-",
          },
        ]}
      />

      <ConfirmDialog
        open={confirmAction === "retry"}
        onOpenChange={(open) => !open && setConfirmAction(null)}
        title={t("notificationOutbox:confirm.retryTitle")}
        description={t("notificationOutbox:confirm.retryDescription")}
        confirmText={t("notificationOutbox:actions.retry")}
        cancelText={t("common:actions.cancel")}
        onConfirm={() => {
          if (selectedItem) {
            retryMutation.mutate(selectedItem.id);
          }
        }}
        isLoading={retryMutation.isPending}
      />

      <ConfirmDialog
        open={confirmAction === "cancel"}
        onOpenChange={(open) => !open && setConfirmAction(null)}
        title={t("notificationOutbox:confirm.cancelTitle")}
        description={t("notificationOutbox:confirm.cancelDescription")}
        confirmText={t("notificationOutbox:actions.cancel")}
        cancelText={t("common:actions.cancel")}
        variant="danger"
        onConfirm={() => {
          if (selectedItem) {
            cancelMutation.mutate(selectedItem.id);
          }
        }}
        isLoading={cancelMutation.isPending}
      />
    </section>
  );
}
