import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { DataTable, DataTablePagination } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { AD_COMPUTERS_LIST_DEFAULTS } from "@/features/ad-management/ad-computers-list-query";
import { createAdComputerColumns } from "@/features/ad-management/ad-computers-columns";
import { buildAdComputerDetailPath } from "@/features/ad-management/ad-computer-detail-path";
import { buildAdComputersListReturnState } from "@/features/ad-management/ad-computers-return-path";
import {
  AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
  getAdComputers,
} from "@/features/ad-management/api";
import { AdComputersSearchToolbar } from "@/features/ad-management/components/AdComputersSearchToolbar";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { useAdComputerListState } from "@/features/ad-management/use-ad-computer-list-state";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

const MIN_SEARCH_LENGTH = 2;

export function AdComputersPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const moduleStatus = useAdManagementModuleStatus();
  const navigate = useNavigate();
  const { listState, listPath, updateListState, clearListState } = useAdComputerListState();

  const normalizedSearch = listState.search.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;
  const effectiveSearch = canSearch ? normalizedSearch : undefined;
  const activeFilterCount =
    (listState.status !== AD_COMPUTERS_LIST_DEFAULTS.status ? 1 : 0)
    + (listState.operatingSystem.trim() ? 1 : 0);

  const computersQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_COMPUTERS_QUERY_KEY,
      "list",
      effectiveSearch,
      listState.status,
      listState.operatingSystem,
      listState.pageNumber,
      listState.pageSize,
    ],
    queryFn: () =>
      getAdComputers({
        search: effectiveSearch,
        status: listState.status,
        operatingSystem: listState.operatingSystem.trim() || undefined,
        pageNumber: listState.pageNumber,
        pageSize: listState.pageSize,
      }),
    enabled: moduleStatus.isOperational && canSearch,
  });

  const computers = useMemo(() => computersQuery.data?.items ?? [], [computersQuery.data]);

  const columns = useMemo(
    () =>
      createAdComputerColumns({
        t,
        onDetail: (computer) => {
          navigate(buildAdComputerDetailPath(computer.id), {
            state: buildAdComputersListReturnState(),
          });
        },
      }),
    [t, navigate],
  );

  const table = useServerDataTable({
    data: computers,
    columns,
    pageCount: computersQuery.data?.hasNextPage
      ? listState.pageNumber + 1
      : listState.pageNumber,
    pageIndex: listState.pageNumber - 1,
    pageSize: listState.pageSize,
  });

  const handleRefresh = () => {
    if (!canSearch) {
      return;
    }

    computersQuery.refetch();
  };

  if (moduleStatus.isOperational && computersQuery.isError) {
    const routeState = createApiErrorRouteState(computersQuery.error, {
      fromPath: listPath,
      retryPath: listPath,
      sourceLabel: t("adManagement:computers.title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <AdManagementModuleStateGuard>
      <section className="space-y-4">
        <SectionCard
          title={t("adManagement:computers.title")}
          description={t("adManagement:computers.description")}
        >
          <div className="space-y-4">
            <AdComputersSearchToolbar
              listState={listState}
              canSearch={canSearch}
              activeFilterCount={activeFilterCount}
              onListStateChange={updateListState}
              onClearFilters={clearListState}
              onRefresh={handleRefresh}
            />

            {!canSearch ? (
              <EmptyState
                title={t("adManagement:computers.empty.searchRequiredTitle")}
                description={t("adManagement:computers.empty.searchRequired")}
              />
            ) : null}

            {canSearch && computersQuery.isLoading ? <LoadingState /> : null}

            {canSearch && computersQuery.isSuccess && !computers.length ? (
              <EmptyState
                title={t("adManagement:computers.empty.title")}
                description={t("adManagement:computers.empty.description")}
              />
            ) : null}

            {canSearch && computers.length > 0 ? (
              <DataTable
                table={table}
                footer={
                  computersQuery.data ? (
                    <DataTablePagination
                      mode="directory"
                      pageNumber={computersQuery.data.pageNumber}
                      pageSize={computersQuery.data.pageSize}
                      hasNextPage={computersQuery.data.hasNextPage}
                      onPageChange={(nextPage) => {
                        updateListState({ pageNumber: nextPage });
                      }}
                      onPageSizeChange={(nextPageSize) => {
                        updateListState({
                          pageSize: nextPageSize,
                          pageNumber: AD_COMPUTERS_LIST_DEFAULTS.pageNumber,
                        });
                      }}
                    />
                  ) : null
                }
              />
            ) : null}
          </div>
        </SectionCard>
      </section>
    </AdManagementModuleStateGuard>
  );
}
