import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { DataTable, DataTablePagination, DataTableToolbar } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { DateTimeText } from "@/components/common/DateTimeText";
import { FormError } from "@/components/common/FormError";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button-variants";
import { Select } from "@/components/ui/select";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";
import { getDnsRecords, getDnsZone } from "./api";
import { createDnsRecordColumns } from "./dns-inventory-columns";
import { DNS_ZONES_PATH } from "./dns-inventory-paths";

const recordTypes = ["A", "AAAA", "CNAME", "MX", "NS", "PTR", "SOA", "SRV", "TXT", "CAA"];

export function DnsZoneRecordsPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const { zoneSnapshotId = "" } = useParams();
  const [search, setSearch] = useState("");
  const [recordType, setRecordType] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const effectiveSearch = useDebouncedValue(search, 350).trim() || undefined;
  const zone = useQuery({
    queryKey: ["dns-management", "inventory", "zone", zoneSnapshotId],
    queryFn: () => getDnsZone(zoneSnapshotId),
    enabled: Boolean(zoneSnapshotId),
  });
  const records = useQuery({
    queryKey: ["dns-management", "inventory", "records", zoneSnapshotId, effectiveSearch, recordType, pageNumber, pageSize],
    queryFn: () => getDnsRecords(zoneSnapshotId, { search: effectiveSearch, recordType: recordType || undefined, pageNumber, pageSize }),
    enabled: Boolean(zoneSnapshotId),
  });
  const columns = useMemo(() => createDnsRecordColumns({ t }), [t]);
  const table = useServerDataTable({
    data: records.data?.items ?? [],
    columns,
    pageCount: records.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  return (
    <section className="space-y-4">
      <PageHeader
        title={zone.data ? t("inventory.recordsTitle", { zone: zone.data.name }) : t("inventory.records")}
        description={zone.data ? t("inventory.recordsDescription", { server: zone.data.serverDisplayName }) : t("inventory.recordsLoading")}
        actions={<Link to={DNS_ZONES_PATH} className={cn(buttonVariants({ variant: "outline" }))}>{t("inventory.backToZones")}</Link>}
      />
      {zone.data ? <SectionCard title={t("inventory.zoneSummary")}>
        <div className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <div><p className="text-muted-foreground">{t("inventory.fields.server")}</p><p className="font-medium">{zone.data.serverDisplayName}</p></div>
          <div><p className="text-muted-foreground">{t("inventory.fields.zoneType")}</p><p><Badge variant="outline">{zone.data.zoneType}</Badge></p></div>
          <div><p className="text-muted-foreground">{t("inventory.fields.scope")}</p><p className="font-medium">{zone.data.zoneScopes.length ? zone.data.zoneScopes.join(", ") : t("inventory.defaultScope")}</p></div>
          <div><p className="text-muted-foreground">{t("inventory.fields.snapshotTime")}</p><p className="font-medium"><DateTimeText value={zone.data.snapshotCompletedAt} /></p></div>
        </div>
      </SectionCard> : null}
      <SectionCard title={t("inventory.recordListTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => { setSearch(value); setPageNumber(1); }}
            searchPlaceholder={t("inventory.searchRecords")}
            activeFilterCount={recordType ? 1 : 0}
            onClearFilters={() => { setRecordType(""); setPageNumber(1); }}
            filterContent={<div className="space-y-2"><label className="text-sm font-medium" htmlFor="dns-record-type-filter">{t("inventory.fields.recordType")}</label><Select id="dns-record-type-filter" value={recordType} onChange={(event) => { setRecordType(event.target.value); setPageNumber(1); }}><option value="">{t("inventory.allRecordTypes")}</option>{recordTypes.map((type) => <option key={type} value={type}>{type}</option>)}</Select></div>}
          />
          <DataTable
            table={table}
            isLoading={records.isLoading}
            emptyMessage={t("inventory.emptyRecords")}
            emptyDescription={t("inventory.emptyRecordsDescription")}
            footer={records.data ? <DataTablePagination mode="server" pageNumber={records.data.pageNumber} pageSize={records.data.pageSize} totalCount={records.data.totalCount} totalPages={records.data.totalPages} onPageChange={setPageNumber} onPageSizeChange={(value) => { setPageSize(value); setPageNumber(1); }} /> : null}
          />
          {zone.isError || records.isError ? <FormError message={t("inventory.loadFailed")} /> : null}
        </div>
      </SectionCard>
    </section>
  );
}
