import { useMemo, useState } from "react";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import type { DateRange } from "react-day-picker";
import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

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
import {
  getSecurityLogFilterOptions,
  getSecurityLogs,
} from "@/features/security-logs/api";
import {
  createSecurityLogColumns,
  getSeverityBadgeVariant,
} from "@/features/security-logs/security-log-columns";
import type { SecurityLogListItem } from "@/features/security-logs/types";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

export function SecurityLogsPage() {
  const { t, i18n } = useTranslation(["securityLogs", "common"]);

  const [search, setSearch] = useState("");
  const [selectedEventTypes, setSelectedEventTypes] = useState<string[]>([]);
  const [selectedSeverities, setSelectedSeverities] = useState<string[]>([]);
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selectedSecurityLog, setSelectedSecurityLog] = useState<SecurityLogListItem | null>(null);

  const from = dateRange?.from ? toUtcStartOfLocalDay(dateRange.from) : undefined;
  const to = dateRange?.to ? toUtcEndOfLocalDay(dateRange.to) : undefined;
  const calendarLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const debouncedSearch = useDebouncedValue(search, 400);
  const normalizedSearch = debouncedSearch.trim();
  const effectiveSearch =
    normalizedSearch.length >= 3 ? normalizedSearch : undefined;

  const securityLogsQuery = useQuery({
    queryKey: [
      "security-logs",
      "list",
      effectiveSearch,
      selectedEventTypes,
      selectedSeverities,
      from,
      to,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getSecurityLogs({
        search: effectiveSearch,
        eventTypes: selectedEventTypes,
        severities: selectedSeverities,
        from,
        to,
        pageNumber,
        pageSize,
      }),
  });

  const filterOptionsQuery = useQuery({
    queryKey: ["security-logs", "filter-options"],
    queryFn: getSecurityLogFilterOptions,
  });

  const securityLogs = useMemo(
    () => securityLogsQuery.data?.items ?? [],
    [securityLogsQuery.data],
  );
  const eventTypeOptions = filterOptionsQuery.data?.eventTypes ?? [];
  const severityOptions = filterOptionsQuery.data?.severities ?? [];

  const activeFilterCount =
    (selectedEventTypes.length > 0 ? 1 : 0) +
    (selectedSeverities.length > 0 ? 1 : 0) +
    (dateRange?.from || dateRange?.to ? 1 : 0);

  const columns = useMemo(
    () =>
      createSecurityLogColumns({
        t,
        onDetail: setSelectedSecurityLog,
      }),
    [t],
  );

  const table = useServerDataTable({
    data: securityLogs,
    columns,
    pageCount: securityLogsQuery.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  const handleClearAllFilters = () => {
    setSelectedEventTypes([]);
    setSelectedSeverities([]);
    setDateRange(undefined);
    setPageNumber(1);
  };

  const handleRefresh = () => {
    securityLogsQuery.refetch();
  };
  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPageNumber(1);
  };
  const handleEventTypeFilterChange = (values: string[]) => {
    setSelectedEventTypes(values);
    setPageNumber(1);
  };
  const handleSeverityFilterChange = (values: string[]) => {
    setSelectedSeverities(values);
    setPageNumber(1);
  };
  const handleDateRangeChange = (value: DateRange | undefined) => {
    setDateRange(value);
    setPageNumber(1);
  };

  if (securityLogsQuery.isError) {
    const routeState = createApiErrorRouteState(securityLogsQuery.error, {
      fromPath: "/security-logs",
      retryPath: "/security-logs",
      sourceLabel: t("securityLogs:sections.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <SectionCard title={t("securityLogs:sections.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("securityLogs:filters.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={handleClearAllFilters}
            filterContent={
              <div className="space-y-3">
                <MultiSelectFilter
                  placeholder={t("securityLogs:filters.eventTypeFilterPlaceholder")}
                  options={eventTypeOptions}
                  selectedValues={selectedEventTypes}
                  onChange={handleEventTypeFilterChange}
                  clearLabel={t("common:select.clearSelection")}
                  emptyLabel={t("common:select.noOptions")}
                  searchPlaceholder={t("common:select.searchOptions")}
                />
                <MultiSelectFilter
                  placeholder={t("securityLogs:filters.severityFilterPlaceholder")}
                  options={severityOptions}
                  selectedValues={selectedSeverities}
                  onChange={handleSeverityFilterChange}
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
            actions={
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
            }
          />

          {securityLogsQuery.isLoading ? <LoadingState /> : null}

          {securityLogsQuery.isSuccess && !securityLogs.length ? (
            <EmptyState
              title={t("securityLogs:empty.title")}
              description={t("securityLogs:empty.description")}
            />
          ) : null}

          {securityLogs.length ? (
            <DataTable
              table={table}
              footer={
                securityLogsQuery.data && securityLogsQuery.data.totalCount > 0 ? (
                  <DataTablePagination
                    mode="server"
                    pageNumber={securityLogsQuery.data.pageNumber}
                    pageSize={securityLogsQuery.data.pageSize}
                    totalCount={securityLogsQuery.data.totalCount}
                    totalPages={securityLogsQuery.data.totalPages}
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
        open={Boolean(selectedSecurityLog)}
        onOpenChange={(open) => {
          if (!open) setSelectedSecurityLog(null);
        }}
        title={t("securityLogs:detail.title")}
        rows={[
          {
            label: t("securityLogs:detail.createdAt"),
            value: selectedSecurityLog ? <DateTimeText value={selectedSecurityLog.createdAt} /> : "-",
          },
          {
            label: t("securityLogs:detail.eventType"),
            value: selectedSecurityLog?.eventType || "-",
          },
          {
            label: t("securityLogs:detail.severity"),
            value: selectedSecurityLog ? (
              <Badge variant={getSeverityBadgeVariant(selectedSecurityLog.severity)}>
                {selectedSecurityLog.severity}
              </Badge>
            ) : (
              "-"
            ),
          },
          {
            label: t("securityLogs:detail.userId"),
            value: selectedSecurityLog?.userId ? (
              <span className="font-mono text-xs md:text-sm">{selectedSecurityLog.userId}</span>
            ) : (
              "-"
            ),
          },
          {
            label: t("securityLogs:detail.userName"),
            value: selectedSecurityLog?.userName || "-",
          },
          {
            label: t("securityLogs:detail.ipAddress"),
            value: selectedSecurityLog?.ipAddress || "-",
          },
          {
            label: t("securityLogs:detail.userAgent"),
            value: selectedSecurityLog?.userAgent || "-",
          },
        ]}
        description={selectedSecurityLog?.description}
        descriptionLabel={t("securityLogs:detail.description")}
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

