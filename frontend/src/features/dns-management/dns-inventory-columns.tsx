import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { DateTimeText } from "@/components/common/DateTimeText";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatDnsRecordValue } from "./dns-record-value";
import type { DnsRecordInventory, DnsZoneInventory } from "./types";

export const createDnsZoneColumns = ({
  t,
  canViewRecords,
  canUpdate = false,
  canDelete = false,
  onViewRecords,
  onEdit,
  onDelete,
}: {
  t: TFunction;
  canViewRecords: boolean;
  canUpdate?: boolean;
  canDelete?: boolean;
  onViewRecords: (zone: DnsZoneInventory) => void;
  onEdit?: (zone: DnsZoneInventory) => void;
  onDelete?: (zone: DnsZoneInventory) => void;
}): ColumnDef<DnsZoneInventory, unknown>[] => {
  const columns: ColumnDef<DnsZoneInventory, unknown>[] = [
    {
      accessorKey: "name",
      header: () => t("dnsManagement:inventory.fields.zone"),
      cell: ({ row }) => <CodeBadge>{row.original.name}</CodeBadge>,
    },
    {
      accessorKey: "serverDisplayName",
      header: () => t("dnsManagement:inventory.fields.server"),
      cell: ({ row }) => row.original.serverDisplayName,
    },
    {
      accessorKey: "zoneType",
      header: () => t("dnsManagement:inventory.fields.zoneType"),
      cell: ({ row }) => <Badge variant="outline">{row.original.zoneType}</Badge>,
    },
    {
      accessorKey: "recordCount",
      header: () => t("dnsManagement:inventory.fields.recordCount"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
    },
    {
      id: "scopes",
      header: () => t("dnsManagement:inventory.fields.scope"),
      cell: ({ row }) => row.original.zoneScopes.length > 0
        ? row.original.zoneScopes.join(", ")
        : t("dnsManagement:inventory.defaultScope"),
    },
    {
      id: "features",
      header: () => t("dnsManagement:inventory.fields.features"),
      cell: ({ row }) => (
        <div className="flex flex-wrap gap-1">
          {row.original.isDsIntegrated ? <Badge variant="secondary">AD</Badge> : null}
          {row.original.isSigned ? <Badge variant="secondary">DNSSEC</Badge> : null}
          {row.original.isReverseLookupZone ? <Badge variant="outline">PTR</Badge> : null}
          {row.original.isPaused ? <Badge variant="destructive">{t("dnsManagement:inventory.paused")}</Badge> : null}
        </div>
      ),
    },
    {
      accessorKey: "snapshotCompletedAt",
      header: () => t("dnsManagement:inventory.fields.snapshotTime"),
      cell: ({ row }) => <DateTimeText value={row.original.snapshotCompletedAt} />,
    },
  ];
  if (canViewRecords || canUpdate || canDelete) {
    columns.push({
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const mutable = !row.original.isAutoCreated && !row.original.virtualizationInstance
          && row.original.name !== "." && row.original.name.toLowerCase() !== "trustanchors"
          && ["Primary", "Secondary", "Stub", "Forwarder"].includes(row.original.zoneType);
        return <div className="flex justify-end gap-2">
          {canViewRecords ? <Button variant="outline" size="sm" onClick={() => onViewRecords(row.original)}>{t("dnsManagement:inventory.viewRecords")}</Button> : null}
          {canUpdate && onEdit && mutable ? <Button variant="outline" size="sm" onClick={() => onEdit(row.original)}>{t("common:actions.edit")}</Button> : null}
          {canDelete && onDelete && mutable && !row.original.isSigned ? <Button variant="destructive" size="sm" onClick={() => onDelete(row.original)}>{t("common:actions.delete")}</Button> : null}
        </div>;
      },
    });
  }
  return columns;
};

export const createDnsRecordColumns = ({ t, canUpdate = false, canDelete = false, onEdit, onDelete }: {
  t: TFunction;
  canUpdate?: boolean;
  canDelete?: boolean;
  onEdit?: (record: DnsRecordInventory) => void;
  onDelete?: (record: DnsRecordInventory) => void;
}): ColumnDef<DnsRecordInventory, unknown>[] => {
  const writableTypes = new Set(["A", "AAAA", "CNAME", "MX", "NS", "PTR", "SRV", "TXT"]);
  const columns: ColumnDef<DnsRecordInventory, unknown>[] = [
  {
    accessorKey: "relativeName",
    header: () => t("dnsManagement:inventory.fields.owner"),
    cell: ({ row }) => <CodeBadge>{row.original.relativeName}</CodeBadge>,
  },
  {
    accessorKey: "recordType",
    header: () => t("dnsManagement:inventory.fields.recordType"),
    cell: ({ row }) => <Badge variant="outline">{row.original.recordType}</Badge>,
  },
  {
    accessorKey: "canonicalValue",
    header: () => t("dnsManagement:inventory.fields.value"),
    meta: { cellClassName: "max-w-[32rem]" } satisfies DataTableColumnMeta,
    cell: ({ row }) => (
      <span className="block break-words font-mono text-xs" title={row.original.canonicalValue}>
        {formatDnsRecordValue(row.original.canonicalValue)}
      </span>
    ),
  },
  {
    accessorKey: "timeToLiveSeconds",
    header: () => t("dnsManagement:inventory.fields.ttl"),
    meta: { align: "right" } satisfies DataTableColumnMeta,
  },
  {
    id: "scope",
    header: () => t("dnsManagement:inventory.fields.scope"),
    cell: ({ row }) => row.original.zoneScope ?? t("dnsManagement:inventory.defaultScope"),
  },
  {
    accessorKey: "timestamp",
    header: () => t("dnsManagement:inventory.fields.timestamp"),
    cell: ({ row }) => <DateTimeText value={row.original.timestamp} />,
  },
  ];
  if (canUpdate || canDelete) {
    columns.push({
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => <div className="flex justify-end gap-2">
        {canUpdate && onEdit && writableTypes.has(row.original.recordType) ? <Button variant="outline" size="sm" onClick={() => onEdit(row.original)}>{t("common:actions.edit")}</Button> : null}
        {canDelete && onDelete && writableTypes.has(row.original.recordType) ? <Button variant="destructive" size="sm" onClick={() => onDelete(row.original)}>{t("common:actions.delete")}</Button> : null}
      </div>,
    });
  }
  return columns;
};
