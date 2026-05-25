import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { useAuthStore } from "@/features/auth/auth-store";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { canAccess } from "@/lib/permissions";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Select } from "@/components/ui/select";
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
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import type {
  AdUserAccountConfirmAction,
  AdUserListItem,
  AdUserStatusFilter,
} from "@/features/ad-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { getApiErrorMessage } from "@/lib/api-error";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { cn } from "@/lib/utils";

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
  const canEnableUser = canAccess(currentUser, "AdManagement.Users.Enable");
  const canDisableUser = canAccess(currentUser, "AdManagement.Users.Disable");
  const canUnlockUser = canAccess(currentUser, "AdManagement.Users.Unlock");
  const canManageGroups = canAccess(currentUser, "AdManagement.Users.Groups.View");
  const navigate = useNavigate();

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<AdUserStatusFilter>("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<AccountConfirmTarget | null>(null);

  const debouncedSearch = useDebouncedValue(search, 400);
  const normalizedSearch = debouncedSearch.trim();
  const canSearch = normalizedSearch.length >= MIN_SEARCH_LENGTH;
  const effectiveSearch = canSearch ? normalizedSearch : undefined;

  const usersQuery = useQuery({
    queryKey: [
      ...AD_MANAGEMENT_USERS_QUERY_KEY,
      "list",
      effectiveSearch,
      statusFilter,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getAdUsers({
        search: effectiveSearch,
        status: statusFilter,
        pageNumber,
        pageSize,
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

  const columns = useMemo(
    () =>
      createAdUserColumns({
        t,
        canManageGroups,
        canDisableUser,
        canEnableUser,
        canUnlockUser,
        onDetail: (user) => setSelectedUserId(user.id),
        onManageGroups: (user) => navigate(`/ad-management/users/${user.id}/groups`),
        onDisable: (user) => setConfirmTarget({ user, action: "disable" }),
        onEnable: (user) => setConfirmTarget({ user, action: "enable" }),
        onUnlock: (user) => setConfirmTarget({ user, action: "unlock" }),
      }),
    [
      t,
      canManageGroups,
      canDisableUser,
      canEnableUser,
      canUnlockUser,
      navigate,
    ],
  );

  const table = useServerDataTable({
    data: users,
    columns,
    pageCount: usersQuery.data?.hasNextPage ? pageNumber + 1 : pageNumber,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  const activeFilterCount = statusFilter !== "all" ? 1 : 0;

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

  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPageNumber(1);
  };

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
      fromPath: "/ad-management/users",
      retryPath: "/ad-management/users",
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
          <DataTableToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("adManagement:users.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setStatusFilter("all");
              setPageNumber(1);
            }}
            filterContent={
              <Select
                value={statusFilter}
                onChange={(event) => {
                  setStatusFilter(event.target.value as AdUserStatusFilter);
                  setPageNumber(1);
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
                <Button variant="outline" onClick={handleRefresh} disabled={!canSearch}>
                  {t("common:actions.refresh")}
                </Button>
              </>
            }
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
                    onPageChange={setPageNumber}
                    onPageSizeChange={(nextPageSize) => {
                      setPageSize(nextPageSize);
                      setPageNumber(1);
                    }}
                    summaryText={t("adManagement:users.pagination.page", {
                      pageNumber: usersQuery.data.pageNumber,
                    })}
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
