import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { RowActions } from "@/components/common/RowActions";
import { RoleBadgeList } from "@/components/common/RoleBadgeList";
import { StatusBadge } from "@/components/common/StatusBadge";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import {
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import type { UserListItem } from "@/features/users/types";

type CreateUserColumnsOptions = {
  t: TFunction;
  canUpdate: boolean;
  canAssignRoles: boolean;
  isStatusPending: boolean;
  onDetail: (user: UserListItem) => void;
  onToggleStatus: (user: UserListItem) => void;
  onAssignRoles: (user: UserListItem) => void;
};

export function createUserColumns({
  t,
  canUpdate,
  canAssignRoles,
  isStatusPending,
  onDetail,
  onToggleStatus,
  onAssignRoles,
}: CreateUserColumnsOptions): ColumnDef<UserListItem, unknown>[] {
  return [
    {
      accessorKey: "displayName",
      header: () => t("users:table.displayName"),
      cell: ({ row }) => row.original.displayName || "-",
    },
    {
      accessorKey: "userName",
      header: () => t("users:table.userName"),
    },
    {
      accessorKey: "email",
      header: () => t("users:table.email"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.email || "-",
    },
    {
      accessorKey: "nationalIdMasked",
      header: () => t("users:table.nationalIdMasked"),
      cell: ({ row }) => row.original.nationalIdMasked || "-",
    },
    {
      id: "roles",
      header: () => t("users:table.roles"),
      cell: ({ row }) => <RoleBadgeList roles={row.original.roles} />,
    },
    {
      id: "status",
      header: () => t("users:table.status"),
      cell: ({ row }) => <StatusBadge isActive={row.original.isActive} />,
    },
    {
      id: "lastLogin",
      header: () => t("users:table.lastLogin"),
      cell: ({ row }) => <DateTimeText value={row.original.lastLoginAt} />,
    },
    {
      id: "actions",
      header: () => t("users:table.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const user = row.original;
        return (
          <RowActions>
            <DropdownMenuLabel>{t("common:actions.actions")}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => onDetail(user)}>
              {t("users:actions.detail")}
            </DropdownMenuItem>
            {canUpdate ? (
              <DropdownMenuItem
                disabled={isStatusPending}
                onClick={() => onToggleStatus(user)}
              >
                {user.isActive
                  ? t("users:actions.deactivate")
                  : t("users:actions.activate")}
              </DropdownMenuItem>
            ) : null}
            {canAssignRoles ? (
              <DropdownMenuItem onClick={() => onAssignRoles(user)}>
                {t("users:actions.assignRoles")}
              </DropdownMenuItem>
            ) : null}
          </RowActions>
        );
      },
    },
  ];
}
