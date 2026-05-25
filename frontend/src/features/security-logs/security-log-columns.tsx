import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { SecurityLogListItem } from "@/features/security-logs/types";

export function getSeverityBadgeVariant(
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

  if (
    normalizedSeverity === "error" ||
    normalizedSeverity === "critical" ||
    normalizedSeverity === "high"
  ) {
    return "destructive";
  }

  if (normalizedSeverity === "success") {
    return "success";
  }

  return "secondary";
}

type CreateSecurityLogColumnsOptions = {
  t: TFunction;
  onDetail: (item: SecurityLogListItem) => void;
};

export function createSecurityLogColumns({
  t,
  onDetail,
}: CreateSecurityLogColumnsOptions): ColumnDef<SecurityLogListItem, unknown>[] {
  return [
    {
      id: "createdAt",
      header: () => t("securityLogs:table.createdAt"),
      cell: ({ row }) => <DateTimeText value={row.original.createdAt} />,
    },
    {
      accessorKey: "eventType",
      header: () => t("securityLogs:table.eventType"),
    },
    {
      id: "severity",
      header: () => t("securityLogs:table.severity"),
      cell: ({ row }) => (
        <Badge variant={getSeverityBadgeVariant(row.original.severity)}>
          {row.original.severity}
        </Badge>
      ),
    },
    {
      accessorKey: "userName",
      header: () => t("securityLogs:table.userName"),
      cell: ({ row }) => row.original.userName || "-",
    },
    {
      accessorKey: "ipAddress",
      header: () => t("securityLogs:table.ipAddress"),
      cell: ({ row }) => row.original.ipAddress || "-",
    },
    {
      accessorKey: "description",
      header: () => t("securityLogs:table.description"),
      cell: ({ row }) => (
        <span className="line-clamp-2">{row.original.description || "-"}</span>
      ),
    },
    {
      id: "actions",
      header: () => t("securityLogs:table.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <Button type="button" variant="outline" size="sm" onClick={() => onDetail(row.original)}>
          {t("securityLogs:actions.detail")}
        </Button>
      ),
    },
  ];
}
