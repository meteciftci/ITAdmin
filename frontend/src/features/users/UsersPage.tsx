import { useMemo, useState } from "react";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { Button } from "@/components/ui/button";
import { DateTimeText } from "@/components/common/DateTimeText";
import { DataToolbar } from "@/components/common/DataToolbar";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { RowActions } from "@/components/common/RowActions";
import { RoleBadgeList } from "@/components/common/RoleBadgeList";
import { SectionCard } from "@/components/common/SectionCard";
import { StatusBadge } from "@/components/common/StatusBadge";
import { TablePagination } from "@/components/common/TablePagination";
import {
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { Select } from "@/components/ui/select";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { getUserById, getUsers, updateUserStatus } from "@/features/users/api";
import { AddUserDialog } from "@/features/users/AddUserDialog";
import { AssignRolesDialog } from "@/features/users/AssignRolesDialog";
import { UserDetailDialog } from "@/features/users/UserDetailDialog";
import type { UserListItem } from "@/features/users/types";
import { getApiErrorMessage } from "@/lib/api-error";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

type StatusFilter = "active" | "passive" | "all";

export function UsersPage() {
  const { t } = useTranslation(["users", "common", "errors"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const canCreate = canAccess(currentUser, "Users.Create");
  const canUpdate = canAccess(currentUser, "Users.Update");
  const canAssignRoles = canAccess(currentUser, "Users.AssignRoles");

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [addUserOpen, setAddUserOpen] = useState(false);
  const [selectedUserForDetail, setSelectedUserForDetail] =
    useState<UserListItem | null>(null);
  const [selectedUserForRoles, setSelectedUserForRoles] =
    useState<UserListItem | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<UserListItem | null>(null);

  const debouncedSearch = useDebouncedValue(search, 400);
  const normalizedSearch = debouncedSearch.trim();
  const effectiveSearch =
    normalizedSearch.length >= 3 ? normalizedSearch : undefined;

  const usersQuery = useQuery({
    queryKey: ["users", "list", effectiveSearch, statusFilter, pageNumber, pageSize],
    queryFn: () =>
      getUsers({
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

  const userDetailQuery = useQuery({
    queryKey: ["users", "detail", selectedUserForDetail?.id],
    queryFn: () => getUserById(selectedUserForDetail!.id),
    enabled: Boolean(selectedUserForDetail?.id),
  });

  const updateUserStatusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      updateUserStatus(id, { isActive }),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["users", "list"] });
      if (selectedUserForDetail?.id) {
        queryClient.invalidateQueries({
          queryKey: ["users", "detail", selectedUserForDetail.id],
        });
      }
      toast.success(
        variables.isActive
          ? t("users:messages.userActivated")
          : t("users:messages.userDeactivated"),
      );
      setConfirmTarget(null);
    },
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, t("users:messages.statusUpdated")),
      );
    },
  });

  const users = useMemo(() => usersQuery.data?.items ?? [], [usersQuery.data]);

  const handleRefresh = () => {
    usersQuery.refetch();
    if (selectedUserForDetail?.id) {
      userDetailQuery.refetch();
    }
  };

  const handleToggleStatus = (user: UserListItem) => setConfirmTarget(user);
  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPageNumber(1);
  };

  const handleActionSuccess = (message?: string) => {
    queryClient.invalidateQueries({ queryKey: ["users", "list"] });
    if (selectedUserForDetail?.id) {
      queryClient.invalidateQueries({
        queryKey: ["users", "detail", selectedUserForDetail.id],
      });
    }
    if (message) {
      toast.success(message);
    }
  };

  if (usersQuery.isError) {
    const routeState = createApiErrorRouteState(usersQuery.error, {
      fromPath: "/users",
      retryPath: "/users",
      sourceLabel: t("users:sections.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <SectionCard title={t("users:sections.listTitle")}>
        <div className="space-y-4">
          <DataToolbar
            searchValue={search}
            onSearchChange={handleSearchChange}
            searchPlaceholder={t("users:search.placeholder")}
            actions={
              <>
                <Button variant="outline" onClick={handleRefresh}>
                  {t("common:actions.refresh")}
                </Button>
                {canCreate ? (
                  <Button onClick={() => setAddUserOpen(true)}>
                    {t("users:actions.addUser")}
                  </Button>
                ) : null}
              </>
            }
          >
            <div className="flex flex-wrap items-center gap-2">
              <Select
                value={statusFilter}
                onChange={(event) => {
                  setStatusFilter(event.target.value as StatusFilter);
                  setPageNumber(1);
                }}
                className="w-full sm:w-40"
              >
                <option value="active">{t("common:status.active")}</option>
                <option value="passive">{t("common:status.passive")}</option>
                <option value="all">{t("common:status.all")}</option>
              </Select>
            </div>
          </DataToolbar>

          {usersQuery.isLoading ? <LoadingState /> : null}

          {usersQuery.isSuccess && !users.length ? (
            <EmptyState
              title={t("users:empty.title")}
              description={t("users:empty.description")}
            />
          ) : null}

          {users.length ? (
            <div className="overflow-x-auto rounded-lg border bg-card">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/50 text-left">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t("users:table.displayName")}</th>
                    <th className="px-3 py-2 font-medium">{t("users:table.userName")}</th>
                    <th className="px-3 py-2 font-medium">{t("users:table.email")}</th>
                    <th className="px-3 py-2 font-medium">{t("users:table.nationalIdMasked")}</th>
                    <th className="px-3 py-2 font-medium">{t("users:table.roles")}</th>
                    <th className="px-3 py-2 font-medium">{t("users:table.status")}</th>
                    <th className="px-3 py-2 font-medium">{t("users:table.lastLogin")}</th>
                    <th className="px-3 py-2 font-medium">{t("users:table.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((user) => (
                    <tr key={user.id} className="border-t align-top hover:bg-muted/20">
                      <td className="px-3 py-2">{user.displayName || "-"}</td>
                      <td className="px-3 py-2">{user.userName}</td>
                      <td className="max-w-56 truncate px-3 py-2">{user.email || "-"}</td>
                      <td className="px-3 py-2">{user.nationalIdMasked || "-"}</td>
                      <td className="px-3 py-2">
                        <RoleBadgeList roles={user.roles} />
                      </td>
                      <td className="px-3 py-2">
                        <StatusBadge isActive={user.isActive} />
                      </td>
                      <td className="px-3 py-2">
                        <DateTimeText value={user.lastLoginAt} />
                      </td>
                      <td className="px-3 py-2">
                        <RowActions>
                          <DropdownMenuLabel>{t("common:actions.actions")}</DropdownMenuLabel>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem onClick={() => setSelectedUserForDetail(user)}>
                            {t("users:actions.detail")}
                          </DropdownMenuItem>
                          {canUpdate ? (
                            <DropdownMenuItem
                              disabled={updateUserStatusMutation.isPending}
                              onClick={() => handleToggleStatus(user)}
                            >
                              {user.isActive
                                ? t("users:actions.deactivate")
                                : t("users:actions.activate")}
                            </DropdownMenuItem>
                          ) : null}
                          {canAssignRoles ? (
                            <DropdownMenuItem onClick={() => setSelectedUserForRoles(user)}>
                              {t("users:actions.assignRoles")}
                            </DropdownMenuItem>
                          ) : null}
                        </RowActions>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {usersQuery.data && usersQuery.data.totalCount > 0 ? (
                <TablePagination
                  pageNumber={usersQuery.data.pageNumber}
                  pageSize={usersQuery.data.pageSize}
                  totalCount={usersQuery.data.totalCount}
                  totalPages={usersQuery.data.totalPages}
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

      <AddUserDialog
        open={addUserOpen}
        onOpenChange={setAddUserOpen}
        onCreated={() => handleActionSuccess(t("users:messages.userCreated"))}
      />

      <AssignRolesDialog
        key={selectedUserForRoles?.id ?? "assign-roles-closed"}
        open={Boolean(selectedUserForRoles)}
        user={selectedUserForRoles}
        onOpenChange={(open) => {
          if (!open) {
            setSelectedUserForRoles(null);
          }
        }}
        onUpdated={() => handleActionSuccess(t("users:messages.rolesUpdated"))}
      />

      <UserDetailDialog
        open={Boolean(selectedUserForDetail)}
        user={userDetailQuery.data ?? null}
        onOpenChange={(open) => {
          if (!open) setSelectedUserForDetail(null);
        }}
      />

      <ConfirmDialog
        open={Boolean(confirmTarget)}
        title={
          confirmTarget?.isActive
            ? t("users:confirm.deactivateTitle")
            : t("users:confirm.activateTitle")
        }
        description={
          confirmTarget?.isActive
            ? t("users:confirm.deactivateDescription", {
                name: confirmTarget?.displayName || confirmTarget?.userName || "",
              })
            : t("users:confirm.activateDescription", {
                name: confirmTarget?.displayName || confirmTarget?.userName || "",
              })
        }
        confirmText={t("common:actions.confirm")}
        cancelText={t("common:actions.cancel")}
        variant="danger"
        isLoading={updateUserStatusMutation.isPending}
        onOpenChange={(open) => !open && setConfirmTarget(null)}
        onConfirm={() => {
          if (!confirmTarget) return;
          updateUserStatusMutation.mutate({
            id: confirmTarget.id,
            isActive: !confirmTarget.isActive,
          });
        }}
      />
    </section>
  );
}
