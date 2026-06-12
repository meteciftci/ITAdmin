import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { RowActions } from "@/components/common/RowActions";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/dropdown-menu";
import {
  getAdComputerPrimaryLabel,
  getAdComputerSecondaryLabel,
} from "@/features/ad-management/ad-computer-display-labels";
import { AdComputerStatusBadge } from "@/features/ad-management/components/AdComputerStatusBadge";
import type { AdComputerListItem } from "@/features/ad-management/types";

type CreateAdComputerColumnsOptions = {
  t: TFunction;
  canDisableComputer: boolean;
  canEnableComputer: boolean;
  onDetail: (computer: AdComputerListItem) => void;
  onDisable: (computer: AdComputerListItem) => void;
  onEnable: (computer: AdComputerListItem) => void;
};

export function createAdComputerColumns({
  t,
  canDisableComputer,
  canEnableComputer,
  onDetail,
  onDisable,
  onEnable,
}: CreateAdComputerColumnsOptions): ColumnDef<AdComputerListItem, unknown>[] {
  return [
    {
      id: "computer",
      header: () => t("adManagement:computers.table.computer"),
      cell: ({ row }) => {
        const computer = row.original;
        const primaryLabel = getAdComputerPrimaryLabel(computer);
        const secondaryLabel = getAdComputerSecondaryLabel(computer, primaryLabel);

        return (
          <div className="space-y-0.5" title={computer.distinguishedName}>
            <p className="font-medium" title={computer.distinguishedName}>
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
      accessorKey: "dnsHostName",
      header: () => t("adManagement:computers.table.dnsHostName"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.dnsHostName || "-",
    },
    {
      accessorKey: "operatingSystem",
      header: () => t("adManagement:computers.table.operatingSystem"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => row.original.operatingSystem || "-",
    },
    {
      id: "status",
      header: () => t("adManagement:computers.table.status"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => <AdComputerStatusBadge isEnabled={row.original.isEnabled} />,
    },
    {
      id: "whenChanged",
      header: () => t("adManagement:computers.table.lastChanged"),
      meta: { align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => <DateTimeText value={row.original.whenChanged} />,
    },
    {
      id: "actions",
      header: () => t("adManagement:computers.table.actions"),
      meta: { isAction: true, align: "center" } satisfies DataTableColumnMeta,
      cell: ({ row }) => {
        const computer = row.original;
        return (
          <RowActions>
            <DropdownMenuItem onClick={() => onDetail(computer)}>
              {t("common:actions.detail")}
            </DropdownMenuItem>
            {(canDisableComputer && computer.isEnabled)
            || (canEnableComputer && !computer.isEnabled) ? (
              <>
                <DropdownMenuSeparator />
                {canDisableComputer && computer.isEnabled ? (
                  <DropdownMenuItem onClick={() => onDisable(computer)}>
                    {t("adManagement:computers.actions.disable")}
                  </DropdownMenuItem>
                ) : null}
                {canEnableComputer && !computer.isEnabled ? (
                  <DropdownMenuItem onClick={() => onEnable(computer)}>
                    {t("adManagement:computers.actions.enable")}
                  </DropdownMenuItem>
                ) : null}
              </>
            ) : null}
          </RowActions>
        );
      },
    },
  ];
}
