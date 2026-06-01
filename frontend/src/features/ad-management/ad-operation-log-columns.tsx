import type { ColumnDef } from "@tanstack/react-table";
import type { ReactNode } from "react";
import type { TFunction } from "i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import type { BadgeVariants } from "@/components/ui/badge-variants";
import { Button } from "@/components/ui/button";
import {
  getAdOperationErrorSummary,
  parseAdOperationErrorMessage,
} from "@/features/ad-management/parse-ad-operation-error-message";
import type { AdOperationLogListItem } from "@/features/ad-management/operation-logs-types";

type StatusBadgeVariant = NonNullable<BadgeVariants["variant"]>;

function renderOptionalText(value: string | null | undefined): ReactNode {
  if (!value) {
    return <span className="text-muted-foreground">-</span>;
  }
  return value;
}

function renderTarget(item: AdOperationLogListItem): ReactNode {
  const primary = item.targetSamAccountName ?? item.targetObjectGuid;
  if (!primary) {
    return renderOptionalText(null);
  }

  const title = item.targetDistinguishedName ?? undefined;
  return (
    <span className="truncate font-mono text-xs" title={title}>
      {primary}
    </span>
  );
}

export function getAdOperationStatusBadgeVariant(
  status: string,
  changeStatus: string | null,
): StatusBadgeVariant {
  if (changeStatus === "NoChangesDetected") {
    return "secondary";
  }

  const normalized = status.toLowerCase();
  if (normalized === "failed") {
    return "destructive";
  }
  if (normalized === "succeeded") {
    return "success";
  }
  return "outline";
}

type CreateAdOperationLogColumnsOptions = {
  t: TFunction;
  getOperationLabel: (operationType: string) => string;
  getStatusLabel: (status: string, changeStatus: string | null) => string;
  onDetail: (item: AdOperationLogListItem) => void;
};

export function createAdOperationLogColumns({
  t,
  getOperationLabel,
  getStatusLabel,
  onDetail,
}: CreateAdOperationLogColumnsOptions): ColumnDef<AdOperationLogListItem, unknown>[] {
  return [
    {
      id: "createdAt",
      header: () => t("adOperationLogs:table.createdAt"),
      cell: ({ row }) => <DateTimeText value={row.original.createdAt} />,
    },
    {
      id: "operationType",
      header: () => t("adOperationLogs:table.operation"),
      cell: ({ row }) => (
        <Badge variant="outline">{getOperationLabel(row.original.operationType)}</Badge>
      ),
    },
    {
      id: "status",
      header: () => t("adOperationLogs:table.status"),
      cell: ({ row }) => (
        <Badge variant={getAdOperationStatusBadgeVariant(row.original.status, null)}>
          {getStatusLabel(row.original.status, null)}
        </Badge>
      ),
    },
    {
      id: "target",
      header: () => t("adOperationLogs:table.target"),
      cell: ({ row }) => renderTarget(row.original),
      meta: { truncate: true, cellClassName: "max-w-[12rem]" } satisfies DataTableColumnMeta,
    },
    {
      id: "actorUserName",
      header: () => t("adOperationLogs:table.actor"),
      cell: ({ row }) => renderOptionalText(row.original.actorUserName),
    },
    {
      id: "domainController",
      header: () => t("adOperationLogs:table.domainController"),
      cell: ({ row }) => (
        <span className="truncate font-mono text-xs text-muted-foreground" title={row.original.domainController ?? undefined}>
          {row.original.domainController ?? "-"}
        </span>
      ),
      meta: { truncate: true, mono: true, cellClassName: "max-w-[10rem]" } satisfies DataTableColumnMeta,
    },
    {
      id: "errorSummary",
      header: () => t("adOperationLogs:table.errorSummary"),
      cell: ({ row }) => {
        if (!row.original.hasError && !row.original.errorMessage) {
          return renderOptionalText(null);
        }

        const summary = getAdOperationErrorSummary(
          parseAdOperationErrorMessage(row.original.errorMessage),
        );
        if (!summary) {
          return renderOptionalText(null);
        }

        return (
          <span className="line-clamp-2 text-xs text-muted-foreground" title={summary}>
            {summary}
          </span>
        );
      },
    },
    {
      id: "actions",
      header: () => t("adOperationLogs:table.actions"),
      cell: ({ row }) => (
        <div className="text-right">
          <Button type="button" variant="ghost" size="sm" onClick={() => onDetail(row.original)}>
            {t("adOperationLogs:actions.detail")}
          </Button>
        </div>
      ),
      meta: { isAction: true } satisfies DataTableColumnMeta,
    },
  ];
}
