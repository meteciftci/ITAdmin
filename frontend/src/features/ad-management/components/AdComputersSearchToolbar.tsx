import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { DataTableToolbar } from "@/components/common/data-table";
import { Select } from "@/components/ui/select";
import {
  AD_COMPUTERS_LIST_DEFAULTS,
  type AdComputersListState,
} from "@/features/ad-management/ad-computers-list-query";
import type { AdComputerStatusFilter } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";

type Props = {
  listState: AdComputersListState;
  canSearch: boolean;
  activeFilterCount: number;
  onListStateChange: (patch: Partial<AdComputersListState>) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
};

export function AdComputersSearchToolbar({
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
      pageNumber: AD_COMPUTERS_LIST_DEFAULTS.pageNumber,
    });
  }, [debouncedSearch, listState.search, onListStateChange]);

  return (
    <DataTableToolbar
      searchValue={searchInput}
      onSearchChange={setSearchInput}
      searchPlaceholder={t("adManagement:computers.searchPlaceholder")}
      activeFilterCount={activeFilterCount}
      onClearFilters={() => {
        setSearchInput(AD_COMPUTERS_LIST_DEFAULTS.search);
        onClearFilters();
      }}
      filterContent={
        <Select
          value={listState.status}
          onChange={(event) => {
            onListStateChange({
              status: event.target.value as AdComputerStatusFilter,
              pageNumber: AD_COMPUTERS_LIST_DEFAULTS.pageNumber,
            });
          }}
          className="w-full"
        >
          <option value="active">{t("adManagement:computers.filters.active")}</option>
          <option value="disabled">{t("adManagement:computers.filters.disabled")}</option>
          <option value="all">{t("adManagement:computers.filters.all")}</option>
        </Select>
      }
      actions={
        <Button variant="outline" onClick={onRefresh} disabled={!canSearch}>
          {t("common:actions.refresh")}
        </Button>
      }
    />
  );
}
