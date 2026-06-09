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
import { AD_GROUPS_LIST_DEFAULTS } from "@/features/ad-management/ad-groups-list-query";
import { createAdGroupColumns } from "@/features/ad-management/ad-groups-columns";
import { buildAdGroupsListReturnState } from "@/features/ad-management/ad-groups-return-path";
import {
  buildAdGroupDetailPath,
  buildAdGroupEditPath,
  buildAdGroupMembersPath,
  buildAdGroupMoveOuPath,
} from "@/features/ad-management/ad-group-detail-path";
import {
  AD_MANAGEMENT_GROUPS_QUERY_KEY,
  getAdGroups,
} from "@/features/ad-management/api";
import { AdDeleteGroupConfirmDialog } from "@/features/ad-management/components/AdDeleteGroupConfirmDialog";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdGroupsSearchToolbar } from "@/features/ad-management/components/AdGroupsSearchToolbar";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { useAdGroupListState } from "@/features/ad-management/use-ad-group-list-state";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

const MIN_SEARCH_LENGTH = 2;

export function AdGroupsPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const currentUser = useAuthStore((state) => state.user);
  const canCreateGroup = canAccess(currentUser, "AdManagement.Groups.Create");
  const canUpdateGroup = canAccess(currentUser, "AdManagement.Groups.Update");
  const canManageMembers = canAccess(currentUser, "AdManagement.Groups.ManageMembers");
  const canMoveOu = canAccess(currentUser, "AdManagement.Groups.MoveOu");
  const canDeleteGroup = canAccess(currentUser, "AdManagement.Groups.Delete");
  const [deleteGroupId, setDeleteGroupId] = useState<string | null>(null);
  const moduleStatus = useAdManagementModuleStatus();
  const navigate = useNavigate();
  const { listState, listPath, updateListState, clearListState } = useAdGroupListState();

  const normalizedSearch = listState.search.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;
  const effectiveSearch = canSearch ? normalizedSearch : undefined;

  const groupsQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_GROUPS_QUERY_KEY,
      "list",
      effectiveSearch,
      listState.pageNumber,
      listState.pageSize,
    ],
    queryFn: () =>
      getAdGroups({
        search: effectiveSearch,
        page: listState.pageNumber,
        pageSize: listState.pageSize,
      }),
    enabled: moduleStatus.isOperational && canSearch,
  });

  const groups = useMemo(() => groupsQuery.data?.items ?? [], [groupsQuery.data]);

  const columns = useMemo(
    () =>
      createAdGroupColumns({
        t,
        canUpdateGroup,
        canManageMembers,
        canMoveOu,
        canDeleteGroup,
        onDetail: (group) => {
          navigate(buildAdGroupDetailPath(group.id), {
            state: buildAdGroupsListReturnState(),
          });
        },
        onEdit: (group) => {
          navigate(buildAdGroupEditPath(group.id), {
            state: buildAdGroupsListReturnState(),
          });
        },
        onManageMembers: (group) => {
          navigate(buildAdGroupMembersPath(group.id), {
            state: buildAdGroupsListReturnState(),
          });
        },
        onMoveOu: (group) => {
          navigate(buildAdGroupMoveOuPath(group.id), {
            state: buildAdGroupsListReturnState(),
          });
        },
        onDelete: (group) => {
          setDeleteGroupId(group.id);
        },
      }),
    [t, navigate, canUpdateGroup, canManageMembers, canMoveOu, canDeleteGroup],
  );

  const table = useServerDataTable({
    data: groups,
    columns,
    pageCount: groupsQuery.data?.hasNextPage
      ? listState.pageNumber + 1
      : listState.pageNumber,
    pageIndex: listState.pageNumber - 1,
    pageSize: listState.pageSize,
  });

  const handleRefresh = () => {
    if (!canSearch) {
      return;
    }

    groupsQuery.refetch();
  };

  if (moduleStatus.isOperational && groupsQuery.isError) {
    const routeState = createApiErrorRouteState(groupsQuery.error, {
      fromPath: listPath,
      retryPath: listPath,
      sourceLabel: t("adManagement:groups.title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <AdManagementModuleStateGuard>
      <section className="space-y-4">
        <SectionCard
          title={t("adManagement:groups.title")}
          description={t("adManagement:groups.description")}
        >
          <div className="space-y-4">
            <AdGroupsSearchToolbar
              listState={listState}
              canSearch={canSearch}
              canCreateGroup={canCreateGroup}
              onListStateChange={updateListState}
              onClearFilters={clearListState}
              onRefresh={handleRefresh}
            />

            {!canSearch ? (
              <EmptyState
                title={t("adManagement:groups.empty.searchRequiredTitle")}
                description={t("adManagement:groups.empty.searchRequired")}
              />
            ) : null}

            {canSearch && groupsQuery.isLoading ? <LoadingState /> : null}

            {canSearch && groupsQuery.isSuccess && !groups.length ? (
              <EmptyState
                title={t("adManagement:groups.empty.title")}
                description={t("adManagement:groups.empty.description")}
              />
            ) : null}

            {canSearch && groups.length > 0 ? (
              <DataTable
                table={table}
                footer={
                  groupsQuery.data ? (
                    <DataTablePagination
                      mode="directory"
                      pageNumber={groupsQuery.data.pageNumber}
                      pageSize={groupsQuery.data.pageSize}
                      hasNextPage={groupsQuery.data.hasNextPage}
                      onPageChange={(nextPage) => {
                        updateListState({ pageNumber: nextPage });
                      }}
                      onPageSizeChange={(nextPageSize) => {
                        updateListState({
                          pageSize: nextPageSize,
                          pageNumber: AD_GROUPS_LIST_DEFAULTS.pageNumber,
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

      <AdDeleteGroupConfirmDialog
        open={deleteGroupId !== null}
        groupId={deleteGroupId}
        onOpenChange={(open) => {
          if (!open) {
            setDeleteGroupId(null);
          }
        }}
        onDeleted={() => {
          setDeleteGroupId(null);
          handleRefresh();
        }}
      />
    </AdManagementModuleStateGuard>
  );
}
