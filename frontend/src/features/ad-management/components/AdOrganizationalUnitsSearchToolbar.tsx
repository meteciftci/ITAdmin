import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { DataTableToolbar } from "@/components/common/data-table";
import {
  AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS,
  type AdOrganizationalUnitsListState,
} from "@/features/ad-management/ad-ous-list-query";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";

type Props = {
  listState: AdOrganizationalUnitsListState;
  onListStateChange: (patch: Partial<AdOrganizationalUnitsListState>) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
  canCreate: boolean;
  onCreate: () => void;
};

export function AdOrganizationalUnitsSearchToolbar({
  listState,
  onListStateChange,
  onClearFilters,
  onRefresh,
  canCreate,
  onCreate,
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
      pageNumber: AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS.pageNumber,
    });
  }, [debouncedSearch, listState.search, onListStateChange]);

  return (
    <DataTableToolbar
      searchValue={searchInput}
      onSearchChange={setSearchInput}
      searchPlaceholder={t("adManagement:organizationalUnits.searchPlaceholder")}
      activeFilterCount={0}
      onClearFilters={() => {
        setSearchInput(AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS.search);
        onClearFilters();
      }}
      actions={
        <>
          {canCreate ? (
            <Button type="button" onClick={onCreate}>
              {t("adManagement:organizationalUnits.actions.create")}
            </Button>
          ) : null}
          <Button type="button" variant="outline" onClick={onRefresh}>
            {t("common:actions.refresh")}
          </Button>
        </>
      }
    />
  );
}
