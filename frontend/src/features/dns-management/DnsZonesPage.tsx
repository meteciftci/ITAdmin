import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { DataTable, DataTablePagination, DataTableToolbar } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { FormError } from "@/components/common/FormError";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { DNS_INVENTORY_SERVERS_QUERY_KEY, DNS_ZONES_QUERY_KEY, getDnsInventoryServers, getDnsZones } from "./api";
import { createDnsZoneColumns } from "./dns-inventory-columns";
import { buildDnsZoneRecordsPath } from "./dns-inventory-paths";

export function DnsZonesPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const canViewRecords = canAccess(user, PermissionCodes.DnsManagement.Records.View);
  const [serverId, setServerId] = useState("");
  const [search, setSearch] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const effectiveSearch = useDebouncedValue(search, 350).trim() || undefined;

  const inventory = useQuery({
    queryKey: DNS_INVENTORY_SERVERS_QUERY_KEY,
    queryFn: getDnsInventoryServers,
  });
  const zones = useQuery({
    queryKey: [...DNS_ZONES_QUERY_KEY, serverId, effectiveSearch, pageNumber, pageSize],
    queryFn: () => getDnsZones({ serverId: serverId || undefined, search: effectiveSearch, pageNumber, pageSize }),
  });
  const columns = useMemo(() => createDnsZoneColumns({
    t,
    canViewRecords,
    onViewRecords: (zone) => navigate(buildDnsZoneRecordsPath(zone.id)),
  }), [t, canViewRecords, navigate]);
  const table = useServerDataTable({
    data: zones.data?.items ?? [],
    columns,
    pageCount: zones.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });
  const selectedServer = inventory.data?.find((x) => x.serverId === serverId);

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("inventory.title")}
        description={t("inventory.description")}
        actions={<Button variant="outline" disabled={inventory.isFetching || zones.isFetching} onClick={() => { inventory.refetch(); zones.refetch(); }}>{t("common:actions.refresh")}</Button>}
      />

      <SectionCard title={t("inventory.freshnessTitle")} description={t("inventory.freshnessDescription")}>
        {inventory.isLoading ? <LoadingState /> : null}
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {inventory.data?.map((server) => (
            <button
              type="button"
              key={server.serverId}
              aria-pressed={server.serverId === serverId}
              onClick={() => { setServerId(server.serverId === serverId ? "" : server.serverId); setPageNumber(1); }}
              className={`rounded-lg border p-4 text-left transition-colors hover:bg-muted/60 ${server.serverId === serverId ? "border-primary bg-primary/5" : ""}`}
            >
              <div className="flex items-start justify-between gap-2">
                <span className="font-medium">{server.serverDisplayName}</span>
                <Badge variant={!server.isAvailable ? "destructive" : server.isStale ? "secondary" : "default"}>
                  {t(!server.isAvailable ? "inventory.unavailable" : server.isStale ? "inventory.stale" : "inventory.current")}
                </Badge>
              </div>
              <p className="mt-2 text-xs text-muted-foreground">
                {server.snapshotCompletedAt ? <DateTimeText value={server.snapshotCompletedAt} /> : t("inventory.noSnapshot")}
              </p>
              <p className="mt-1 text-xs text-muted-foreground">{t("inventory.counts", { zones: server.zoneCount, records: server.recordCount })}</p>
            </button>
          ))}
          {inventory.isSuccess && inventory.data.length === 0 ? <p className="text-sm text-muted-foreground">{t("inventory.noServers")}</p> : null}
        </div>
      </SectionCard>

      <SectionCard title={t("inventory.zoneListTitle")} description={selectedServer ? t("inventory.filteredServer", { server: selectedServer.serverDisplayName }) : t("inventory.allServers")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => { setSearch(value); setPageNumber(1); }}
            searchPlaceholder={t("inventory.searchZones")}
            activeFilterCount={serverId ? 1 : 0}
            onClearFilters={() => { setServerId(""); setPageNumber(1); }}
            filterContent={<div className="space-y-2"><label className="text-sm font-medium" htmlFor="dns-zone-server-filter">{t("inventory.fields.server")}</label><Select id="dns-zone-server-filter" value={serverId} onChange={(event) => { setServerId(event.target.value); setPageNumber(1); }}><option value="">{t("inventory.allServers")}</option>{inventory.data?.map((server) => <option key={server.serverId} value={server.serverId}>{server.serverDisplayName}</option>)}</Select></div>}
          />
          <DataTable
            table={table}
            isLoading={zones.isLoading}
            emptyMessage={t("inventory.emptyZones")}
            emptyDescription={t("inventory.emptyZonesDescription")}
            footer={zones.data ? <DataTablePagination mode="server" pageNumber={zones.data.pageNumber} pageSize={zones.data.pageSize} totalCount={zones.data.totalCount} totalPages={zones.data.totalPages} onPageChange={setPageNumber} onPageSizeChange={(value) => { setPageSize(value); setPageNumber(1); }} /> : null}
          />
          {inventory.isError || zones.isError ? <FormError message={t("inventory.loadFailed")} /> : null}
        </div>
      </SectionCard>
    </section>
  );
}
