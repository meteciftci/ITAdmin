import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { LogDetailDialog } from "@/components/common/LogDetailDialog";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { createNotificationOutboxColumns } from "@/features/notification-outbox/notification-outbox-columns";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
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

  const activeFilterCount = (channel ? 1 : 0) + (status ? 1 : 0);

  const columns = useMemo(
    () =>
      createNotificationOutboxColumns({
        t,
        canRetry,
        canCancel,
        onDetail: (item) => {
          setSelectedItem(item);
          setDetailOpen(true);
        },
        onRetry: (item) => {
          setSelectedItem(item);
          setConfirmAction("retry");
        },
        onCancel: (item) => {
          setSelectedItem(item);
          setConfirmAction("cancel");
        },
      }),
    [t, canRetry, canCancel],
  );

  const table = useServerDataTable({
    data: items,
    columns,
    pageCount: listQuery.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

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
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("notificationOutbox:filters.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setChannel("");
              setStatus("");
              setPageNumber(1);
            }}
            filterContent={
              <div className="space-y-3">
                <Select
                  value={channel}
                  onChange={(event) => {
                    setChannel(event.target.value);
                    setPageNumber(1);
                  }}
                  className="w-full"
                >
                  <option value="">{t("notificationOutbox:filters.channelAll")}</option>
                  <option value="Sms">{t("common:channels.sms")}</option>
                  <option value="Email">{t("common:channels.email")}</option>
                </Select>
                <Select
                  value={status}
                  onChange={(event) => {
                    setStatus(event.target.value);
                    setPageNumber(1);
                  }}
                  className="w-full"
                >
                  <option value="">{t("notificationOutbox:filters.statusAll")}</option>
                  <option value="Pending">{t("notificationOutbox:statuses.pending")}</option>
                  <option value="Processing">{t("notificationOutbox:statuses.processing")}</option>
                  <option value="Sent">{t("notificationOutbox:statuses.sent")}</option>
                  <option value="Failed">{t("notificationOutbox:statuses.failed")}</option>
                  <option value="Cancelled">{t("notificationOutbox:statuses.cancelled")}</option>
                </Select>
              </div>
            }
            actions={
              <Button variant="outline" onClick={() => void listQuery.refetch()}>
                {t("common:actions.refresh")}
              </Button>
            }
          />

          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && items.length === 0 ? (
            <EmptyState title={t("notificationOutbox:empty.title")} />
          ) : null}

          {!listQuery.isLoading && items.length > 0 ? (
            <DataTable
              table={table}
              footer={
                listQuery.data ? (
                  <DataTablePagination
                    mode="server"
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
                ) : null
              }
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
