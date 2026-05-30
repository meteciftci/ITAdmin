import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { DataTableToolbar } from "@/components/common/data-table";
import { Select } from "@/components/ui/select";
import {
  AD_USERS_LIST_DEFAULTS,
  type AdUsersListQueryState,
} from "@/features/ad-management/ad-users-list-query";
import type { AdUserStatusFilter } from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";

type Props = {
  listState: AdUsersListQueryState;
  canSearch: boolean;
  canCreateUser: boolean;
  activeFilterCount: number;
  onListStateChange: (patch: Partial<AdUsersListQueryState>) => void;
  onRefresh: () => void;
};

export function AdUsersSearchToolbar({
  listState,
  canSearch,
  canCreateUser,
  activeFilterCount,
  onListStateChange,
  onRefresh,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const [searchInput, setSearchInput] = useState(listState.q);
  const debouncedSearch = useDebouncedValue(searchInput, 400);

  /* eslint-disable react-hooks/set-state-in-effect -- sync draft search with URL on navigation */
  useEffect(() => {
    setSearchInput(listState.q);
  }, [listState.q]);
  /* eslint-enable react-hooks/set-state-in-effect */

  useEffect(() => {
    if (debouncedSearch === listState.q) {
      return;
    }

    onListStateChange({
      q: debouncedSearch,
      page: AD_USERS_LIST_DEFAULTS.page,
    });
  }, [debouncedSearch, listState.q, onListStateChange]);

  return (
    <DataTableToolbar
      searchValue={searchInput}
      onSearchChange={setSearchInput}
      searchPlaceholder={t("adManagement:users.searchPlaceholder")}
      activeFilterCount={activeFilterCount}
      onClearFilters={() => {
        setSearchInput(AD_USERS_LIST_DEFAULTS.q);
        onListStateChange({
          q: AD_USERS_LIST_DEFAULTS.q,
          status: AD_USERS_LIST_DEFAULTS.status,
          page: AD_USERS_LIST_DEFAULTS.page,
        });
      }}
      filterContent={
        <Select
          value={listState.status}
          onChange={(event) => {
            onListStateChange({
              status: event.target.value as AdUserStatusFilter,
              page: AD_USERS_LIST_DEFAULTS.page,
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
              to="/ad-management/users/create"
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
