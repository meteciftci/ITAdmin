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
  canDisableUser: boolean;
  canEnableUser: boolean;
  canUnlockUser: boolean;
  onDetail: (user: AdUserListItem) => void;
  onManageGroups: (user: AdUserListItem) => void;
  onDisable: (user: AdUserListItem) => void;
  onEnable: (user: AdUserListItem) => void;
  onUnlock: (user: AdUserListItem) => void;
};

export function createAdUserColumns({
  t,
  canManageGroups,
  canDisableUser,
  canEnableUser,
  canUnlockUser,
  onDetail,
  onManageGroups,
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
      cell: ({ row }) => <AdAccountStatusBadge isEnabled={row.original.isEnabled} />,
    },
    {
      id: "locked",
      header: () => t("adManagement:users.table.locked"),
      cell: ({ row }) => <AdLockStatusBadge isLockedOut={row.original.isLockedOut} />,
    },
    {
      id: "lastLogon",
      header: () => t("adManagement:users.table.lastLogon"),
      cell: ({ row }) => <DateTimeText value={row.original.lastLogonAt} />,
    },
    {
      id: "actions",
      header: () => t("adManagement:users.table.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const user = row.original;
        return (
          <RowActions>
            <DropdownMenuLabel>{t("common:actions.actions")}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => onDetail(user)}>
              {t("adManagement:users.actions.detail")}
            </DropdownMenuItem>
            {canManageGroups ? (
              <DropdownMenuItem onClick={() => onManageGroups(user)}>
                {t("adManagement:users.actions.manageGroups")}
              </DropdownMenuItem>
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
