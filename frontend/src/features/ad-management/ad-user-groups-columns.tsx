import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Button } from "@/components/ui/button";
import { formatAdGroupTableDisplayName } from "@/features/ad-management/ad-group-display";
import type { AdUserGroupMembershipItem } from "@/features/ad-management/types";

type CreateAdUserGroupColumnsOptions = {
  t: TFunction;
  canRemoveGroup: boolean;
  isRemovePending: boolean;
  onRemove: (group: AdUserGroupMembershipItem) => void;
};

export function createAdUserGroupColumns({
  t,
  canRemoveGroup,
  isRemovePending,
  onRemove,
}: CreateAdUserGroupColumnsOptions): ColumnDef<AdUserGroupMembershipItem, unknown>[] {
  const columns: ColumnDef<AdUserGroupMembershipItem, unknown>[] = [
    {
      id: "groupName",
      header: () => t("adManagement:users.groups.table.groupName"),
      cell: ({ row }) => formatAdGroupTableDisplayName(row.original.displayName),
    },
    {
      accessorKey: "samAccountName",
      header: () => t("adManagement:users.groups.table.samAccountName"),
      cell: ({ row }) => row.original.samAccountName || "-",
    },
    {
      accessorKey: "description",
      header: () => t("adManagement:users.groups.table.description"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.description || "-",
    },
    {
      accessorKey: "distinguishedName",
      header: () => t("adManagement:users.groups.table.distinguishedName"),
      meta: { mono: true, truncate: true, cellClassName: "max-w-[28rem]" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <span className="block truncate" title={row.original.distinguishedName}>
          {row.original.distinguishedName}
        </span>
      ),
    },
  ];

  if (canRemoveGroup) {
    columns.push({
      id: "actions",
      header: () => t("adManagement:users.table.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <Button
          type="button"
          variant="destructive"
          size="sm"
          disabled={isRemovePending}
          onClick={() => onRemove(row.original)}
        >
          {t("adManagement:users.groups.actions.removeFromGroup")}
        </Button>
      ),
    });
  }

  return columns;
}
