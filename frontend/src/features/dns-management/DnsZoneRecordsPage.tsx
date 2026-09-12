import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { DataTable, DataTablePagination, DataTableToolbar } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { DateTimeText } from "@/components/common/DateTimeText";
import { FormError } from "@/components/common/FormError";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Select } from "@/components/ui/select";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useAuthStore } from "@/features/auth/auth-store";
import { getApiErrorMessage } from "@/lib/api-error";
import { PermissionCodes } from "@/lib/permission-codes";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { createDnsRecord, deleteDnsRecord, exportDnsRecords, getDnsRecords, getDnsZone, updateDnsRecord } from "./api";
import { createDnsRecordColumns } from "./dns-inventory-columns";
import { DNS_ZONES_PATH } from "./dns-inventory-paths";
import { saveDnsDownload } from "./download-dns-export";
import { DnsRecordDialog } from "./DnsRecordDialog";
import type { CreateDnsRecord, DnsRecordInventory, DnsRecordMutationInput } from "./types";

const recordTypes = ["A", "AAAA", "CNAME", "MX", "NS", "PTR", "SOA", "SRV", "TXT", "CAA"];

export function DnsZoneRecordsPage() {
  const { t } = useTranslation(["dnsManagement", "common"]);
  const navigate = useNavigate();
  const user = useAuthStore((x) => x.user);
  const canCreate = canAccess(user, PermissionCodes.DnsManagement.Records.Create);
  const canUpdate = canAccess(user, PermissionCodes.DnsManagement.Records.Update);
  const canDelete = canAccess(user, PermissionCodes.DnsManagement.Records.Delete);
  const canExport = canAccess(user, PermissionCodes.DnsManagement.Export);
  const queryClient = useQueryClient();
  const { zoneSnapshotId = "" } = useParams();
  const [search, setSearch] = useState("");
  const [recordType, setRecordType] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [editorOpen, setEditorOpen] = useState(false);
  const [recordToEdit, setRecordToEdit] = useState<DnsRecordInventory | null>(null);
  const [recordToDelete, setRecordToDelete] = useState<DnsRecordInventory | null>(null);
  const [mutationError, setMutationError] = useState<string | null>(null);
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
  const refreshInventory = async () => {
    await queryClient.invalidateQueries({ queryKey: ["dns-management", "inventory"] });
  };
  const createMutation = useMutation({
    mutationFn: (request: CreateDnsRecord) => createDnsRecord(zoneSnapshotId, request),
    onSuccess: async () => { setEditorOpen(false); setMutationError(null); await refreshInventory(); toast.success(t("records.created")); navigate(DNS_ZONES_PATH); },
    onError: (error) => setMutationError(getApiErrorMessage(error, t("records.operationFailed"))),
  });
  const updateMutation = useMutation({
    mutationFn: (request: DnsRecordMutationInput) => updateDnsRecord(zoneSnapshotId, recordToEdit!, request),
    onSuccess: async () => { setEditorOpen(false); setRecordToEdit(null); setMutationError(null); await refreshInventory(); toast.success(t("records.updated")); navigate(DNS_ZONES_PATH); },
    onError: (error) => setMutationError(getApiErrorMessage(error, t("records.operationFailed"))),
  });
  const removeMutation = useMutation({
    mutationFn: (record: DnsRecordInventory) => deleteDnsRecord(zoneSnapshotId, record),
    onSuccess: async () => { setRecordToDelete(null); setMutationError(null); await refreshInventory(); toast.success(t("records.deleted")); navigate(DNS_ZONES_PATH); },
    onError: (error) => { setMutationError(getApiErrorMessage(error, t("records.operationFailed"))); setRecordToDelete(null); },
  });
  const exportMutation = useMutation({
    mutationFn: () => exportDnsRecords(zoneSnapshotId, { search: effectiveSearch, recordType: recordType || undefined }),
    onSuccess: (download) => { saveDnsDownload(download); setMutationError(null); toast.success(t("export.completed")); },
    onError: (error) => setMutationError(getApiErrorMessage(error, t("export.failed"))),
  });
  const columns = useMemo(() => createDnsRecordColumns({
    t, canUpdate, canDelete,
    onEdit: (record) => { setRecordToEdit(record); setMutationError(null); setEditorOpen(true); },
    onDelete: (record) => { setMutationError(null); setRecordToDelete(record); },
  }), [t, canUpdate, canDelete]);
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
        actions={<div className="flex gap-2">{canExport && zone.data ? <Button variant="outline" disabled={exportMutation.isPending} onClick={() => exportMutation.mutate()}>{exportMutation.isPending ? t("export.preparing") : t("export.csv")}</Button> : null}{canCreate && zone.data ? <Button onClick={() => { setRecordToEdit(null); setMutationError(null); setEditorOpen(true); }}>{t("records.createAction")}</Button> : null}<Link to={DNS_ZONES_PATH} className={cn(buttonVariants({ variant: "outline" }))}>{t("inventory.backToZones")}</Link></div>}
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
          {mutationError && !editorOpen ? <FormError message={mutationError} /> : null}
        </div>
      </SectionCard>
      {zone.data && editorOpen ? <DnsRecordDialog open zone={zone.data} record={recordToEdit} isLoading={createMutation.isPending || updateMutation.isPending} error={mutationError} onOpenChange={(open) => { setEditorOpen(open); if (!open) { setRecordToEdit(null); setMutationError(null); } }} onSubmit={(request) => { if (recordToEdit) updateMutation.mutate(request as DnsRecordMutationInput); else createMutation.mutate(request as CreateDnsRecord); }} /> : null}
      <ConfirmDialog open={recordToDelete !== null} title={t("records.deleteTitle")} description={t("records.deleteDescription", { name: recordToDelete?.fullyQualifiedName, type: recordToDelete?.recordType })} confirmText={t("common:actions.delete")} cancelText={t("common:actions.cancel")} variant="danger" isLoading={removeMutation.isPending} onOpenChange={(open) => { if (!open) setRecordToDelete(null); }} onConfirm={() => { if (recordToDelete) removeMutation.mutate(recordToDelete); }} />
    </section>
  );
}
