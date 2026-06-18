import { useMemo, useState } from "react";
import type { DateRange } from "react-day-picker";
import { useQuery } from "@tanstack/react-query";
import { Navigate, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { DateRangePicker } from "@/components/common/DateRangePicker";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { useAuthStore } from "@/features/auth/auth-store";
import { createAdOperationLogColumns } from "@/features/ad-management/ad-operation-log-columns";
import {
  AD_MANAGEMENT_SETTINGS_QUERY_KEY,
  getAdManagementSettings,
} from "@/features/ad-management/api";
import { AdOperationLogDetailDialog } from "@/features/ad-management/components/AdOperationLogDetailDialog";
import { useAdOperationLogLabels } from "@/features/ad-management/hooks/useAdOperationLogLabels";
import { resolveAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import {
  AD_OPERATION_LOGS_QUERY_KEY,
  getAdOperationLogById,
  getAdOperationLogs,
} from "@/features/ad-management/operation-logs-api";
import {
  AD_OPERATION_LOG_OPERATION_TYPES,
  AD_OPERATION_LOG_STATUSES,
  type AdOperationLogListItem,
} from "@/features/ad-management/operation-logs-types";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";

export function AdOperationLogsPage() {
  const { t, i18n } = useTranslation(["adOperationLogs", "common"]);
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((state) => state.user);
  const canViewSettings = canAccess(user, PermissionCodes.AdManagement.Settings.View);
  const { getOperationLabel, getStatusLabel } = useAdOperationLogLabels();
  const initialTargetObjectGuid = searchParams.get("targetObjectGuid")?.trim() ?? "";

  const [status, setStatus] = useState("");
  const [operationType, setOperationType] = useState("");
  const [targetObjectGuid, setTargetObjectGuid] = useState(initialTargetObjectGuid);
  const [targetSearch, setTargetSearch] = useState("");
  const [actorUserName, setActorUserName] = useState("");
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selectedLogId, setSelectedLogId] = useState<string | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);

  const from = dateRange?.from ? toUtcStartOfLocalDay(dateRange.from) : undefined;
  const to = dateRange?.to ? toUtcEndOfLocalDay(dateRange.to) : undefined;
  const calendarLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const settingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getAdManagementSettings,
    enabled: canViewSettings,
    staleTime: 60_000,
  });

  const moduleStatus = resolveAdManagementModuleStatus(settingsQuery.data, {
    isLoading: settingsQuery.isLoading,
    isError: settingsQuery.isError,
  });

  const showModuleWarning =
    canViewSettings && !moduleStatus.isLoading && !moduleStatus.isOperational;

  const effectiveTargetObjectGuid = targetObjectGuid.trim();
  const effectiveTargetSearch = effectiveTargetObjectGuid
    ? ""
    : targetSearch.trim();

  const listQuery = useQuery({
    queryKey: [
      ...AD_OPERATION_LOGS_QUERY_KEY,
      "list",
      status,
      operationType,
      effectiveTargetObjectGuid,
      effectiveTargetSearch,
      actorUserName,
      from,
      to,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getAdOperationLogs({
        status: status || undefined,
        operationType: operationType || undefined,
        targetObjectGuid: effectiveTargetObjectGuid || undefined,
        targetSamAccountName: effectiveTargetSearch || undefined,
        actorUserName: actorUserName.trim() || undefined,
        dateFrom: from,
        dateTo: to,
        pageNumber,
        pageSize,
      }),
  });

  const detailQuery = useQuery({
    queryKey: [...AD_OPERATION_LOGS_QUERY_KEY, "detail", selectedLogId],
    queryFn: () => getAdOperationLogById(selectedLogId!),
    enabled: detailOpen && Boolean(selectedLogId),
  });

  const logs = useMemo(() => listQuery.data?.items ?? [], [listQuery.data]);

  const columns = useMemo(
    () =>
      createAdOperationLogColumns({
        t,
        getOperationLabel,
        getStatusLabel,
        onDetail: (item: AdOperationLogListItem) => {
          setSelectedLogId(item.id);
          setDetailOpen(true);
        },
      }),
    [t, getOperationLabel, getStatusLabel],
  );

  const table = useServerDataTable({
    data: logs,
    columns,
    pageCount: listQuery.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  const activeFilterCount =
    (status ? 1 : 0) +
    (operationType ? 1 : 0) +
    (effectiveTargetObjectGuid ? 1 : 0) +
    (effectiveTargetSearch ? 1 : 0) +
    (actorUserName.trim() ? 1 : 0) +
    (dateRange?.from || dateRange?.to ? 1 : 0);

  const handleClearFilters = () => {
    setStatus("");
    setOperationType("");
    setTargetObjectGuid("");
    setTargetSearch("");
    setActorUserName("");
    setDateRange(undefined);
    setPageNumber(1);
    if (searchParams.has("targetObjectGuid")) {
      const nextParams = new URLSearchParams(searchParams);
      nextParams.delete("targetObjectGuid");
      setSearchParams(nextParams, { replace: true });
    }
  };

  if (listQuery.isError) {
    const routeState = createApiErrorRouteState(listQuery.error, {
      fromPath: "/monitoring/module-logs/ad-operation-logs",
      retryPath: "/monitoring/module-logs/ad-operation-logs",
      sourceLabel: t("sections.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("title")}
        description={t("description")}
      />

      {showModuleWarning ? (
        <Alert>
          <AlertTitle>{t("moduleWarning.title")}</AlertTitle>
          <AlertDescription>{t("moduleWarning.description")}</AlertDescription>
        </Alert>
      ) : null}

      <SectionCard title={t("sections.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={effectiveTargetObjectGuid || targetSearch}
            onSearchChange={(value) => {
              setTargetObjectGuid("");
              setTargetSearch(value);
              setPageNumber(1);
              if (searchParams.has("targetObjectGuid")) {
                const nextParams = new URLSearchParams(searchParams);
                nextParams.delete("targetObjectGuid");
                setSearchParams(nextParams, { replace: true });
              }
            }}
            searchPlaceholder={t("filters.targetSearchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={handleClearFilters}
            filterContent={
              <div className="space-y-3">
                <Select
                  value={status}
                  onChange={(event) => {
                    setStatus(event.target.value);
                    setPageNumber(1);
                  }}
                  className="w-full"
                >
                  <option value="">{t("filters.statusAll")}</option>
                  {AD_OPERATION_LOG_STATUSES.map((item) => (
                    <option key={item} value={item}>
                      {getStatusLabel(item, null)}
                    </option>
                  ))}
                </Select>
                <Select
                  value={operationType}
                  onChange={(event) => {
                    setOperationType(event.target.value);
                    setPageNumber(1);
                  }}
                  className="w-full"
                >
                  <option value="">{t("filters.operationAll")}</option>
                  {AD_OPERATION_LOG_OPERATION_TYPES.map((item) => (
                    <option key={item} value={item}>
                      {getOperationLabel(item)}
                    </option>
                  ))}
                </Select>
                <div className="space-y-1">
                  <label
                    className="text-xs text-muted-foreground"
                    htmlFor="target-object-guid-filter"
                  >
                    {t("filters.targetObjectGuidLabel")}
                  </label>
                  <Input
                    id="target-object-guid-filter"
                    value={targetObjectGuid}
                    onChange={(event) => {
                      setTargetObjectGuid(event.target.value);
                      if (event.target.value.trim()) {
                        setTargetSearch("");
                      }
                      setPageNumber(1);
                    }}
                    placeholder={t("filters.targetObjectGuidPlaceholder")}
                    className="font-mono text-xs"
                  />
                </div>
                <div className="space-y-1">
                  <label className="text-xs text-muted-foreground" htmlFor="actor-filter">
                    {t("filters.actorLabel")}
                  </label>
                  <Input
                    id="actor-filter"
                    value={actorUserName}
                    onChange={(event) => {
                      setActorUserName(event.target.value);
                      setPageNumber(1);
                    }}
                    placeholder={t("filters.actorPlaceholder")}
                  />
                </div>
                <DateRangePicker
                  value={dateRange}
                  onChange={(value) => {
                    setDateRange(value);
                    setPageNumber(1);
                  }}
                  placeholder={t("common:dateRange.placeholder")}
                  clearLabel={t("common:dateRange.clear")}
                  locale={calendarLocale}
                />
              </div>
            }
            actions={
              <Button variant="outline" onClick={() => void listQuery.refetch()}>
                {t("common:actions.refresh")}
              </Button>
            }
          />

          {listQuery.isLoading ? <LoadingState /> : null}

          {listQuery.isSuccess && !logs.length ? (
            <EmptyState
              title={t("empty.title")}
              description={t("empty.description")}
            />
          ) : null}

          {logs.length ? (
            <DataTable
              table={table}
              footer={
                listQuery.data && listQuery.data.totalCount > 0 ? (
                  <DataTablePagination
                    mode="server"
                    pageNumber={listQuery.data.pageNumber}
                    pageSize={listQuery.data.pageSize}
                    totalCount={listQuery.data.totalCount}
                    totalPages={listQuery.data.totalPages}
                    onPageChange={setPageNumber}
                    onPageSizeChange={(nextPageSize) => {
                      setPageSize(nextPageSize);
                      setPageNumber(1);
                    }}
                  />
                ) : null
              }
            />
          ) : null}
        </div>
      </SectionCard>

      <AdOperationLogDetailDialog
        open={detailOpen}
        onOpenChange={(open) => {
          setDetailOpen(open);
          if (!open) {
            setSelectedLogId(null);
          }
        }}
        detail={detailQuery.data}
        isLoading={detailQuery.isLoading}
        getOperationLabel={getOperationLabel}
        getStatusLabel={getStatusLabel}
      />
    </section>
  );
}

function toUtcStartOfLocalDay(value: Date): string {
  const startOfLocalDay = new Date(
    value.getFullYear(),
    value.getMonth(),
    value.getDate(),
    0,
    0,
    0,
    0,
  );

  return startOfLocalDay.toISOString();
}

function toUtcEndOfLocalDay(value: Date): string {
  const endOfLocalDay = new Date(
    value.getFullYear(),
    value.getMonth(),
    value.getDate(),
    23,
    59,
    59,
    999,
  );

  return endOfLocalDay.toISOString();
}
