import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { RowActions } from "@/components/common/RowActions";
import { StatusBadge } from "@/components/common/StatusBadge";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import type { RoleListItem } from "@/features/roles/types";

type CreateRoleColumnsOptions = {
  t: TFunction;
  canUpdate: boolean;
  canAssignPermissions: boolean;
  canViewPermissions: boolean;
  isStatusPending: boolean;
  onDetail: (role: RoleListItem) => void;
  onEdit: (role: RoleListItem) => void;
  onToggleStatus: (role: RoleListItem) => void;
  onAssignPermissions: (role: RoleListItem) => void;
};

export function createRoleColumns({
  t,
  canUpdate,
  canAssignPermissions,
  canViewPermissions,
  isStatusPending,
  onDetail,
  onEdit,
  onToggleStatus,
  onAssignPermissions,
}: CreateRoleColumnsOptions): ColumnDef<RoleListItem, unknown>[] {
  return [
    {
      accessorKey: "name",
      header: () => t("common:fields.name"),
    },
    {
      accessorKey: "code",
      header: () => t("common:fields.code"),
      cell: ({ row }) => <CodeBadge>{row.original.code}</CodeBadge>,
    },
    {
      accessorKey: "description",
      header: () => t("common:fields.description"),
      cell: ({ row }) => (
        <span className="line-clamp-2">{row.original.description || "-"}</span>
      ),
    },
    {
      id: "type",
      header: () => t("common:fields.type"),
      cell: ({ row }) => {
        const isSystemRole = row.original.isSystem;
        return (
          <Badge variant={isSystemRole ? "warning" : "secondary"}>
            {isSystemRole ? t("roles:type.system") : t("roles:type.custom")}
          </Badge>
        );
      },
    },
    {
      id: "status",
      header: () => t("common:fields.status"),
      cell: ({ row }) => <StatusBadge isActive={row.original.isActive} />,
    },
    {
      accessorKey: "permissionCount",
      header: () => t("roles:table.permissionCount"),
      cell: ({ row }) => <Badge variant="outline">{row.original.permissionCount}</Badge>,
    },
    {
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const role = row.original;
        const isSystemRole = role.isSystem;
        const canEditRole = canUpdate && !isSystemRole;
        const canChangeStatus = canUpdate && !isSystemRole;
        const canAssignRolePermissions =
          canAssignPermissions && canViewPermissions && !isSystemRole;

        return (
          <RowActions>
            <DropdownMenuLabel>{t("common:actions.actions")}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => onDetail(role)}>
              {t("common:actions.detail")}
            </DropdownMenuItem>
            {canEditRole ? (
              <DropdownMenuItem onClick={() => onEdit(role)}>
                {t("common:actions.edit")}
              </DropdownMenuItem>
            ) : null}
            {canChangeStatus ? (
              <DropdownMenuItem
                disabled={isStatusPending}
                onClick={() => onToggleStatus(role)}
              >
                {role.isActive
                  ? t("roles:actions.deactivate")
                  : t("roles:actions.activate")}
              </DropdownMenuItem>
            ) : null}
            {canAssignRolePermissions ? (
              <DropdownMenuItem onClick={() => onAssignPermissions(role)}>
                {t("roles:actions.assignPermissions")}
              </DropdownMenuItem>
            ) : null}
          </RowActions>
        );
      },
    },
  ];
}
