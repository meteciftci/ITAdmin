import { useMemo, useState } from "react";
import type { DateRange } from "react-day-picker";
import type { ColumnDef } from "@tanstack/react-table";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { DateRangePicker } from "@/components/common/DateRangePicker";
import { DateTimeText } from "@/components/common/DateTimeText";
import { DataTable, DataTablePagination, DataTableToolbar } from "@/components/common/data-table";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { FormError } from "@/components/common/FormError";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { getApiErrorMessage } from "@/lib/api-error";
import { DNS_OPERATION_LOGS_QUERY_KEY, getDnsOperationLog, getDnsOperationLogs } from "./api";
import { DnsOperationLogDetailDialog } from "./DnsOperationLogDetailDialog";
import type { DnsOperationLogListItem } from "./types";

const operations = ["InventorySynchronization", "ServerConnectionTest", "RecordCreate", "RecordUpdate", "RecordDelete", "ZoneCreate", "ZoneUpdate", "ZoneDelete", "ServerSettingsUpdate", "CacheClear", "PolicySaveClientSubnet", "PolicyDeleteClientSubnet", "PolicyCreateZoneScope", "PolicyDeleteZoneScope", "PolicySaveQueryPolicy", "PolicyDeleteQueryPolicy", "PolicySetQueryPolicyEnabled", "DnssecZoneSign", "DnssecZoneResign", "DnssecZoneUnsign", "DnssecKeyRollover", "DnssecValidationUpdate", "DnssecRootTrustAnchorRetrieve", "DnssecDsTrustAnchorAdd", "DnssecDnsKeyTrustAnchorAdd", "DnssecTrustAnchorRemove", "DnsScavengingServerUpdate", "DnsZoneAgingUpdate", "DnsScavengingStart", "DnsListeningAddressesUpdate", "DnsRootHintAdd", "DnsRootHintUpdate", "DnsRootHintRemove"];
const statuses = ["Pending", "Succeeded", "Failed"];

export function DnsOperationLogsPage() {
  const { t, i18n } = useTranslation(["dnsManagement", "common"]);
  const [search, setSearch] = useState("");
  const [actor, setActor] = useState("");
  const [operation, setOperation] = useState("");
  const [status, setStatus] = useState("");
  const [range, setRange] = useState<DateRange>();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const targetSearch = useDebouncedValue(search, 350).trim() || undefined;
  const actorSearch = useDebouncedValue(actor, 350).trim() || undefined;
  const dateFrom = range?.from ? startOfDay(range.from).toISOString() : undefined;
  const dateTo = range?.to ? endOfDay(range.to).toISOString() : undefined;
  const list = useQuery({
    queryKey: [...DNS_OPERATION_LOGS_QUERY_KEY, operation, status, targetSearch, actorSearch, dateFrom, dateTo, pageNumber, pageSize],
    queryFn: () => getDnsOperationLogs({ operationType: operation || undefined, status: status || undefined, targetSearch, actorUserName: actorSearch, dateFrom, dateTo, pageNumber, pageSize }),
  });
  const detail = useQuery({ queryKey: [...DNS_OPERATION_LOGS_QUERY_KEY, selectedId], queryFn: () => getDnsOperationLog(selectedId!), enabled: Boolean(selectedId) });
  const columns = useMemo<ColumnDef<DnsOperationLogListItem, unknown>[]>(() => [
    { id: "createdAt", header: () => t("operationLogs.fields.createdAt"), cell: ({ row }) => <DateTimeText value={row.original.createdAt} /> },
    { id: "operation", header: () => t("operationLogs.fields.operation"), cell: ({ row }) => <Badge variant="outline">{t(`operationLogs.operations.${row.original.operationType}`, { defaultValue: row.original.operationType })}</Badge> },
    { id: "status", header: () => t("operationLogs.fields.status"), cell: ({ row }) => <Badge variant={row.original.status === "Failed" ? "destructive" : row.original.status === "Succeeded" ? "success" : "secondary"}>{t(`operationLogs.status.${row.original.status}`, { defaultValue: row.original.status })}</Badge> },
    { id: "server", header: () => t("operationLogs.fields.server"), cell: ({ row }) => row.original.serverDisplayName ?? "-" },
    { id: "target", header: () => t("operationLogs.fields.target"), cell: ({ row }) => <span className="font-mono text-xs">{row.original.recordName ?? row.original.zoneName ?? "-"}</span>, meta: { truncate: true, cellClassName: "max-w-[14rem]" } satisfies DataTableColumnMeta },
    { id: "actor", header: () => t("operationLogs.fields.actor"), cell: ({ row }) => row.original.actorUserName ?? "-" },
    { id: "actions", header: () => t("common:fields.actions"), cell: ({ row }) => <div className="text-right"><Button variant="ghost" size="sm" onClick={() => setSelectedId(row.original.id)}>{t("common:actions.detail")}</Button></div>, meta: { isAction: true } satisfies DataTableColumnMeta },
  ], [t]);
  const table = useServerDataTable({ data: list.data?.items ?? [], columns, pageCount: list.data?.totalPages ?? 0, pageIndex: pageNumber - 1, pageSize });
  const activeFilterCount = [operation, status, actorSearch, dateFrom || dateTo].filter(Boolean).length;
  const clear = () => { setOperation(""); setStatus(""); setActor(""); setRange(undefined); setPageNumber(1); };

  return <section className="space-y-4">
    <PageHeader title={t("operationLogs.title")} description={t("operationLogs.description")} />
    <SectionCard title={t("operationLogs.listTitle")}>
      <div className="space-y-4">
        <DataTableToolbar searchValue={search} onSearchChange={(value) => { setSearch(value); setPageNumber(1); }} searchPlaceholder={t("operationLogs.searchPlaceholder")} activeFilterCount={activeFilterCount} onClearFilters={clear} filterContent={<div className="space-y-3">
          <Select value={operation} onChange={(event) => { setOperation(event.target.value); setPageNumber(1); }}><option value="">{t("operationLogs.allOperations")}</option>{operations.map((value) => <option key={value} value={value}>{t(`operationLogs.operations.${value}`)}</option>)}</Select>
          <Select value={status} onChange={(event) => { setStatus(event.target.value); setPageNumber(1); }}><option value="">{t("operationLogs.allStatuses")}</option>{statuses.map((value) => <option key={value} value={value}>{t(`operationLogs.status.${value}`)}</option>)}</Select>
          <Input value={actor} onChange={(event) => { setActor(event.target.value); setPageNumber(1); }} placeholder={t("operationLogs.actorPlaceholder")} />
          <DateRangePicker value={range} onChange={(value) => { setRange(value); setPageNumber(1); }} placeholder={t("common:dateRange.placeholder")} clearLabel={t("common:dateRange.clear")} locale={i18n.language.startsWith("tr") ? "tr" : "en"} />
        </div>} actions={<Button variant="outline" onClick={() => list.refetch()}>{t("common:actions.refresh")}</Button>} />
        <DataTable table={table} isLoading={list.isLoading} emptyMessage={t("operationLogs.empty")} emptyDescription={t("operationLogs.emptyDescription")} footer={list.data ? <DataTablePagination mode="server" pageNumber={list.data.pageNumber} pageSize={list.data.pageSize} totalCount={list.data.totalCount} totalPages={list.data.totalPages} onPageChange={setPageNumber} onPageSizeChange={(value) => { setPageSize(value); setPageNumber(1); }} /> : null} />
        {list.isError ? <FormError message={getApiErrorMessage(list.error, t("operationLogs.loadFailed"))} /> : null}
      </div>
    </SectionCard>
    <DnsOperationLogDetailDialog open={Boolean(selectedId)} detail={detail.data} isLoading={detail.isLoading} onOpenChange={(open) => { if (!open) setSelectedId(null); }} />
  </section>;
}

function startOfDay(value: Date) { return new Date(value.getFullYear(), value.getMonth(), value.getDate()); }
function endOfDay(value: Date) { return new Date(value.getFullYear(), value.getMonth(), value.getDate(), 23, 59, 59, 999); }
