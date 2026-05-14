import { useMemo, useState } from "react";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import type { DateRange } from "react-day-picker";
import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { DataToolbar } from "@/components/common/DataToolbar";
import { DateRangePicker } from "@/components/common/DateRangePicker";
import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { LogDetailDialog } from "@/components/common/LogDetailDialog";
import { MultiSelectFilter } from "@/components/common/MultiSelectFilter";
import { SectionCard } from "@/components/common/SectionCard";
import { TablePagination } from "@/components/common/TablePagination";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  getSecurityLogFilterOptions,
  getSecurityLogs,
} from "@/features/security-logs/api";
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
          <DataToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("securityLogs:filters.searchPlaceholder")}
            actions={
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
            }
          >
            <MultiSelectFilter
              placeholder={t("securityLogs:filters.eventTypeFilterPlaceholder")}
              options={eventTypeOptions}
              selectedValues={selectedEventTypes}
              onChange={handleEventTypeFilterChange}
              clearLabel={t("securityLogs:filters.clearSelection")}
              emptyLabel={t("securityLogs:filters.noOptions")}
              searchPlaceholder={t("securityLogs:filters.searchOptions")}
            />
            <MultiSelectFilter
              placeholder={t("securityLogs:filters.severityFilterPlaceholder")}
              options={severityOptions}
              selectedValues={selectedSeverities}
              onChange={handleSeverityFilterChange}
              clearLabel={t("securityLogs:filters.clearSelection")}
              emptyLabel={t("securityLogs:filters.noOptions")}
              searchPlaceholder={t("securityLogs:filters.searchOptions")}
            />
            <DateRangePicker
              value={dateRange}
              onChange={handleDateRangeChange}
              placeholder={t("securityLogs:filters.dateRangePlaceholder")}
              clearLabel={t("securityLogs:filters.clearDateRange")}
              locale={calendarLocale}
            />
          </DataToolbar>

          {securityLogsQuery.isLoading ? <LoadingState /> : null}

          {securityLogsQuery.isSuccess && !securityLogs.length ? (
            <EmptyState
              title={t("securityLogs:empty.title")}
              description={t("securityLogs:empty.description")}
            />
          ) : null}

          {securityLogs.length ? (
            <div className="overflow-x-auto rounded-lg border bg-card">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/50 text-left">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t("securityLogs:table.createdAt")}</th>
                    <th className="px-3 py-2 font-medium">{t("securityLogs:table.eventType")}</th>
                    <th className="px-3 py-2 font-medium">{t("securityLogs:table.severity")}</th>
                    <th className="px-3 py-2 font-medium">{t("securityLogs:table.userName")}</th>
                    <th className="px-3 py-2 font-medium">{t("securityLogs:table.ipAddress")}</th>
                    <th className="px-3 py-2 font-medium">{t("securityLogs:table.description")}</th>
                    <th className="px-3 py-2 font-medium">{t("securityLogs:table.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {securityLogs.map((logItem) => (
                    <tr key={logItem.id} className="border-t align-top hover:bg-muted/20">
                      <td className="whitespace-nowrap px-3 py-2">
                        <DateTimeText value={logItem.createdAt} />
                      </td>
                      <td className="px-3 py-2">{logItem.eventType}</td>
                      <td className="px-3 py-2">
                        <Badge variant={getSeverityBadgeVariant(logItem.severity)}>
                          {logItem.severity}
                        </Badge>
                      </td>
                      <td className="px-3 py-2">{logItem.userName || "-"}</td>
                      <td className="px-3 py-2">{logItem.ipAddress || "-"}</td>
                      <td className="max-w-96 px-3 py-2">
                        <span className="line-clamp-2">{logItem.description || "-"}</span>
                      </td>
                      <td className="px-3 py-2">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => setSelectedSecurityLog(logItem)}
                        >
                          {t("securityLogs:actions.detail")}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {securityLogsQuery.data && securityLogsQuery.data.totalCount > 0 ? (
                <TablePagination
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
              ) : null}
            </div>
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

function getSeverityBadgeVariant(
  severity: string,
): "default" | "secondary" | "outline" | "info" | "success" | "warning" | "destructive" {
  const normalizedSeverity = severity.trim().toLocaleLowerCase();

  if (normalizedSeverity === "info") {
    return "info";
  }

  if (normalizedSeverity === "low") {
    return "secondary";
  }

  if (normalizedSeverity === "warning") {
    return "warning";
  }

  if (normalizedSeverity === "error" || normalizedSeverity === "critical" || normalizedSeverity === "high") {
    return "destructive";
  }

  if (normalizedSeverity === "success") {
    return "success";
  }

  return "secondary";
}
