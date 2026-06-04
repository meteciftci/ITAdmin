import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DataTable, DataTablePagination } from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { AD_USERS_LIST_DEFAULTS } from "@/features/ad-management/ad-users-list-query";
import { createAdUserColumns } from "@/features/ad-management/ad-users-columns";
import { buildAdUsersListReturnState } from "@/features/ad-management/ad-return-path";
import { buildAdUserDetailPath } from "@/features/ad-management/ad-user-detail-path";
import {
  AD_MANAGEMENT_USERS_QUERY_KEY,
  disableAdUser,
  enableAdUser,
  getAdUsers,
  invalidateAdManagementUserQueries,
  unlockAdUser,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdUsersSearchToolbar } from "@/features/ad-management/components/AdUsersSearchToolbar";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { useAdUserListState } from "@/features/ad-management/use-ad-user-list-state";
import type { AdUserAccountConfirmAction, AdUserListItem } from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

type AccountConfirmTarget = {
  user: AdUserListItem;
  action: AdUserAccountConfirmAction;
};

const MIN_SEARCH_LENGTH = 2;

export function AdUsersPage() {
  const { t } = useTranslation(["adManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();
  const canCreateUser = canAccess(currentUser, "AdManagement.Users.Create");
  const canUpdateUser = canAccess(currentUser, "AdManagement.Users.Update");
  const canEnableUser = canAccess(currentUser, "AdManagement.Users.Enable");
  const canDisableUser = canAccess(currentUser, "AdManagement.Users.Disable");
  const canUnlockUser = canAccess(currentUser, "AdManagement.Users.Unlock");
  const canManageGroups = canAccess(currentUser, "AdManagement.Users.Groups.View");
  const canMoveOu = canAccess(currentUser, "AdManagement.Users.MoveOu");
  const navigate = useNavigate();
  const { listState, listPath, updateListState, clearListState } = useAdUserListState();

  const [confirmTarget, setConfirmTarget] = useState<AccountConfirmTarget | null>(null);

  const normalizedSearch = listState.search.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;
  const effectiveSearch = canSearch ? normalizedSearch : undefined;

  const usersQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_USERS_QUERY_KEY,
      "list",
      effectiveSearch,
      listState.status,
      listState.pageNumber,
      listState.pageSize,
    ],
    queryFn: () =>
      getAdUsers({
        search: effectiveSearch,
        status: listState.status,
        pageNumber: listState.pageNumber,
        pageSize: listState.pageSize,
      }),
    enabled: moduleStatus.isOperational && canSearch,
  });

  const users = useMemo(() => usersQuery.data?.items ?? [], [usersQuery.data]);

  const columns = useMemo(
    () =>
      createAdUserColumns({
        t,
        canManageGroups,
        canUpdateUser,
        canDisableUser,
        canEnableUser,
        canUnlockUser,
        canMoveOu,
        onDetail: (user) => {
          navigate(buildAdUserDetailPath(user.id));
        },
        onEdit: (user) => {
          navigate(`/ad-management/users/${user.id}/edit`);
        },
        onManageGroups: (user) => {
          navigate(`/ad-management/users/${user.id}/groups`);
        },
        onMoveOu: (user) => {
          navigate(`/ad-management/users/${user.id}/move-ou`, {
            state: buildAdUsersListReturnState(),
          });
        },
        onDisable: (user) => setConfirmTarget({ user, action: "disable" }),
        onEnable: (user) => setConfirmTarget({ user, action: "enable" }),
        onUnlock: (user) => setConfirmTarget({ user, action: "unlock" }),
      }),
    [
      t,
      canManageGroups,
      canUpdateUser,
      canDisableUser,
      canEnableUser,
      canUnlockUser,
      canMoveOu,
      navigate,
    ],
  );

  const table = useServerDataTable({
    data: users,
    columns,
    pageCount: usersQuery.data?.hasNextPage
      ? listState.pageNumber + 1
      : listState.pageNumber,
    pageIndex: listState.pageNumber - 1,
    pageSize: listState.pageSize,
  });

  const activeFilterCount = listState.status !== "all" ? 1 : 0;

  const accountOperationMutation = useMutation({
    mutationFn: async ({
      userId,
      action,
    }: {
      userId: string;
      action: AdUserAccountConfirmAction;
    }) => {
      if (action === "enable") {
        return enableAdUser(userId);
      }

      if (action === "disable") {
        return disableAdUser(userId);
      }

      return unlockAdUser(userId);
    },
    onSuccess: async (response, variables) => {
      if (!response.success) {
        toast.error(response.message || t("adManagement:users.messages.operationFailed"));
        return;
      }

      await invalidateAdManagementUserQueries(queryClient);

      const message =
        variables.action === "enable"
          ? t("adManagement:users.messages.enabled")
          : variables.action === "disable"
            ? t("adManagement:users.messages.disabled")
            : t("adManagement:users.messages.unlocked");
      toast.success(response.message || message);
      setConfirmTarget(null);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("adManagement:users.messages.operationFailed")),
      );
    },
  });

  const handleRefresh = () => {
    if (!canSearch) {
      return;
    }

    usersQuery.refetch();
  };

  const confirmCopy = useMemo(() => {
    if (!confirmTarget) {
      return { title: "", description: "", variant: "default" as const };
    }

    if (confirmTarget.action === "disable") {
      return {
        title: t("adManagement:users.confirm.disableTitle"),
        description: t("adManagement:users.confirm.disableDescription"),
        variant: "danger" as const,
      };
    }

    if (confirmTarget.action === "enable") {
      return {
        title: t("adManagement:users.confirm.enableTitle"),
        description: t("adManagement:users.confirm.enableDescription"),
        variant: "default" as const,
      };
    }

    return {
      title: t("adManagement:users.confirm.unlockTitle"),
      description: t("adManagement:users.confirm.unlockDescription"),
      variant: "default" as const,
    };
  }, [confirmTarget, t]);

  if (moduleStatus.isOperational && usersQuery.isError) {
    const routeState = createApiErrorRouteState(usersQuery.error, {
      fromPath: listPath,
      retryPath: listPath,
      sourceLabel: t("adManagement:users.title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <AdManagementModuleStateGuard>
    <section className="space-y-4">
      <SectionCard
        title={t("adManagement:users.title")}
        description={t("adManagement:users.description")}
      >
        <div className="space-y-4">
          <AdUsersSearchToolbar
            listState={listState}
            canSearch={canSearch}
            canCreateUser={canCreateUser}
            activeFilterCount={activeFilterCount}
            onListStateChange={updateListState}
            onClearFilters={clearListState}
            onRefresh={handleRefresh}
          />

          {!canSearch ? (
            <EmptyState
              title={t("adManagement:users.empty.searchRequiredTitle")}
              description={t("adManagement:users.empty.searchRequired")}
            />
          ) : null}

          {canSearch && usersQuery.isLoading ? <LoadingState /> : null}

          {canSearch && usersQuery.isSuccess && !users.length ? (
            <EmptyState
              title={t("adManagement:users.empty.title")}
              description={t("adManagement:users.empty.description")}
            />
          ) : null}

          {canSearch && users.length > 0 ? (
            <DataTable
              table={table}
              footer={
                usersQuery.data ? (
                  <DataTablePagination
                    mode="directory"
                    pageNumber={usersQuery.data.pageNumber}
                    pageSize={usersQuery.data.pageSize}
                    hasNextPage={usersQuery.data.hasNextPage}
                    onPageChange={(nextPage) => {
                      updateListState({ pageNumber: nextPage });
                    }}
                    onPageSizeChange={(nextPageSize) => {
                      updateListState({
                        pageSize: nextPageSize,
                        pageNumber: AD_USERS_LIST_DEFAULTS.pageNumber,
                      });
                    }}
                  />
                ) : null
              }
            />
          ) : null}
        </div>
      </SectionCard>

      <ConfirmDialog
        open={Boolean(confirmTarget)}
        title={confirmCopy.title}
        description={confirmCopy.description}
        confirmText={t("common:actions.confirm")}
        cancelText={t("common:actions.cancel")}
        variant={confirmCopy.variant}
        isLoading={accountOperationMutation.isPending}
        onOpenChange={(open) => {
          if (!open) {
            setConfirmTarget(null);
          }
        }}
        onConfirm={() => {
          if (!confirmTarget) {
            return;
          }

          accountOperationMutation.mutate({
            userId: confirmTarget.user.id,
            action: confirmTarget.action,
          });
        }}
      />

    </section>
    </AdManagementModuleStateGuard>
  );
}
