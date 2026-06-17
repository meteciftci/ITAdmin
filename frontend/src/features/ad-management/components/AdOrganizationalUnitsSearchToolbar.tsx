import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { DataTableToolbar } from "@/components/common/data-table";
import { AD_ORGANIZATIONAL_UNIT_CREATE_PATH } from "@/features/ad-management/ad-ou-detail-path";
import {
  AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS,
  type AdOrganizationalUnitsListState,
} from "@/features/ad-management/ad-ous-list-query";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

type Props = {
  listState: AdOrganizationalUnitsListState;
  canSearch: boolean;
  onListStateChange: (patch: Partial<AdOrganizationalUnitsListState>) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
  canCreate: boolean;
};

export function AdOrganizationalUnitsSearchToolbar({
  listState,
  canSearch,
  onListStateChange,
  onClearFilters,
  onRefresh,
  canCreate,
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
            <Link
              to={AD_ORGANIZATIONAL_UNIT_CREATE_PATH}
              className={cn(buttonVariants({ variant: "default" }))}
            >
              {t("adManagement:organizationalUnits.actions.create")}
            </Link>
          ) : null}
          <Button type="button" variant="outline" onClick={onRefresh} disabled={!canSearch}>
            {t("common:actions.refresh")}
          </Button>
        </>
      }
    />
  );
}
