import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import type { DataTableColumnMeta } from "@/components/common/data-table";
import { Button } from "@/components/ui/button";
import type { AdAttributeMapping } from "@/features/ad-management/types";

function formatBool(value: boolean, t: TFunction): string {
  return value ? t("common:status.active") : t("common:status.passive");
}

type CreateAdAttributeMappingColumnsOptions = {
  t: TFunction;
  readOnly: boolean;
  onEdit: (mapping: AdAttributeMapping) => void;
  onDelete: (mapping: AdAttributeMapping) => void;
};

export function createAdAttributeMappingColumns({
  t,
  readOnly,
  onEdit,
  onDelete,
}: CreateAdAttributeMappingColumnsOptions): ColumnDef<AdAttributeMapping, unknown>[] {
  const columns: ColumnDef<AdAttributeMapping, unknown>[] = [
    {
      accessorKey: "displayName",
      header: () => t("settings:adManagement.mappings.table.displayName"),
      cell: ({ row }) => <span className="font-medium">{row.original.displayName}</span>,
    },
    {
      accessorKey: "logicalField",
      header: () => t("settings:adManagement.mappings.table.logicalField"),
      meta: { mono: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <code className="rounded bg-muted px-1.5 py-0.5 text-xs">{row.original.logicalField}</code>
      ),
    },
    {
      accessorKey: "attributeName",
      header: () => t("settings:adManagement.mappings.table.attributeName"),
      meta: { mono: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <code className="rounded bg-muted px-1.5 py-0.5 text-xs">{row.original.attributeName}</code>
      ),
    },
    {
      id: "isEnabled",
      header: () => t("settings:adManagement.mappings.table.isEnabled"),
      cell: ({ row }) => formatBool(row.original.isEnabled, t),
    },
    {
      id: "isEditable",
      header: () => t("settings:adManagement.mappings.table.isEditable"),
      cell: ({ row }) => formatBool(row.original.isEditable, t),
    },
    {
      id: "isSensitive",
      header: () => t("settings:adManagement.mappings.table.isSensitive"),
      cell: ({ row }) => formatBool(row.original.isSensitive, t),
    },
    {
      id: "validationType",
      header: () => t("settings:adManagement.mappings.table.validationType"),
      cell: ({ row }) =>
        t(`settings:adManagement.mappings.validationTypes.${row.original.validationType}`, {
          defaultValue: row.original.validationType,
        }),
    },
    {
      id: "maskingStrategy",
      header: () => t("settings:adManagement.mappings.table.maskingStrategy"),
      cell: ({ row }) =>
        t(`settings:adManagement.mappings.maskingStrategies.${row.original.maskingStrategy}`, {
          defaultValue: row.original.maskingStrategy,
        }),
    },
    {
      accessorKey: "sortOrder",
      header: () => t("settings:adManagement.mappings.table.sortOrder"),
    },
  ];

  if (!readOnly) {
    columns.push({
      id: "actions",
      header: () => t("settings:adManagement.mappings.table.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => onEdit(row.original)}>
            {t("settings:adManagement.mappings.actions.edit")}
          </Button>
          <Button variant="destructive" onClick={() => onDelete(row.original)}>
            {t("settings:adManagement.mappings.actions.delete")}
          </Button>
        </div>
      ),
    });
  }

  return columns;
}
