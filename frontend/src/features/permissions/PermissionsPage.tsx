import { useMemo, useState } from "react";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { getPermissions } from "@/features/permissions/api";
import { createPermissionColumns } from "@/features/permissions/permission-columns";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

type StatusFilter = "active" | "passive" | "all";

export function PermissionsPage() {
  const { t } = useTranslation(["permissions", "common"]);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const debouncedSearch = useDebouncedValue(search, 400);
  const normalizedSearch = debouncedSearch.trim();
  const effectiveSearch =
    normalizedSearch.length >= 3 ? normalizedSearch : undefined;

  const permissionsQuery = useQuery({
    queryKey: ["permissions", "list", effectiveSearch, statusFilter, pageNumber, pageSize],
    queryFn: () =>
      getPermissions({
        search: effectiveSearch,
        isActive:
          statusFilter === "all"
            ? undefined
            : statusFilter === "active"
              ? true
              : false,
        pageNumber,
        pageSize,
      }),
  });

  const permissions = useMemo(
    () => permissionsQuery.data?.items ?? [],
    [permissionsQuery.data],
  );

  const showStatusColumn = permissions.some(
    (permission) => typeof permission.isActive === "boolean",
  );
  const showGroupColumn = permissions.some(
    (permission) =>
      Boolean(permission.group ?? permission.module ?? permission.category),
  );

  const columns = useMemo(
    () =>
      createPermissionColumns({
        t,
        showGroupColumn,
        showStatusColumn,
      }),
    [t, showGroupColumn, showStatusColumn],
  );

  const table = useServerDataTable({
    data: permissions,
    columns,
    pageCount: permissionsQuery.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  const activeFilterCount = statusFilter !== "active" ? 1 : 0;

  const handleRefresh = () => {
    permissionsQuery.refetch();
  };
  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPageNumber(1);
  };

  if (permissionsQuery.isError) {
    const routeState = createApiErrorRouteState(permissionsQuery.error, {
      fromPath: "/permissions",
      retryPath: "/permissions",
      sourceLabel: t("permissions:sections.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <SectionCard title={t("permissions:sections.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("permissions:search.placeholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setStatusFilter("active");
              setPageNumber(1);
            }}
            filterContent={
              <Select
                value={statusFilter}
                onChange={(event) => {
                  setStatusFilter(event.target.value as StatusFilter);
                  setPageNumber(1);
                }}
                className="w-full"
              >
                <option value="active">{t("common:status.active")}</option>
                <option value="passive">{t("common:status.passive")}</option>
                <option value="all">{t("common:status.all")}</option>
              </Select>
            }
            actions={
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
            }
          />

          {permissionsQuery.isLoading ? <LoadingState /> : null}

          {permissionsQuery.isSuccess && !permissions.length ? (
            <EmptyState
              title={t("permissions:empty.title")}
              description={t("permissions:empty.description")}
            />
          ) : null}

          {permissions.length ? (
            <DataTable
              table={table}
              footer={
                permissionsQuery.data && permissionsQuery.data.totalCount > 0 ? (
                  <DataTablePagination
                    mode="server"
                    pageNumber={permissionsQuery.data.pageNumber}
                    pageSize={permissionsQuery.data.pageSize}
                    totalCount={permissionsQuery.data.totalCount}
                    totalPages={permissionsQuery.data.totalPages}
                    onPageChange={setPageNumber}
                    onPageSizeChange={(nextPageSize) => {
                      setPageSize(nextPageSize);
                      setPageNumber(1);
                    }}
                  />
                ) : null
              }
            />
          ) : null}
        </div>
      </SectionCard>
    </section>
  );
}
