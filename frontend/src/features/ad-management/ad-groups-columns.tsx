import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { RowActions } from "@/components/common/RowActions";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/dropdown-menu";
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
  canUpdateGroup: boolean;
  canDeleteGroup: boolean;
  onDetail: (group: AdGroupListItem) => void;
  onEdit: (group: AdGroupListItem) => void;
  onDelete: (group: AdGroupListItem) => void;
};

export function createAdGroupColumns({
  t,
  canUpdateGroup,
  canDeleteGroup,
  onDetail,
  onEdit,
  onDelete,
}: CreateAdGroupColumnsOptions): ColumnDef<AdGroupListItem, unknown>[] {
  return [
    {
      id: "group",
      header: () => t("adManagement:groups.table.group"),
      cell: ({ row }) => {
        const group = row.original;
        const primaryLabel = getAdGroupPrimaryLabel(group);
        const secondaryLabel = getAdGroupSecondaryLabel(group, primaryLabel);

        return (
          <div className="space-y-0.5" title={group.distinguishedName}>
            <p className="font-medium" title={group.distinguishedName}>
              {primaryLabel}
            </p>
            {secondaryLabel ? (
              <p
                className="truncate text-xs text-muted-foreground"
                title={secondaryLabel}
              >
                {secondaryLabel}
              </p>
            ) : null}
          </div>
        );
      },
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
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <Badge variant="secondary">
          {getAdGroupScopeLabel(t, row.original.groupScope)}
        </Badge>
      ),
    },
    {
      id: "type",
      header: () => t("adManagement:groups.table.type"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <Badge variant={row.original.securityEnabled ? "default" : "outline"}>
          {getAdGroupTypeLabel(t, row.original.securityEnabled)}
        </Badge>
      ),
    },
    {
      id: "actions",
      header: () => t("adManagement:groups.table.actions"),
      meta: { isAction: true, align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <RowActions>
          <DropdownMenuItem onClick={() => onDetail(row.original)}>
            {t("adManagement:groups.actions.detail")}
          </DropdownMenuItem>
          {canUpdateGroup ? (
            <DropdownMenuItem onClick={() => onEdit(row.original)}>
              {t("adManagement:groups.actions.edit")}
            </DropdownMenuItem>
          ) : null}
          {canDeleteGroup ? (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                className="text-destructive focus:text-destructive"
                onClick={() => onDelete(row.original)}
              >
                {t("adManagement:groups.actions.delete")}
              </DropdownMenuItem>
            </>
          ) : null}
        </RowActions>
      ),
    },
  ];
}
