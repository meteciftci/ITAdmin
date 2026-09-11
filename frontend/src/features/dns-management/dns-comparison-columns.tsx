import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import { formatDnsRecordValue } from "./dns-record-value";
import type { DnsComparisonRow, DnsComparisonStatus, DnsInventoryServer } from "./types";

const statusVariant = (status: DnsComparisonStatus): "default" | "secondary" | "destructive" | "outline" => {
  if (status === "Equal") return "default";
  if (status === "Different" || status === "Missing") return "destructive";
  return "secondary";
};

export const createDnsComparisonColumns = ({
  t,
  servers,
  compareTimeToLive,
}: {
  t: TFunction;
  servers: DnsInventoryServer[];
  compareTimeToLive: boolean;
}): ColumnDef<DnsComparisonRow, unknown>[] => [
  {
    id: "record",
    header: () => t("dnsManagement:comparison.fields.record"),
    meta: { cellClassName: "min-w-64" } satisfies DataTableColumnMeta,
    cell: ({ row }) => (
      <div className="space-y-1">
        <CodeBadge>{row.original.relativeName === "@" ? row.original.zoneName : `${row.original.relativeName}.${row.original.zoneName}`}</CodeBadge>
        <div className="flex flex-wrap gap-1">
          <Badge variant="outline">{row.original.recordType}</Badge>
          {row.original.zoneScope ? <Badge variant="secondary">{row.original.zoneScope}</Badge> : null}
          {row.original.virtualizationInstance ? <Badge variant="secondary">{row.original.virtualizationInstance}</Badge> : null}
        </div>
      </div>
    ),
  },
  ...servers.map((server): ColumnDef<DnsComparisonRow, unknown> => ({
    id: server.serverId,
    header: () => server.serverDisplayName,
    meta: { cellClassName: "min-w-60 align-top" } satisfies DataTableColumnMeta,
    cell: ({ row }) => {
      const cell = row.original.cells.find((item) => item.serverId === server.serverId);
      if (!cell) return null;
      return (
        <div className="space-y-2">
          <Badge variant={statusVariant(cell.status)}>{t(`dnsManagement:comparison.status.${cell.status}`)}</Badge>
          {cell.values.length ? (
            <div className="space-y-1 font-mono text-xs">
              {cell.values.map((value) => <div key={value} className="break-words">{formatDnsRecordValue(value)}</div>)}
            </div>
          ) : <p className="text-xs text-muted-foreground">{t("dnsManagement:comparison.noValue")}</p>}
          {compareTimeToLive && cell.timeToLiveValues.length ? (
            <p className="text-xs text-muted-foreground">
              {t("dnsManagement:comparison.ttlValues", { values: cell.timeToLiveValues.join(", ") })}
            </p>
          ) : null}
        </div>
      );
    },
  })),
];
