import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { DataTableToolbar } from "@/components/common/data-table";
import { AD_GROUP_CREATE_PATH } from "@/features/ad-management/ad-group-detail-path";
import {
  AD_GROUPS_LIST_DEFAULTS,
  type AdGroupsListState,
} from "@/features/ad-management/ad-groups-list-query";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

type Props = {
  listState: AdGroupsListState;
  canSearch: boolean;
  canCreateGroup: boolean;
  onListStateChange: (patch: Partial<AdGroupsListState>) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
};

export function AdGroupsSearchToolbar({
  listState,
  canSearch,
  canCreateGroup,
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
      pageNumber: AD_GROUPS_LIST_DEFAULTS.pageNumber,
    });
  }, [debouncedSearch, listState.search, onListStateChange]);

  return (
    <DataTableToolbar
      searchValue={searchInput}
      onSearchChange={setSearchInput}
      searchPlaceholder={t("adManagement:groups.searchPlaceholder")}
      activeFilterCount={0}
      onClearFilters={() => {
        setSearchInput(AD_GROUPS_LIST_DEFAULTS.search);
        onClearFilters();
      }}
      actions={
        <>
          {canCreateGroup ? (
            <Link
              to={AD_GROUP_CREATE_PATH}
              className={cn(buttonVariants({ variant: "default" }))}
            >
              {t("adManagement:groups.create.actions.open")}
            </Link>
          ) : null}
          <Button variant="outline" onClick={onRefresh} disabled={!canSearch}>
            {t("common:actions.refresh")}
          </Button>
        </>
      }
    />
  );
}
