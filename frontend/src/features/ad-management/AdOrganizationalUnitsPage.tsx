import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { DataTable, DataTablePagination } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS } from "@/features/ad-management/ad-ous-list-query";
import { createAdOrganizationalUnitColumns } from "@/features/ad-management/ad-ous-columns";
import { buildAdOrganizationalUnitsListReturnState } from "@/features/ad-management/ad-ous-return-path";
import {
  buildAdOrganizationalUnitCreatePath,
  buildAdOrganizationalUnitDetailPath,
  buildAdOrganizationalUnitMovePath,
  buildAdOrganizationalUnitRenamePath,
} from "@/features/ad-management/ad-ou-detail-path";
import {
  AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY,
  getAdOrganizationalUnits,
} from "@/features/ad-management/api";
import { AdDeleteOrganizationalUnitDialog } from "@/features/ad-management/components/AdOrganizationalUnitDialogs";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdOrganizationalUnitsSearchToolbar } from "@/features/ad-management/components/AdOrganizationalUnitsSearchToolbar";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { useAdOrganizationalUnitListState } from "@/features/ad-management/use-ad-ou-list-state";
import type { AdOrganizationalUnitManageListItem } from "@/features/ad-management/types";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { PermissionCodes } from "@/lib/permission-codes";

const MIN_SEARCH_LENGTH = 2;

export function AdOrganizationalUnitsPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const currentUser = useAuthStore((state) => state.user);
  const canCreate = canAccess(currentUser, PermissionCodes.AdManagement.OrganizationalUnits.Create);
  const canUpdate = canAccess(currentUser, PermissionCodes.AdManagement.OrganizationalUnits.Update);
  const canMove = canAccess(currentUser, PermissionCodes.AdManagement.OrganizationalUnits.Move);
  const canDelete = canAccess(currentUser, PermissionCodes.AdManagement.OrganizationalUnits.Delete);
  const moduleStatus = useAdManagementModuleStatus();
  const navigate = useNavigate();
  const { listState, listPath, updateListState, clearListState } = useAdOrganizationalUnitListState();

  const [deleteTarget, setDeleteTarget] = useState<AdOrganizationalUnitManageListItem | null>(null);

  const emptyText = t("common:notAvailable");

  const normalizedSearch = listState.search.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;
  const effectiveSearch = canSearch ? normalizedSearch : undefined;

  const organizationalUnitsQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_ORGANIZATIONAL_UNITS_QUERY_KEY,
      "list",
      effectiveSearch,
      listState.pageNumber,
      listState.pageSize,
    ],
    queryFn: () =>
      getAdOrganizationalUnits({
        search: effectiveSearch,
        pageNumber: listState.pageNumber,
        pageSize: listState.pageSize,
      }),
    enabled: moduleStatus.isOperational && canSearch,
  });

  const organizationalUnits = useMemo(
    () => (canSearch ? organizationalUnitsQuery.data?.items ?? [] : []),
    [canSearch, organizationalUnitsQuery.data],
  );

  const columns = useMemo(
    () =>
      createAdOrganizationalUnitColumns({
        t,
        emptyText,
        canCreate,
        canUpdate,
        canMove,
        canDelete,
        onDetail: (item) => {
          navigate(buildAdOrganizationalUnitDetailPath(item.objectGuid), {
            state: buildAdOrganizationalUnitsListReturnState(),
          });
        },
        onCreateChild: (item) => {
          navigate(buildAdOrganizationalUnitCreatePath(item.distinguishedName), {
            state: buildAdOrganizationalUnitsListReturnState(),
          });
        },
        onRename: (item) => {
          navigate(buildAdOrganizationalUnitRenamePath(item.objectGuid), {
            state: buildAdOrganizationalUnitsListReturnState(),
          });
        },
        onMove: (item) => {
          navigate(buildAdOrganizationalUnitMovePath(item.objectGuid), {
            state: buildAdOrganizationalUnitsListReturnState(),
          });
        },
        onDelete: setDeleteTarget,
      }),
    [t, emptyText, navigate, canCreate, canUpdate, canMove, canDelete],
  );

  const table = useServerDataTable({
    data: organizationalUnits,
    columns,
    pageCount: organizationalUnitsQuery.data?.hasNextPage
      ? listState.pageNumber + 1
      : listState.pageNumber,
    pageIndex: listState.pageNumber - 1,
    pageSize: listState.pageSize,
  });

  const handleRefresh = () => {
    if (!canSearch) {
      return;
    }

    organizationalUnitsQuery.refetch();
  };

  if (moduleStatus.isOperational && organizationalUnitsQuery.isError) {
    const routeState = createApiErrorRouteState(organizationalUnitsQuery.error, {
      fromPath: listPath,
      retryPath: listPath,
      sourceLabel: t("adManagement:organizationalUnits.title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <AdManagementModuleStateGuard>
      <section className="space-y-4">
        <SectionCard
          title={t("adManagement:organizationalUnits.title")}
          description={t("adManagement:organizationalUnits.description")}
        >
          <div className="space-y-4">
            <AdOrganizationalUnitsSearchToolbar
              listState={listState}
              canSearch={canSearch}
              onListStateChange={updateListState}
              onClearFilters={clearListState}
              onRefresh={handleRefresh}
              canCreate={canCreate}
            />

            {!canSearch ? (
              <EmptyState
                title={t("adManagement:organizationalUnits.empty.searchRequiredTitle")}
                description={t("adManagement:organizationalUnits.empty.searchRequired")}
              />
            ) : null}

            {canSearch && organizationalUnitsQuery.isLoading ? <LoadingState /> : null}

            {canSearch && organizationalUnitsQuery.isSuccess && !organizationalUnits.length ? (
              <EmptyState title={t("adManagement:organizationalUnits.empty.title")} />
            ) : null}

            {canSearch && organizationalUnits.length > 0 ? (
              <DataTable
                table={table}
                footer={
                  organizationalUnitsQuery.data ? (
                    <DataTablePagination
                      mode="directory"
                      pageNumber={organizationalUnitsQuery.data.pageNumber}
                      pageSize={organizationalUnitsQuery.data.pageSize}
                      hasNextPage={organizationalUnitsQuery.data.hasNextPage}
                      onPageChange={(nextPage) => {
                        updateListState({ pageNumber: nextPage });
                      }}
                      onPageSizeChange={(nextPageSize) => {
                        updateListState({
                          pageSize: nextPageSize,
                          pageNumber: AD_ORGANIZATIONAL_UNITS_LIST_DEFAULTS.pageNumber,
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

      <AdDeleteOrganizationalUnitDialog
        open={Boolean(deleteTarget)}
        organizationalUnit={deleteTarget}
        onOpenChange={(open) => {
          if (!open) {
            setDeleteTarget(null);
          }
        }}
      />
    </AdManagementModuleStateGuard>
  );
}
