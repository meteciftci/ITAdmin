import { useCallback, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate, useNavigate, useSearchParams } from "react-router-dom";
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
import { appendReturnTo } from "@/features/ad-management/ad-return-path";
import {
  AD_USERS_LIST_DEFAULTS,
  buildAdUsersListPath,
  buildAdUsersListSearchParams,
  parseAdUsersListQuery,
} from "@/features/ad-management/ad-users-list-query";
import { createAdUserColumns } from "@/features/ad-management/ad-users-columns";
import {
  AD_MANAGEMENT_USERS_QUERY_KEY,
  disableAdUser,
  enableAdUser,
  getAdUserById,
  getAdUsers,
  invalidateAdManagementUserQueries,
  unlockAdUser,
} from "@/features/ad-management/api";
import { AdManagementModuleStateGuard } from "@/features/ad-management/components/AdManagementModuleStateGuard";
import { AdUserDetailDialog } from "@/features/ad-management/components/AdUserDetailDialog";
import { AdUsersSearchToolbar } from "@/features/ad-management/components/AdUsersSearchToolbar";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
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
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const listState = useMemo(
    () => parseAdUsersListQuery(searchParams),
    [searchParams],
  );
  const listPath = useMemo(() => buildAdUsersListPath(listState), [listState]);

  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<AccountConfirmTarget | null>(null);

  const normalizedSearch = listState.q.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;
  const effectiveSearch = canSearch ? normalizedSearch : undefined;

  const usersQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_USERS_QUERY_KEY,
      "list",
      effectiveSearch,
      listState.status,
      listState.page,
      listState.pageSize,
    ],
    queryFn: () =>
      getAdUsers({
        search: effectiveSearch,
        status: listState.status,
        pageNumber: listState.page,
        pageSize: listState.pageSize,
      }),
    enabled: moduleStatus.isOperational && canSearch,
  });

  const userDetailQuery = useQuery({
    queryKey: [...AD_MANAGEMENT_USERS_QUERY_KEY, "detail", selectedUserId],
    queryFn: () => getAdUserById(selectedUserId!),
    enabled: moduleStatus.isOperational && Boolean(selectedUserId),
    staleTime: 0,
    refetchOnMount: "always",
  });

  const users = useMemo(() => usersQuery.data?.items ?? [], [usersQuery.data]);

  const updateListQuery = useCallback(
    (patch: Partial<typeof listState>) => {
      const nextState = {
        ...listState,
        ...patch,
      };
      setSearchParams(buildAdUsersListSearchParams(nextState), { replace: true });
    },
    [listState, setSearchParams],
  );

  const columns = useMemo(
    () =>
      createAdUserColumns({
        t,
        canManageGroups,
        canUpdateUser,
        canDisableUser,
        canEnableUser,
        canUnlockUser,
        onDetail: (user) => setSelectedUserId(user.id),
        onEdit: (user) => {
          navigate(appendReturnTo(`/ad-management/users/${user.id}/edit`, listPath));
        },
        onManageGroups: (user) => {
          navigate(appendReturnTo(`/ad-management/users/${user.id}/groups`, listPath));
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
      navigate,
      listPath,
    ],
  );

  const table = useServerDataTable({
    data: users,
    columns,
    pageCount: usersQuery.data?.hasNextPage ? listState.page + 1 : listState.page,
    pageIndex: listState.page - 1,
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
      if (selectedUserId === variables.userId) {
        await queryClient.invalidateQueries({
          queryKey: [...AD_MANAGEMENT_USERS_QUERY_KEY, "detail", variables.userId],
        });
      }

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
    if (selectedUserId) {
      userDetailQuery.refetch();
    }
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
            onListStateChange={updateListQuery}
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
                      updateListQuery({ page: nextPage });
                    }}
                    onPageSizeChange={(nextPageSize) => {
                      updateListQuery({
                        pageSize: nextPageSize,
                        page: AD_USERS_LIST_DEFAULTS.page,
                      });
                    }}
                  />
                ) : null
              }
            />
          ) : null}
        </div>
      </SectionCard>

      <AdUserDetailDialog
        open={Boolean(selectedUserId)}
        user={userDetailQuery.data ?? null}
        returnTo={listPath}
        canUpdateUser={canUpdateUser}
        onOpenChange={(open) => {
          if (!open) {
            setSelectedUserId(null);
          }
        }}
      />

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
