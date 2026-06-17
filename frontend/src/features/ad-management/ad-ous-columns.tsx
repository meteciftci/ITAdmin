import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { RowActions } from "@/components/common/RowActions";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/dropdown-menu";
import {
  formatAdOrganizationalUnitCount,
  getAdOrganizationalUnitParentPath,
  getAdOrganizationalUnitPrimaryLabel,
  getAdOrganizationalUnitSecondaryLabel,
} from "@/features/ad-management/ad-ou-display-labels";
import type { AdOrganizationalUnitManageListItem } from "@/features/ad-management/types";

type CreateAdOrganizationalUnitColumnsOptions = {
  t: TFunction;
  canCreate: boolean;
  canUpdate: boolean;
  canMove: boolean;
  canDelete: boolean;
  onDetail: (item: AdOrganizationalUnitManageListItem) => void;
  onCreateChild: (item: AdOrganizationalUnitManageListItem) => void;
  onRename: (item: AdOrganizationalUnitManageListItem) => void;
  onMove: (item: AdOrganizationalUnitManageListItem) => void;
  onDelete: (item: AdOrganizationalUnitManageListItem) => void;
};

export function createAdOrganizationalUnitColumns({
  t,
  canCreate,
  canUpdate,
  canMove,
  canDelete,
  onDetail,
  onCreateChild,
  onRename,
  onMove,
  onDelete,
}: CreateAdOrganizationalUnitColumnsOptions): ColumnDef<AdOrganizationalUnitManageListItem, unknown>[] {
  return [
    {
      id: "organizationalUnit",
      header: () => t("adManagement:organizationalUnits.table.organizationalUnit"),
      cell: ({ row }) => {
        const item = row.original;
        const primaryLabel = getAdOrganizationalUnitPrimaryLabel(item);
        const secondaryLabel = getAdOrganizationalUnitSecondaryLabel(item, primaryLabel);

        return (
          <div className="space-y-0.5" title={item.distinguishedName}>
            <p className="font-medium" title={primaryLabel}>
              {primaryLabel}
            </p>
            {secondaryLabel ? (
              <p className="truncate text-xs text-muted-foreground" title={secondaryLabel}>
                {secondaryLabel}
              </p>
            ) : null}
          </div>
        );
      },
    },
    {
      id: "location",
      header: () => t("adManagement:organizationalUnits.table.location"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const location =
          getAdOrganizationalUnitParentPath(row.original.canonicalName)
          || row.original.parentDistinguishedName
          || "-";
        return (
          <span className="truncate text-muted-foreground" title={location}>
            {location}
          </span>
        );
      },
    },
    {
      id: "childOuCount",
      header: () => t("adManagement:organizationalUnits.table.childOuCount"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => formatAdOrganizationalUnitCount(row.original.childOuCount),
    },
    {
      id: "userCount",
      header: () => t("adManagement:organizationalUnits.table.userCount"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => formatAdOrganizationalUnitCount(row.original.userCount),
    },
    {
      id: "groupCount",
      header: () => t("adManagement:organizationalUnits.table.groupCount"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => formatAdOrganizationalUnitCount(row.original.groupCount),
    },
    {
      id: "computerCount",
      header: () => t("adManagement:organizationalUnits.table.computerCount"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => formatAdOrganizationalUnitCount(row.original.computerCount),
    },
    {
      id: "actions",
      header: () => t("adManagement:organizationalUnits.table.actions"),
      meta: { isAction: true, align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <RowActions>
          <DropdownMenuItem onClick={() => onDetail(row.original)}>
            {t("common:actions.detail")}
          </DropdownMenuItem>
          {canCreate ? (
            <DropdownMenuItem onClick={() => onCreateChild(row.original)}>
              {t("adManagement:organizationalUnits.actions.createChild")}
            </DropdownMenuItem>
          ) : null}
          {canUpdate ? (
            <DropdownMenuItem onClick={() => onRename(row.original)}>
              {t("adManagement:organizationalUnits.actions.rename")}
            </DropdownMenuItem>
          ) : null}
          {canMove ? (
            <DropdownMenuItem onClick={() => onMove(row.original)}>
              {t("adManagement:organizationalUnits.actions.move")}
            </DropdownMenuItem>
          ) : null}
          {canDelete ? (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                className="text-destructive focus:text-destructive"
                onClick={() => onDelete(row.original)}
              >
                {t("common:actions.delete")}
              </DropdownMenuItem>
            </>
          ) : null}
        </RowActions>
      ),
    },
  ];
}
