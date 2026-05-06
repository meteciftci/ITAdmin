import { useMemo, useState } from "react";
import type { DateRange } from "react-day-picker";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { DataToolbar } from "@/components/common/DataToolbar";
import { DateRangePicker } from "@/components/common/DateRangePicker";
import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { MultiSelectFilter } from "@/components/common/MultiSelectFilter";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { TablePagination } from "@/components/common/TablePagination";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { getAuditLogFilterOptions, getAuditLogs } from "@/features/audit-logs/api";
import { getApiErrorMessage } from "@/lib/api-error";

export function AuditLogsPage() {
  const { t, i18n } = useTranslation(["auditLogs", "common"]);

  const [search, setSearch] = useState("");
  const [selectedActions, setSelectedActions] = useState<string[]>([]);
  const [selectedEntityNames, setSelectedEntityNames] = useState<string[]>([]);
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const from = dateRange?.from ? toUtcStartOfLocalDay(dateRange.from) : undefined;
  const to = dateRange?.to ? toUtcEndOfLocalDay(dateRange.to) : undefined;
  const calendarLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const auditLogsQuery = useQuery({
    queryKey: [
      "audit-logs",
      "list",
      search,
      selectedActions,
      selectedEntityNames,
      from,
      to,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getAuditLogs({
        search: search.trim() || undefined,
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

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("auditLogs:title")}
        description={t("auditLogs:description")}
      />

      <SectionCard title={t("auditLogs:sections.listTitle")}>
        <div className="space-y-4">
          <DataToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("auditLogs:filters.searchPlaceholder")}
            actions={
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
            }
          >
            <MultiSelectFilter
              placeholder={t("auditLogs:filters.actionFilterPlaceholder")}
              options={actionOptions}
              selectedValues={selectedActions}
              onChange={handleActionFilterChange}
              clearLabel={t("auditLogs:filters.clearSelection")}
              emptyLabel={t("auditLogs:filters.noOptions")}
              searchPlaceholder={t("auditLogs:filters.searchOptions")}
            />
            <MultiSelectFilter
              placeholder={t("auditLogs:filters.entityNameFilterPlaceholder")}
              options={entityNameOptions}
              selectedValues={selectedEntityNames}
              onChange={handleEntityNameFilterChange}
              clearLabel={t("auditLogs:filters.clearSelection")}
              emptyLabel={t("auditLogs:filters.noOptions")}
              searchPlaceholder={t("auditLogs:filters.searchOptions")}
            />
            <DateRangePicker
              value={dateRange}
              onChange={handleDateRangeChange}
              placeholder={t("auditLogs:filters.dateRangePlaceholder")}
              clearLabel={t("auditLogs:filters.clearDateRange")}
              locale={calendarLocale}
            />
          </DataToolbar>

          {auditLogsQuery.isLoading ? <LoadingState /> : null}

          {auditLogsQuery.isError ? (
            <ErrorState
              title={t("auditLogs:errors.loadFailed")}
              description={getApiErrorMessage(
                auditLogsQuery.error,
                t("auditLogs:errors.loadFailed"),
              )}
              retry={
                <Button variant="outline" onClick={handleRefresh}>
                  {t("common:actions.refresh")}
                </Button>
              }
            />
          ) : null}

          {auditLogsQuery.isSuccess && !auditLogs.length ? (
            <EmptyState
              title={t("auditLogs:empty.title")}
              description={t("auditLogs:empty.description")}
            />
          ) : null}

          {auditLogs.length ? (
            <div className="overflow-x-auto rounded-lg border bg-card">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/50 text-left">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.createdAt")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.action")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.entityName")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.entityId")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.description")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.actorUserName")}</th>
                    <th className="px-3 py-2 font-medium">{t("auditLogs:table.ipAddress")}</th>
                  </tr>
                </thead>
                <tbody>
                  {auditLogs.map((logItem) => (
                    <tr key={logItem.id} className="border-t align-top hover:bg-muted/20">
                      <td className="whitespace-nowrap px-3 py-2">
                        <DateTimeText value={logItem.createdAt} />
                      </td>
                      <td className="px-3 py-2">
                        <Badge variant="secondary">{logItem.action}</Badge>
                      </td>
                      <td className="px-3 py-2">
                        <CodeBadge>{logItem.entityName}</CodeBadge>
                      </td>
                      <td className="px-3 py-2">{logItem.entityId || "-"}</td>
                      <td className="max-w-96 px-3 py-2">
                        <span className="line-clamp-2">{logItem.description || "-"}</span>
                      </td>
                      <td className="px-3 py-2">{logItem.actorUserName || "-"}</td>
                      <td className="px-3 py-2">{logItem.ipAddress || "-"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {auditLogsQuery.data && auditLogsQuery.data.totalCount > 0 ? (
                <TablePagination
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
              ) : null}
            </div>
          ) : null}
        </div>
      </SectionCard>
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
