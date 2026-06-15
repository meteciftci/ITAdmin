import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { RowActions } from "@/components/common/RowActions";
import { Badge } from "@/components/ui/badge";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import {
  getAdDeletedObjectPrimaryLabel,
  getAdDeletedObjectSecondaryLabel,
} from "@/features/ad-management/ad-deleted-object-display-labels";
import { getAdDeletedObjectTypeLabel } from "@/features/ad-management/ad-deleted-object-labels";
import { canRestoreDeletedObject } from "@/features/ad-management/ad-deleted-object-restore-eligibility";
import type { AdDeletedObjectListItem } from "@/features/ad-management/types";

type CreateAdDeletedObjectColumnsOptions = {
  t: TFunction;
  onDetail: (item: AdDeletedObjectListItem) => void;
  onRestore?: (item: AdDeletedObjectListItem) => void;
  canRestore?: boolean;
};

export function createAdDeletedObjectColumns({
  t,
  onDetail,
  onRestore,
  canRestore = false,
}: CreateAdDeletedObjectColumnsOptions): ColumnDef<AdDeletedObjectListItem, unknown>[] {
  return [
    {
      id: "object",
      header: () => t("adManagement:deletedObjects.table.object"),
      cell: ({ row }) => {
        const item = row.original;
        const primaryLabel = getAdDeletedObjectPrimaryLabel(item);
        const secondaryLabel = getAdDeletedObjectSecondaryLabel(item, primaryLabel);

        return (
          <div className="space-y-1">
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
      id: "type",
      header: () => t("adManagement:deletedObjects.table.type"),
      cell: ({ row }) => (
        <Badge variant="outline">
          {getAdDeletedObjectTypeLabel(t, row.original.objectType)}
        </Badge>
      ),
    },
    {
      id: "lastKnownParent",
      header: () => t("adManagement:deletedObjects.table.lastKnownParent"),
      cell: ({ row }) => (
        <p
          className="max-w-xs break-all font-mono text-xs text-muted-foreground"
          title={row.original.lastKnownParent ?? undefined}
        >
          {row.original.lastKnownParent?.trim() || "-"}
        </p>
      ),
    },
    {
      id: "distinguishedName",
      header: () => t("adManagement:deletedObjects.table.distinguishedName"),
      cell: ({ row }) => (
        <p
          className="max-w-md break-all font-mono text-xs text-muted-foreground"
          title={row.original.distinguishedName}
        >
          {row.original.distinguishedName}
        </p>
      ),
    },
    {
      id: "whenChanged",
      header: () => t("adManagement:deletedObjects.table.whenChanged"),
      cell: ({ row }) =>
        row.original.whenChanged ? (
          <DateTimeText value={row.original.whenChanged} />
        ) : (
          <span>-</span>
        ),
    },
    {
      id: "actions",
      header: () => t("adManagement:deletedObjects.table.actions"),
      cell: ({ row }) => (
        <RowActions>
          <DropdownMenuItem onClick={() => onDetail(row.original)}>
            {t("common:actions.detail")}
          </DropdownMenuItem>
          {canRestore && onRestore && canRestoreDeletedObject(row.original) ? (
            <DropdownMenuItem onClick={() => onRestore(row.original)}>
              {t("adManagement:deletedObjects.actions.restore")}
            </DropdownMenuItem>
          ) : null}
        </RowActions>
      ),
      meta: {
        align: "right",
      },
    },
  ];
}
