import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { DataTable } from "@/components/common/data-table";
import { useClientDataTable } from "@/components/common/data-table-hooks";
import { Button } from "@/components/ui/button";
import { createAdAttributeMappingColumns } from "@/features/ad-management/ad-attribute-mapping-columns";
import type { AdAttributeMapping } from "@/features/ad-management/types";

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

  const table = useClientDataTable({
    data: mappings,
    columns,
    enablePagination: false,
  });

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
        {!readOnly ? (
          <Button onClick={onCreate}>
            {t("settings:adManagement.mappings.actions.create")}
          </Button>
        ) : null}
      </div>

      {isLoading ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("common:loading")}
        </p>
      ) : null}

      {isEmpty ? (
        <div className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">
          <p>{t("settings:adManagement.mappings.empty.title")}</p>
          <p className="text-xs">{t("settings:adManagement.mappings.empty.description")}</p>
        </div>
      ) : null}

      {!isLoading && mappings.length > 0 ? <DataTable table={table} /> : null}
    </div>
  );
}
