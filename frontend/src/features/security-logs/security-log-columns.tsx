import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { DateTimeText } from "@/components/common/DateTimeText";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Button } from "@/components/ui/button";
import { SecuritySeverityBadge } from "@/features/security-logs/SecuritySeverityBadge";
import type { SecurityLogListItem } from "@/features/security-logs/types";

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
      cell: ({ row }) => <CodeBadge>{row.original.eventType}</CodeBadge>,
    },
    {
      id: "severity",
      header: () => t("securityLogs:table.severity"),
      cell: ({ row }) => <SecuritySeverityBadge severity={row.original.severity} t={t} />,
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
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => onDetail(row.original)}
          aria-label={t("securityLogs:actions.detailFor", {
            eventType: row.original.eventType,
          })}
        >
          {t("common:actions.detail")}
        </Button>
      ),
    },
  ];
}
