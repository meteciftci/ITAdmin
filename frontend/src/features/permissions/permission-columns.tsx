import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { StatusBadge } from "@/components/common/StatusBadge";
import type { PermissionListItem } from "@/features/permissions/types";

const getGroupValue = (permission: PermissionListItem): string =>
  permission.group ?? permission.module ?? permission.category ?? "";

type CreatePermissionColumnsOptions = {
  t: TFunction;
  showGroupColumn: boolean;
  showStatusColumn: boolean;
};

export function createPermissionColumns({
  t,
  showGroupColumn,
  showStatusColumn,
}: CreatePermissionColumnsOptions): ColumnDef<PermissionListItem, unknown>[] {
  const columns: ColumnDef<PermissionListItem, unknown>[] = [
    {
      accessorKey: "name",
      header: () => t("permissions:table.name"),
    },
    {
      accessorKey: "code",
      header: () => t("permissions:table.code"),
      cell: ({ row }) => <CodeBadge>{row.original.code}</CodeBadge>,
    },
    {
      accessorKey: "description",
      header: () => t("permissions:table.description"),
      cell: ({ row }) => (
        <span className="line-clamp-2">{row.original.description || "-"}</span>
      ),
    },
  ];

  if (showGroupColumn) {
    columns.push({
      id: "group",
      header: () => t("permissions:table.group"),
      cell: ({ row }) => getGroupValue(row.original) || "-",
    });
  }

  if (showStatusColumn) {
    columns.push({
      id: "status",
      header: () => t("permissions:table.status"),
      cell: ({ row }) => <StatusBadge isActive={row.original.isActive} />,
    });
  }

  return columns;
}
