import { useMemo, useState, type ReactNode } from "react";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import type { DateRange } from "react-day-picker";
import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { DateRangePicker } from "@/components/common/DateRangePicker";
import { DateTimeText } from "@/components/common/DateTimeText";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { LogDetailDialog } from "@/components/common/LogDetailDialog";
import { MultiSelectFilter } from "@/components/common/MultiSelectFilter";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { getAuditLogFilterOptions, getAuditLogs } from "@/features/audit-logs/api";
import {
  createAuditLogColumns,
  getAuditActionBadgeVariant,
} from "@/features/audit-logs/audit-log-columns";
import type { AuditLogListItem } from "@/features/audit-logs/types";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

const MIN_SEARCH_LENGTH = 3;

function renderOptionalText(value: string | null | undefined): ReactNode {
  if (!value) {
    return <span className="text-muted-foreground">-</span>;
  }
  return value;
}

export function AuditLogsPage() {
  const { t, i18n } = useTranslation(["auditLogs", "common"]);

  const [search, setSearch] = useState("");
  const [selectedActions, setSelectedActions] = useState<string[]>([]);
  const [selectedEntityNames, setSelectedEntityNames] = useState<string[]>([]);
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selectedAuditLog, setSelectedAuditLog] = useState<AuditLogListItem | null>(null);

  const from = dateRange?.from ? toUtcStartOfLocalDay(dateRange.from) : undefined;
  const to = dateRange?.to ? toUtcEndOfLocalDay(dateRange.to) : undefined;
  const calendarLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const debouncedSearch = useDebouncedValue(search, 400);
  const normalizedSearch = debouncedSearch.trim();
  const effectiveSearch =
    normalizedSearch.length >= MIN_SEARCH_LENGTH ? normalizedSearch : undefined;

  const trimmedSearch = search.trim();
  const isActionFilterActive = selectedActions.length > 0;
  const isEntityFilterActive = selectedEntityNames.length > 0;
  const isDateRangeActive = Boolean(dateRange?.from || dateRange?.to);
  const activeFilterCount =
    (isActionFilterActive ? 1 : 0) +
    (isEntityFilterActive ? 1 : 0) +
    (isDateRangeActive ? 1 : 0);
  const showMinSearchHint =
    trimmedSearch.length > 0 && trimmedSearch.length < MIN_SEARCH_LENGTH;

  const auditLogsQuery = useQuery({
    queryKey: [
      "audit-logs",
      "list",
      effectiveSearch,
      selectedActions,
      selectedEntityNames,
      from,
      to,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getAuditLogs({
        search: effectiveSearch,
        actions: selectedActions,
        entityNames: selectedEntityNames,
        from,
        to,
        pageNumber,
        pageSize,
      }),
  });

  const filterOptionsQuery = useQuery({
    queryKey: ["audit-logs", "filter-options"],
    queryFn: getAuditLogFilterOptions,
  });

  const auditLogs = useMemo(() => auditLogsQuery.data?.items ?? [], [auditLogsQuery.data]);
  const actionOptions = filterOptionsQuery.data?.actions ?? [];
  const entityNameOptions = filterOptionsQuery.data?.entityNames ?? [];

  const columns = useMemo(
    () =>
      createAuditLogColumns({
        t,
        onDetail: setSelectedAuditLog,
      }),
    [t],
  );

  const table = useServerDataTable({
    data: auditLogs,
    columns,
    pageCount: auditLogsQuery.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  const handleRefresh = () => {
    auditLogsQuery.refetch();
  };
  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPageNumber(1);
  };
  const handleActionFilterChange = (values: string[]) => {
    setSelectedActions(values);
    setPageNumber(1);
  };
  const handleEntityNameFilterChange = (values: string[]) => {
    setSelectedEntityNames(values);
    setPageNumber(1);
  };
  const handleDateRangeChange = (value: DateRange | undefined) => {
    setDateRange(value);
    setPageNumber(1);
  };
  const handleClearAllFilters = () => {
    setSearch("");
    setSelectedActions([]);
    setSelectedEntityNames([]);
    setDateRange(undefined);
    setPageNumber(1);
  };

  if (auditLogsQuery.isError) {
    const routeState = createApiErrorRouteState(auditLogsQuery.error, {
      fromPath: "/audit-logs",
      retryPath: "/audit-logs",
      sourceLabel: t("auditLogs:sections.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <SectionCard title={t("auditLogs:sections.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("auditLogs:filters.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={handleClearAllFilters}
            filterContent={
              <div className="space-y-3">
                <MultiSelectFilter
                  placeholder={t("auditLogs:filters.actionFilterPlaceholder")}
                  options={actionOptions}
                  selectedValues={selectedActions}
                  onChange={handleActionFilterChange}
                  clearLabel={t("common:select.clearSelection")}
                  emptyLabel={t("common:select.noOptions")}
                  searchPlaceholder={t("common:select.searchOptions")}
                />
                <MultiSelectFilter
                  placeholder={t("auditLogs:filters.entityNameFilterPlaceholder")}
                  options={entityNameOptions}
                  selectedValues={selectedEntityNames}
                  onChange={handleEntityNameFilterChange}
                  clearLabel={t("common:select.clearSelection")}
                  emptyLabel={t("common:select.noOptions")}
                  searchPlaceholder={t("common:select.searchOptions")}
                />
                <DateRangePicker
                  value={dateRange}
                  onChange={handleDateRangeChange}
                  placeholder={t("common:dateRange.placeholder")}
                  clearLabel={t("common:dateRange.clear")}
                  locale={calendarLocale}
                />
              </div>
            }
            toolbarFooter={
              showMinSearchHint ? (
                <p className="text-xs text-muted-foreground">
                  {t("auditLogs:filters.minSearchLengthHint")}
                </p>
              ) : null
            }
            actions={
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
            }
          />

          {auditLogsQuery.isLoading ? <LoadingState /> : null}

          {auditLogsQuery.isSuccess && !auditLogs.length ? (
            <EmptyState
              title={t("auditLogs:empty.title")}
              description={t("auditLogs:empty.description")}
            />
          ) : null}

          {auditLogs.length ? (
            <DataTable
              table={table}
              footer={
                auditLogsQuery.data && auditLogsQuery.data.totalCount > 0 ? (
                  <DataTablePagination
                    mode="server"
                    pageNumber={auditLogsQuery.data.pageNumber}
                    pageSize={auditLogsQuery.data.pageSize}
                    totalCount={auditLogsQuery.data.totalCount}
                    totalPages={auditLogsQuery.data.totalPages}
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

      <LogDetailDialog
        open={Boolean(selectedAuditLog)}
        onOpenChange={(open) => {
          if (!open) setSelectedAuditLog(null);
        }}
        title={t("auditLogs:detail.title")}
        rows={[
          {
            label: t("auditLogs:detail.createdAt"),
            value: selectedAuditLog ? (
              <DateTimeText value={selectedAuditLog.createdAt} />
            ) : (
              renderOptionalText(null)
            ),
          },
          {
            label: t("auditLogs:detail.action"),
            value: selectedAuditLog ? (
              <Badge variant={getAuditActionBadgeVariant(selectedAuditLog.action)}>
                {selectedAuditLog.action}
              </Badge>
            ) : (
              renderOptionalText(null)
            ),
          },
          {
            label: t("auditLogs:detail.entityName"),
            value: selectedAuditLog?.entityName ? (
              <CodeBadge>{selectedAuditLog.entityName}</CodeBadge>
            ) : (
              renderOptionalText(null)
            ),
          },
          {
            label: t("auditLogs:detail.entityId"),
            value: selectedAuditLog?.entityId ? (
              <span className="break-all font-mono text-xs md:text-sm">
                {selectedAuditLog.entityId}
              </span>
            ) : (
              renderOptionalText(null)
            ),
          },
          {
            label: t("auditLogs:detail.actorUserId"),
            value: selectedAuditLog?.actorUserId ? (
              <span className="break-all font-mono text-xs md:text-sm">
                {selectedAuditLog.actorUserId}
              </span>
            ) : (
              renderOptionalText(null)
            ),
          },
          {
            label: t("auditLogs:detail.actorUserName"),
            value: renderOptionalText(selectedAuditLog?.actorUserName),
          },
          {
            label: t("auditLogs:detail.ipAddress"),
            value: renderOptionalText(selectedAuditLog?.ipAddress),
          },
          {
            label: t("auditLogs:detail.userAgent"),
            value: renderOptionalText(selectedAuditLog?.userAgent),
          },
        ]}
        description={selectedAuditLog?.description ?? undefined}
        descriptionLabel={t("auditLogs:detail.description")}
        closeLabel={t("common:actions.close")}
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
