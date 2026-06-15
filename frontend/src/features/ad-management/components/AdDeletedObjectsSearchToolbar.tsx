import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { DataTableToolbar } from "@/components/common/data-table";
import { Select } from "@/components/ui/select";
import {
  AD_DELETED_OBJECTS_LIST_DEFAULTS,
  type AdDeletedObjectsListState,
} from "@/features/ad-management/ad-deleted-objects-list-query";
import type { AdDeletedObjectTypeFilter } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";

type Props = {
  listState: AdDeletedObjectsListState;
  canSearch: boolean;
  activeFilterCount: number;
  onListStateChange: (patch: Partial<AdDeletedObjectsListState>) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
};

export function AdDeletedObjectsSearchToolbar({
  listState,
  canSearch,
  activeFilterCount,
  onListStateChange,
  onClearFilters,
  onRefresh,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [searchInput, setSearchInput] = useState(listState.search);
  const debouncedSearch = useDebouncedValue(searchInput, 400);

  /* eslint-disable react-hooks/set-state-in-effect -- sync draft search when list state is restored */
  useEffect(() => {
    setSearchInput(listState.search);
  }, [listState.search]);
  /* eslint-enable react-hooks/set-state-in-effect */

  useEffect(() => {
    if (debouncedSearch === listState.search) {
      return;
    }

    onListStateChange({
      search: debouncedSearch,
      pageNumber: AD_DELETED_OBJECTS_LIST_DEFAULTS.pageNumber,
    });
  }, [debouncedSearch, listState.search, onListStateChange]);

  return (
    <DataTableToolbar
      searchValue={searchInput}
      onSearchChange={setSearchInput}
      searchPlaceholder={t("adManagement:deletedObjects.searchPlaceholder")}
      activeFilterCount={activeFilterCount}
      onClearFilters={() => {
        setSearchInput(AD_DELETED_OBJECTS_LIST_DEFAULTS.search);
        onClearFilters();
      }}
      filterContent={
        <div className="space-y-3">
          <label className="space-y-1 text-sm">
            <span className="text-muted-foreground">
              {t("adManagement:deletedObjects.filters.type")}
            </span>
            <Select
              value={listState.type}
              onChange={(event) => {
                onListStateChange({
                  type: event.target.value as AdDeletedObjectTypeFilter,
                  pageNumber: AD_DELETED_OBJECTS_LIST_DEFAULTS.pageNumber,
                });
              }}
              className="w-full"
            >
              <option value="all">{t("adManagement:deletedObjects.filters.typeAll")}</option>
              <option value="user">{t("adManagement:deletedObjects.filters.typeUser")}</option>
              <option value="group">{t("adManagement:deletedObjects.filters.typeGroup")}</option>
              <option value="computer">{t("adManagement:deletedObjects.filters.typeComputer")}</option>
            </Select>
          </label>
        </div>
      }
      actions={
        <Button type="button" variant="outline" onClick={onRefresh} disabled={!canSearch}>
          {t("common:actions.refresh")}
        </Button>
      }
    />
  );
}
