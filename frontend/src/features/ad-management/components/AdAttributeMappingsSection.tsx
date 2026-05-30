import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useClientDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { Button } from "@/components/ui/button";
import { createAdAttributeMappingColumns } from "@/features/ad-management/ad-attribute-mapping-columns";
import type { AdAttributeMapping } from "@/features/ad-management/types";

const PAGE_SIZE_OPTIONS = [10, 25, 50];
const DEFAULT_PAGE_SIZE = 10;

type Props = {
  mappings: AdAttributeMapping[];
  readOnly: boolean;
  isLoading: boolean;
  onCreate: () => void;
  onEdit: (mapping: AdAttributeMapping) => void;
  onDelete: (mapping: AdAttributeMapping) => void;
};

export function AdAttributeMappingsSection({
  mappings,
  readOnly,
  isLoading,
  onCreate,
  onEdit,
  onDelete,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);

  const [search, setSearch] = useState("");

  const isEmpty = !isLoading && mappings.length === 0;

  const columns = useMemo(
    () =>
      createAdAttributeMappingColumns({
        t,
        readOnly,
        onEdit,
        onDelete,
      }),
    [t, readOnly, onEdit, onDelete],
  );

  const getSearchableValue = useMemo(
    () => (row: AdAttributeMapping) =>
      [row.logicalField, row.attributeName, row.displayName].filter(Boolean).join(" "),
    [],
  );

  const table = useClientDataTable({
    data: mappings,
    columns,
    globalFilter: search,
    enableGlobalFilter: true,
    getSearchableValue,
    initialPageSize: DEFAULT_PAGE_SIZE,
  });

  const hasRows = mappings.length > 0;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">
            {t("settings:adManagement.mappings.title")}
          </h3>
          <p className="text-xs text-muted-foreground">
            {t("settings:adManagement.mappings.description")}
          </p>
        </div>
      </div>

      {isLoading ? <LoadingState /> : null}

      {!isLoading ? (
        <>
          <DataTableToolbar
            searchValue={search}
            onSearchChange={setSearch}
            searchPlaceholder={t("settings:adManagement.mappings.searchPlaceholder")}
            actions={
              !readOnly ? (
                <Button onClick={onCreate}>
                  {t("settings:adManagement.mappings.actions.create")}
                </Button>
              ) : null
            }
          />

          {isEmpty ? (
            <EmptyState
              title={t("settings:adManagement.mappings.empty.title")}
              description={t("settings:adManagement.mappings.empty.description")}
            />
          ) : (
            <DataTable
              table={table}
              emptyMessage={t("common:dataTable.noResults")}
              footer={
                hasRows ? (
                  <DataTablePagination
                    mode="client"
                    table={table}
                    pageSizeOptions={PAGE_SIZE_OPTIONS}
                  />
                ) : undefined
              }
            />
          )}
        </>
      ) : null}
    </div>
  );
}
