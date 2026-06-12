import type { ColumnDef } from "@tanstack/react-table";
import type { ReactNode } from "react";
import type { TFunction } from "i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { DateTimeText } from "@/components/common/DateTimeText";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import type { BadgeVariants } from "@/components/ui/badge-variants";
import { Button } from "@/components/ui/button";
import type { AuditLogListItem } from "@/features/audit-logs/types";

type AuditActionBadgeVariant = NonNullable<BadgeVariants["variant"]>;

function renderOptionalText(value: string | null | undefined): ReactNode {
  if (!value) {
    return <span className="text-muted-foreground">-</span>;
  }
  return value;
}

export function getAuditActionBadgeVariant(
  action: string | null | undefined,
): AuditActionBadgeVariant {
  if (!action) {
    return "secondary";
  }
  const normalized = action.toLowerCase();
  if (
    normalized.startsWith("delete") ||
    normalized.startsWith("remove") ||
    normalized.startsWith("revoke") ||
    normalized.startsWith("disable") ||
    normalized.startsWith("unassign")
  ) {
    return "destructive";
  }
  if (
    normalized.startsWith("create") ||
    normalized.startsWith("add") ||
    normalized.startsWith("assign") ||
    normalized.startsWith("enable")
  ) {
    return "success";
  }
  if (normalized.startsWith("login") || normalized.startsWith("logout")) {
    return "outline";
  }
  return "secondary";
}

type CreateAuditLogColumnsOptions = {
  t: TFunction;
  onDetail: (item: AuditLogListItem) => void;
};

export function createAuditLogColumns({
  t,
  onDetail,
}: CreateAuditLogColumnsOptions): ColumnDef<AuditLogListItem, unknown>[] {
  return [
    {
      id: "createdAt",
      header: () => t("auditLogs:table.createdAt"),
      cell: ({ row }) => <DateTimeText value={row.original.createdAt} />,
    },
    {
      id: "action",
      header: () => t("auditLogs:table.action"),
      cell: ({ row }) => (
        <Badge variant={getAuditActionBadgeVariant(row.original.action)}>
          {row.original.action}
        </Badge>
      ),
    },
    {
      id: "entityName",
      header: () => t("auditLogs:table.entityName"),
      cell: ({ row }) => <CodeBadge>{row.original.entityName}</CodeBadge>,
    },
    {
      id: "entityId",
      header: () => t("auditLogs:table.entityId"),
      meta: { mono: true, truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) =>
        row.original.entityId ? (
          <span className="block truncate" title={row.original.entityId}>
            {row.original.entityId}
          </span>
        ) : (
          renderOptionalText(row.original.entityId)
        ),
    },
    {
      id: "description",
      header: () => t("auditLogs:table.description"),
      cell: ({ row }) =>
        row.original.description ? (
          <span className="line-clamp-2 text-foreground" title={row.original.description}>
            {row.original.description}
          </span>
        ) : (
          renderOptionalText(row.original.description)
        ),
    },
    {
      id: "actorUserName",
      header: () => t("auditLogs:table.actorUserName"),
      cell: ({ row }) => renderOptionalText(row.original.actorUserName),
    },
    {
      id: "ipAddress",
      header: () => t("auditLogs:table.ipAddress"),
      cell: ({ row }) => renderOptionalText(row.original.ipAddress),
    },
    {
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <Button type="button" variant="outline" size="sm" onClick={() => onDetail(row.original)}>
          {t("common:actions.detail")}
        </Button>
      ),
    },
  ];
}
