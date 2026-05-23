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
import { DataToolbar } from "@/components/common/DataToolbar";
import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { RowActions } from "@/components/common/RowActions";
import { SectionCard } from "@/components/common/SectionCard";
import { Select } from "@/components/ui/select";
import {
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import {
  AD_MANAGEMENT_USERS_QUERY_KEY,
  disableAdUser,
  enableAdUser,
  getAdUserById,
  getAdUsers,
  invalidateAdManagementUserQueries,
  unlockAdUser,
} from "@/features/ad-management/api";
import { AdAccountStatusBadge } from "@/features/ad-management/components/AdAccountStatusBadge";
import { AdDirectoryPagination } from "@/features/ad-management/components/AdDirectoryPagination";
import { AdLockStatusBadge } from "@/features/ad-management/components/AdLockStatusBadge";
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

  const openDetail = (user: AdUserListItem) => {
    setSelectedUserId(user.id);
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
          <DataToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("adManagement:users.searchPlaceholder")}
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
          >
            <div className="flex flex-wrap items-center gap-2">
              <Select
                value={statusFilter}
                onChange={(event) => {
                  setStatusFilter(event.target.value as AdUserStatusFilter);
                  setPageNumber(1);
                }}
                className="w-full sm:w-40"
              >
                <option value="active">{t("adManagement:users.filters.active")}</option>
                <option value="disabled">{t("adManagement:users.filters.disabled")}</option>
                <option value="all">{t("adManagement:users.filters.all")}</option>
              </Select>
            </div>
          </DataToolbar>

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
            <div className="overflow-x-auto rounded-lg border bg-card">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/50 text-left">
                  <tr>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.displayName")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.username")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.upn")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.email")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.department")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.status")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.locked")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.lastLogon")}
                    </th>
                    <th className="px-3 py-2 font-medium">
                      {t("adManagement:users.table.actions")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((user) => (
                    <tr key={user.id} className="border-t align-top hover:bg-muted/20">
                      <td className="px-3 py-2">{user.displayName || "-"}</td>
                      <td className="px-3 py-2">{user.samAccountName || "-"}</td>
                      <td className="max-w-48 truncate px-3 py-2">
                        {user.userPrincipalName || "-"}
                      </td>
                      <td className="max-w-48 truncate px-3 py-2">{user.mail || "-"}</td>
                      <td className="px-3 py-2">{user.department || "-"}</td>
                      <td className="px-3 py-2">
                        <AdAccountStatusBadge isEnabled={user.isEnabled} />
                      </td>
                      <td className="px-3 py-2">
                        <AdLockStatusBadge isLockedOut={user.isLockedOut} />
                      </td>
                      <td className="px-3 py-2">
                        <DateTimeText value={user.lastLogonAt} />
                      </td>
                      <td className="px-3 py-2">
                        <RowActions>
                          <DropdownMenuLabel>{t("common:actions.actions")}</DropdownMenuLabel>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem onClick={() => openDetail(user)}>
                            {t("adManagement:users.actions.detail")}
                          </DropdownMenuItem>
                          {canManageGroups ? (
                            <DropdownMenuItem
                              onClick={() =>
                                navigate(`/ad-management/users/${user.id}/groups`)
                              }
                            >
                              {t("adManagement:users.actions.manageGroups")}
                            </DropdownMenuItem>
                          ) : null}
                          {canDisableUser && user.isEnabled ? (
                            <DropdownMenuItem
                              onClick={() =>
                                setConfirmTarget({ user, action: "disable" })
                              }
                            >
                              {t("adManagement:users.actions.disable")}
                            </DropdownMenuItem>
                          ) : null}
                          {canEnableUser && !user.isEnabled ? (
                            <DropdownMenuItem
                              onClick={() =>
                                setConfirmTarget({ user, action: "enable" })
                              }
                            >
                              {t("adManagement:users.actions.enable")}
                            </DropdownMenuItem>
                          ) : null}
                          {canUnlockUser && user.isLockedOut ? (
                            <DropdownMenuItem
                              onClick={() =>
                                setConfirmTarget({ user, action: "unlock" })
                              }
                            >
                              {t("adManagement:users.actions.unlock")}
                            </DropdownMenuItem>
                          ) : null}
                        </RowActions>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {usersQuery.data ? (
                <AdDirectoryPagination
                  pageNumber={usersQuery.data.pageNumber}
                  pageSize={usersQuery.data.pageSize}
                  hasNextPage={usersQuery.data.hasNextPage}
                  onPageChange={setPageNumber}
                  onPageSizeChange={(nextPageSize) => {
                    setPageSize(nextPageSize);
                    setPageNumber(1);
                  }}
                />
              ) : null}
            </div>
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
