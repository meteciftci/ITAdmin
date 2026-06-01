import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { DataTableToolbar } from "@/components/common/data-table";
import { Select } from "@/components/ui/select";
import { AD_USERS_LIST_PATH } from "@/features/ad-management/ad-users-list-path";
import {
  AD_USERS_LIST_DEFAULTS,
  type AdUsersListState,
} from "@/features/ad-management/ad-users-list-query";
import type { AdUserStatusFilter } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

type Props = {
  listState: AdUsersListState;
  canSearch: boolean;
  canCreateUser: boolean;
  activeFilterCount: number;
  onListStateChange: (patch: Partial<AdUsersListState>) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
};

export function AdUsersSearchToolbar({
  listState,
  canSearch,
  canCreateUser,
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
      pageNumber: AD_USERS_LIST_DEFAULTS.pageNumber,
    });
  }, [debouncedSearch, listState.search, onListStateChange]);

  return (
    <DataTableToolbar
      searchValue={searchInput}
      onSearchChange={setSearchInput}
      searchPlaceholder={t("adManagement:users.searchPlaceholder")}
      activeFilterCount={activeFilterCount}
      onClearFilters={() => {
        setSearchInput(AD_USERS_LIST_DEFAULTS.search);
        onClearFilters();
      }}
      filterContent={
        <Select
          value={listState.status}
          onChange={(event) => {
            onListStateChange({
              status: event.target.value as AdUserStatusFilter,
              pageNumber: AD_USERS_LIST_DEFAULTS.pageNumber,
            });
          }}
          className="w-full"
        >
          <option value="active">{t("adManagement:users.filters.active")}</option>
          <option value="disabled">{t("adManagement:users.filters.disabled")}</option>
          <option value="all">{t("adManagement:users.filters.all")}</option>
        </Select>
      }
      actions={
        <>
          {canCreateUser ? (
            <Link
              to={`${AD_USERS_LIST_PATH}/create`}
              className={cn(buttonVariants({ variant: "default" }))}
            >
              {t("adManagement:users.actions.create")}
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
