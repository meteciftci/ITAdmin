import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { DataTable, DataTablePagination } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { AD_DELETED_OBJECTS_LIST_DEFAULTS } from "@/features/ad-management/ad-deleted-objects-list-query";
import { createAdDeletedObjectColumns } from "@/features/ad-management/ad-deleted-objects-columns";
import { buildAdDeletedObjectDetailPath } from "@/features/ad-management/ad-deleted-object-detail-path";
import { buildAdDeletedObjectsListReturnState } from "@/features/ad-management/ad-deleted-objects-return-path";
import {
  AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY,
  getAdDeletedObjects,
} from "@/features/ad-management/api";
import { AdDeletedObjectsSearchToolbar } from "@/features/ad-management/components/AdDeletedObjectsSearchToolbar";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { useAdDeletedObjectListState } from "@/features/ad-management/use-ad-deleted-object-list-state";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

const MIN_SEARCH_LENGTH = 2;

export function AdDeletedObjectsPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const moduleStatus = useAdManagementModuleStatus();
  const navigate = useNavigate();
  const { listState, listPath, updateListState, clearListState } = useAdDeletedObjectListState();

  const normalizedSearch = listState.search.trim();
  const hasTypeFilter = listState.type !== AD_DELETED_OBJECTS_LIST_DEFAULTS.type;
  const canSearch =
    normalizedSearch.length >= MIN_SEARCH_LENGTH || hasTypeFilter || listState.includeAll;
  const effectiveSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH ? normalizedSearch : undefined;
  const activeFilterCount = hasTypeFilter ? 1 : 0;

  const deletedObjectsQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_DELETED_OBJECTS_QUERY_KEY,
      "list",
      effectiveSearch,
      listState.type,
      listState.includeAll,
      listState.pageNumber,
      listState.pageSize,
    ],
    queryFn: () =>
      getAdDeletedObjects({
        search: effectiveSearch,
        type: listState.type,
        includeAll: listState.includeAll,
        pageNumber: listState.pageNumber,
        pageSize: listState.pageSize,
      }),
    enabled: moduleStatus.isOperational && canSearch,
  });

  const items = useMemo(() => deletedObjectsQuery.data?.items ?? [], [deletedObjectsQuery.data]);

  const columns = useMemo(
    () =>
      createAdDeletedObjectColumns({
        t,
        onDetail: (item) => {
          navigate(buildAdDeletedObjectDetailPath(item.id), {
            state: buildAdDeletedObjectsListReturnState(listPath),
          });
        },
      }),
    [t, navigate, listPath],
  );

  const table = useServerDataTable({
    data: items,
    columns,
    pageCount: deletedObjectsQuery.data?.hasNextPage
      ? listState.pageNumber + 1
      : listState.pageNumber,
    pageIndex: listState.pageNumber - 1,
    pageSize: listState.pageSize,
  });

  const handleRefresh = () => {
    if (!canSearch) {
      return;
    }

    deletedObjectsQuery.refetch();
  };

  if (moduleStatus.isOperational && deletedObjectsQuery.isError) {
    const routeState = createApiErrorRouteState(deletedObjectsQuery.error, {
      fromPath: listPath,
      retryPath: listPath,
      sourceLabel: t("adManagement:deletedObjects.list.pageTitle"),
    });
    return <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />;
  }

  return (
    <AdManagementModuleStateGuard>
      <section className="space-y-4">
        <SectionCard
          title={t("adManagement:deletedObjects.list.pageTitle")}
          description={t("adManagement:deletedObjects.list.pageDescription")}
        >
          <div className="space-y-4">
            <AdDeletedObjectsSearchToolbar
              listState={listState}
              canSearch={canSearch}
              activeFilterCount={activeFilterCount}
              onListStateChange={updateListState}
              onClearFilters={clearListState}
              onRefresh={handleRefresh}
            />

            {!canSearch ? (
              <EmptyState
                title={t("adManagement:deletedObjects.empty.title")}
                description={t("adManagement:deletedObjects.empty.searchRequired")}
              />
            ) : null}

            {canSearch && deletedObjectsQuery.isLoading ? <LoadingState /> : null}

            {canSearch && deletedObjectsQuery.isSuccess && !items.length ? (
              <EmptyState
                title={t("adManagement:deletedObjects.empty.title")}
                description={t("adManagement:deletedObjects.empty.description")}
              />
            ) : null}

            {canSearch && items.length > 0 ? (
              <DataTable
                table={table}
                footer={
                  deletedObjectsQuery.data ? (
                    <DataTablePagination
                      mode="directory"
                      pageNumber={deletedObjectsQuery.data.pageNumber}
                      pageSize={deletedObjectsQuery.data.pageSize}
                      hasNextPage={deletedObjectsQuery.data.hasNextPage}
                      onPageChange={(nextPage) => {
                        updateListState({ pageNumber: nextPage });
                      }}
                      onPageSizeChange={(nextPageSize) => {
                        updateListState({
                          pageSize: nextPageSize,
                          pageNumber: AD_DELETED_OBJECTS_LIST_DEFAULTS.pageNumber,
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
