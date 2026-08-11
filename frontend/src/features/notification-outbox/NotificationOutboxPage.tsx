import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CircleAlert, RotateCcw, XCircle } from "lucide-react";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { DateTimeText } from "@/components/common/DateTimeText";
import { LogDetailDialog } from "@/components/common/LogDetailDialog";
import { PageContainer } from "@/components/common/PageContainer";
import { PageHeader } from "@/components/common/PageHeader";
import {
  NotificationChannelBadge,
  NotificationDeliveryStatusBadge,
} from "@/features/notification-outbox/NotificationDeliveryBadges";
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
import { PermissionCodes } from "@/lib/permission-codes";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

const MIN_SEARCH_LENGTH = 3;

export function NotificationOutboxPage() {
  const { t } = useTranslation(["notificationOutbox", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canRetry = canAccess(user, PermissionCodes.NotificationOutbox.Retry);
  const canCancel = canAccess(user, PermissionCodes.NotificationOutbox.Cancel);

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

  if (listQuery.isError) {
    const routeState = createApiErrorRouteState(listQuery.error, {
      fromPath: "/notification-outbox",
      retryPath: "/notification-outbox",
      sourceLabel: t("notificationOutbox:sections.list"),
    });
    return <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />;
  }

  return (
    <PageContainer variant="fluid">
      <PageHeader
        title={t("notificationOutbox:title")}
        description={t("notificationOutbox:description")}
        actions={
          <Button variant="outline" onClick={() => void listQuery.refetch()}>
            {t("common:actions.refresh")}
          </Button>
        }
      />

      <div className="flex min-w-0 flex-col gap-4">
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
          activeFilters={[
            ...(channel
              ? [{
                  id: "channel",
                  label: t("notificationOutbox:columns.channel"),
                  value:
                    channel === "Sms"
                      ? t("common:channels.sms")
                      : t("common:channels.email"),
                  onRemove: () => {
                    setChannel("");
                    setPageNumber(1);
                  },
                }]
              : []),
            ...(status
              ? [{
                  id: "status",
                  label: t("common:fields.status"),
                  value: t(`notificationOutbox:statuses.${status.toLowerCase()}`),
                  onRemove: () => {
                    setStatus("");
                    setPageNumber(1);
                  },
                }]
              : []),
          ]}
          filterContent={
            <div className="space-y-3">
              <Select
                value={channel}
                onChange={(event) => {
                  setChannel(event.target.value);
                  setPageNumber(1);
                }}
                aria-label={t("notificationOutbox:columns.channel")}
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
                aria-label={t("common:fields.status")}
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
        />

        <DataTable
          table={table}
          isLoading={listQuery.isLoading}
          emptyMessage={t("notificationOutbox:empty.title")}
          emptyDescription={t("notificationOutbox:empty.description")}
          footer={
            listQuery.data && listQuery.data.totalCount > 0 ? (
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
      </div>

      <LogDetailDialog
        open={detailOpen}
        onOpenChange={setDetailOpen}
        title={t("notificationOutbox:detail.title")}
        descriptionLabel={t("notificationOutbox:detail.body")}
        description={detailBody}
        closeLabel={t("common:actions.close")}
        isLoading={detailQuery.isLoading}
        loadingLabel={t("notificationOutbox:detail.loading")}
        error={
          detailQuery.isError
            ? getApiErrorMessage(detailQuery.error, t("notificationOutbox:detail.loadFailed"))
            : undefined
        }
        actions={
          <>
            {canRetry && detail?.status === "Failed" ? (
              <Button variant="outline" onClick={() => setConfirmAction("retry")}>
                <RotateCcw className="size-4" aria-hidden />
                {t("notificationOutbox:actions.retry")}
              </Button>
            ) : null}
            {canCancel && (detail?.status === "Pending" || detail?.status === "Failed") ? (
              <Button variant="destructive" onClick={() => setConfirmAction("cancel")}>
                <XCircle className="size-4" aria-hidden />
                {t("notificationOutbox:actions.cancel")}
              </Button>
            ) : null}
          </>
        }
        rows={[
          { label: t("notificationOutbox:columns.recipient"), value: detail?.recipientMasked },
          {
            label: t("notificationOutbox:columns.channel"),
            value: detail ? <NotificationChannelBadge channel={detail.channel} t={t} /> : "-",
          },
          {
            label: t("notificationOutbox:columns.status"),
            value: detail ? <NotificationDeliveryStatusBadge status={detail.status} t={t} /> : "-",
          },
          { label: t("notificationOutbox:columns.provider"), value: detail?.providerKey },
          { label: t("notificationOutbox:columns.subject"), value: detail?.subject ?? "-" },
          {
            label: t("notificationOutbox:columns.attempts"),
            value: detail
              ? t("notificationOutbox:columns.attemptProgress", {
                  current: detail.attemptCount,
                  max: detail.maxAttempts,
                })
              : "-",
          },
          {
            label: t("notificationOutbox:columns.nextAttempt"),
            value: detail?.nextAttemptAt ? <DateTimeText value={detail.nextAttemptAt} /> : "-",
          },
          {
            label: t("notificationOutbox:detail.lastAttempt"),
            value: detail?.lastAttemptAt ? <DateTimeText value={detail.lastAttemptAt} /> : "-",
          },
          {
            label: t("notificationOutbox:detail.sentAt"),
            value: detail?.sentAt ? <DateTimeText value={detail.sentAt} /> : "-",
          },
          {
            label: t("notificationOutbox:detail.lastError"),
            value: detail?.lastErrorMessage ? (
              <span className="flex items-start gap-2 text-destructive">
                <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                <span>{detail.lastErrorMessage}</span>
              </span>
            ) : "-",
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
          {
            label: t("notificationOutbox:detail.relatedEntity"),
            value: detail?.relatedEntityType
              ? `${detail.relatedEntityType} / ${detail.relatedEntityId ?? "-"}`
              : "-",
          },
          {
            label: t("notificationOutbox:detail.correlationId"),
            value: detailQuery.data?.correlationId ? (
              <span className="font-mono text-xs">{detailQuery.data.correlationId}</span>
            ) : "-",
          },
          {
            label: t("notificationOutbox:columns.createdAt"),
            value: detail?.createdAt ? <DateTimeText value={detail.createdAt} /> : "-",
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
    </PageContainer>
  );
}
