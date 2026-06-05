import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { RowActions } from "@/components/common/RowActions";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import {
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { AdAccountStatusBadge } from "@/features/ad-management/components/AdAccountStatusBadge";
import { AdLockStatusBadge } from "@/features/ad-management/components/AdLockStatusBadge";
import type { AdUserListItem } from "@/features/ad-management/types";

type CreateAdUserColumnsOptions = {
  t: TFunction;
  canManageGroups: boolean;
  canUpdateUser: boolean;
  canDisableUser: boolean;
  canEnableUser: boolean;
  canUnlockUser: boolean;
  canMoveOu: boolean;
  onDetail: (user: AdUserListItem) => void;
  onEdit: (user: AdUserListItem) => void;
  onManageGroups: (user: AdUserListItem) => void;
  onMoveOu: (user: AdUserListItem) => void;
  onDisable: (user: AdUserListItem) => void;
  onEnable: (user: AdUserListItem) => void;
  onUnlock: (user: AdUserListItem) => void;
};

export function createAdUserColumns({
  t,
  canManageGroups,
  canUpdateUser,
  canDisableUser,
  canEnableUser,
  canUnlockUser,
  canMoveOu,
  onDetail,
  onEdit,
  onManageGroups,
  onMoveOu,
  onDisable,
  onEnable,
  onUnlock,
}: CreateAdUserColumnsOptions): ColumnDef<AdUserListItem, unknown>[] {
  return [
    {
      accessorKey: "displayName",
      header: () => t("adManagement:users.table.displayName"),
      cell: ({ row }) => row.original.displayName || "-",
    },
    {
      accessorKey: "samAccountName",
      header: () => t("adManagement:users.table.username"),
      cell: ({ row }) => row.original.samAccountName || "-",
    },
    {
      accessorKey: "userPrincipalName",
      header: () => t("adManagement:users.table.upn"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.userPrincipalName || "-",
    },
    {
      accessorKey: "mail",
      header: () => t("adManagement:users.table.email"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.mail || "-",
    },
    {
      accessorKey: "department",
      header: () => t("adManagement:users.table.department"),
      cell: ({ row }) => row.original.department || "-",
    },
    {
      id: "status",
      header: () => t("adManagement:users.table.status"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <div className="flex flex-wrap items-center gap-2">
          <AdAccountStatusBadge isEnabled={row.original.isEnabled} />
          {row.original.isLockedOut ? (
            <AdLockStatusBadge isLockedOut={row.original.isLockedOut} />
          ) : null}
        </div>
      ),
    },
    {
      id: "lastLogon",
      header: () => t("adManagement:users.table.lastLogon"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => <DateTimeText value={row.original.lastLogonAt} />,
    },
    {
      id: "actions",
      header: () => t("adManagement:users.table.actions"),
      meta: { isAction: true, align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const user = row.original;
        return (
          <RowActions>
            <DropdownMenuLabel>{t("common:actions.actions")}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => onDetail(user)}>
              {t("adManagement:users.actions.detail")}
            </DropdownMenuItem>
            {canUpdateUser ? (
              <DropdownMenuItem onClick={() => onEdit(user)}>
                {t("adManagement:users.actions.edit")}
              </DropdownMenuItem>
            ) : null}
            {canManageGroups ? (
              <DropdownMenuItem onClick={() => onManageGroups(user)}>
                {t("adManagement:users.actions.manageGroups")}
              </DropdownMenuItem>
            ) : null}
            {canMoveOu ? (
              <DropdownMenuItem onClick={() => onMoveOu(user)}>
                {t("adManagement:users.actions.moveOu")}
              </DropdownMenuItem>
            ) : null}
            {(canDisableUser && user.isEnabled)
            || (canEnableUser && !user.isEnabled)
            || (canUnlockUser && user.isLockedOut) ? (
              <DropdownMenuSeparator />
            ) : null}
            {canDisableUser && user.isEnabled ? (
              <DropdownMenuItem onClick={() => onDisable(user)}>
                {t("adManagement:users.actions.disable")}
              </DropdownMenuItem>
            ) : null}
            {canEnableUser && !user.isEnabled ? (
              <DropdownMenuItem onClick={() => onEnable(user)}>
                {t("adManagement:users.actions.enable")}
              </DropdownMenuItem>
            ) : null}
            {canUnlockUser && user.isLockedOut ? (
              <DropdownMenuItem onClick={() => onUnlock(user)}>
                {t("adManagement:users.actions.unlock")}
              </DropdownMenuItem>
            ) : null}
          </RowActions>
        );
      },
    },
  ];
}
