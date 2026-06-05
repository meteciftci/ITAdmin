import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { RowActions } from "@/components/common/RowActions";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import {
  getAdGroupPrimaryLabel,
  getAdGroupSecondaryLabel,
} from "@/features/ad-management/ad-group-display-labels";
import {
  getAdGroupScopeLabel,
  getAdGroupTypeLabel,
} from "@/features/ad-management/ad-group-labels";
import type { AdGroupListItem } from "@/features/ad-management/types";

type CreateAdGroupColumnsOptions = {
  t: TFunction;
  onDetail: (group: AdGroupListItem) => void;
};

export function createAdGroupColumns({
  t,
  onDetail,
}: CreateAdGroupColumnsOptions): ColumnDef<AdGroupListItem, unknown>[] {
  return [
    {
      id: "primaryLabel",
      header: () => t("adManagement:groups.table.displayName"),
      cell: ({ row }) => {
        const group = row.original;
        const primaryLabel = getAdGroupPrimaryLabel(group);
        const secondaryLabel = getAdGroupSecondaryLabel(group, primaryLabel);

        return (
          <div className="space-y-0.5">
            <p className="font-medium">{primaryLabel}</p>
            {secondaryLabel ? (
              <p className="text-xs text-muted-foreground">{secondaryLabel}</p>
            ) : null}
          </div>
        );
      },
    },
    {
      accessorKey: "name",
      header: () => t("adManagement:groups.table.name"),
      cell: ({ row }) => row.original.name || "-",
    },
    {
      accessorKey: "cn",
      header: () => t("adManagement:groups.table.cn"),
      cell: ({ row }) => row.original.cn || "-",
    },
    {
      accessorKey: "samAccountName",
      header: () => t("adManagement:groups.table.samAccountName"),
      cell: ({ row }) => row.original.samAccountName || "-",
    },
    {
      accessorKey: "description",
      header: () => t("adManagement:groups.table.description"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.description || "-",
    },
    {
      id: "scope",
      header: () => t("adManagement:groups.table.scope"),
      cell: ({ row }) => (
        <Badge variant="secondary">
          {getAdGroupScopeLabel(t, row.original.groupScope)}
        </Badge>
      ),
    },
    {
      id: "type",
      header: () => t("adManagement:groups.table.type"),
      cell: ({ row }) => (
        <Badge variant={row.original.securityEnabled ? "default" : "outline"}>
          {getAdGroupTypeLabel(t, row.original.securityEnabled)}
        </Badge>
      ),
    },
    {
      accessorKey: "distinguishedName",
      header: () => t("adManagement:groups.table.distinguishedName"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <span className="break-all font-mono text-xs text-muted-foreground">
          {row.original.distinguishedName}
        </span>
      ),
    },
    {
      id: "actions",
      header: () => t("adManagement:groups.table.actions"),
      cell: ({ row }) => (
        <RowActions>
          <DropdownMenuItem onClick={() => onDetail(row.original)}>
            {t("adManagement:groups.actions.detail")}
          </DropdownMenuItem>
        </RowActions>
      ),
    },
  ];
}
