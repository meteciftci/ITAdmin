import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Button } from "@/components/ui/button";
import { formatAdGroupTableDisplayName } from "@/features/ad-management/ad-group-display";
import type { AdComputerGroupMembershipItem } from "@/features/ad-management/types";

type CreateAdComputerGroupColumnsOptions = {
  t: TFunction;
  canRemoveGroup: boolean;
  isRemovePending: boolean;
  onRemove: (group: AdComputerGroupMembershipItem) => void;
};

export function createAdComputerGroupColumns({
  t,
  canRemoveGroup,
  isRemovePending,
  onRemove,
}: CreateAdComputerGroupColumnsOptions): ColumnDef<AdComputerGroupMembershipItem, unknown>[] {
  const columns: ColumnDef<AdComputerGroupMembershipItem, unknown>[] = [
    {
      id: "groupName",
      header: () => t("adManagement:computers.groups.table.groupName"),
      cell: ({ row }) => formatAdGroupTableDisplayName(row.original.displayName ?? row.original.name),
    },
    {
      accessorKey: "samAccountName",
      header: () => t("adManagement:computers.groups.table.samAccountName"),
      cell: ({ row }) => row.original.samAccountName || "-",
    },
    {
      accessorKey: "description",
      header: () => t("adManagement:computers.groups.table.description"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.description || "-",
    },
    {
      accessorKey: "distinguishedName",
      header: () => t("adManagement:computers.groups.table.distinguishedName"),
      meta: { mono: true, truncate: true, cellClassName: "max-w-[28rem]" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <span className="block break-all" title={row.original.distinguishedName}>
          {row.original.distinguishedName}
        </span>
      ),
    },
  ];

  if (canRemoveGroup) {
    columns.push({
      id: "actions",
      header: () => t("adManagement:computers.table.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <Button
          type="button"
          variant="destructive"
          size="sm"
          disabled={isRemovePending}
          onClick={() => onRemove(row.original)}
        >
          {t("adManagement:computers.groups.actions.removeFromGroup")}
        </Button>
      ),
    });
  }

  return columns;
}
