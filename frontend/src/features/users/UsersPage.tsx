import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AxiosError } from "axios";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DateTimeText } from "@/components/common/DateTimeText";
import { Input } from "@/components/ui/input";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { getUserById, getUsers, updateUserStatus } from "@/features/users/api";
import { AddUserDialog } from "@/features/users/AddUserDialog";
import { AssignRolesDialog } from "@/features/users/AssignRolesDialog";
import { UserDetailDialog } from "@/features/users/UserDetailDialog";
import type { UserListItem } from "@/features/users/types";
import { useTranslation } from "react-i18next";

type StatusFilter = "active" | "passive" | "all";
type ApiErrorPayload = { message?: string };

const getErrorMessage = (error: unknown, fallback: string): string => {
  const apiError = error as AxiosError<ApiErrorPayload>;
  return apiError.response?.data?.message ?? fallback;
};

export function UsersPage() {
  const { t } = useTranslation(["users", "common"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const canCreate = canAccess(currentUser, "Users.Create");
  const canUpdate = canAccess(currentUser, "Users.Update");
  const canAssignRoles = canAccess(currentUser, "Users.AssignRoles");

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");
  const [alertMessage, setAlertMessage] = useState<string | null>(null);
  const [showAddUser, setShowAddUser] = useState(false);
  const [selectedUserForDetail, setSelectedUserForDetail] =
    useState<UserListItem | null>(null);
  const [selectedUserForRoles, setSelectedUserForRoles] =
    useState<UserListItem | null>(null);

  const usersQuery = useQuery({
    queryKey: ["users", "list", search, statusFilter],
    queryFn: () =>
      getUsers({
        search: search.trim() || undefined,
        isActive:
          statusFilter === "all"
            ? undefined
            : statusFilter === "active"
              ? true
              : false,
        pageNumber: 1,
        pageSize: 50,
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
    onSuccess: () => {
      setAlertMessage(null);
      queryClient.invalidateQueries({ queryKey: ["users", "list"] });
      if (selectedUserForDetail?.id) {
        queryClient.invalidateQueries({
          queryKey: ["users", "detail", selectedUserForDetail.id],
        });
      }
    },
    onError: (error) => {
      setAlertMessage(
        getErrorMessage(error, t("users:messages.statusUpdated")),
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

  const handleToggleStatus = (user: UserListItem) => {
    const nextValue = !user.isActive;
    const confirmed = window.confirm(
      nextValue
        ? `${t("users:actions.activate")} ${user.displayName || user.userName}?`
        : `${t("users:actions.deactivate")} ${user.displayName || user.userName}?`,
    );
    if (!confirmed) return;
    updateUserStatusMutation.mutate({ id: user.id, isActive: nextValue });
  };

  const handleActionSuccess = () => {
    setAlertMessage(null);
    queryClient.invalidateQueries({ queryKey: ["users", "list"] });
    if (selectedUserForDetail?.id) {
      queryClient.invalidateQueries({
        queryKey: ["users", "detail", selectedUserForDetail.id],
      });
    }
  };

  return (
    <section className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("users:title")}</h1>
        <p className="text-sm text-muted-foreground">
          {t("users:description")}
        </p>
      </div>

      {alertMessage ? (
        <Alert variant="destructive">
          <AlertTitle>{t("common:error")}</AlertTitle>
          <AlertDescription>{alertMessage}</AlertDescription>
        </Alert>
      ) : null}

      <Card>
        <CardHeader className="space-y-3">
          <CardTitle>{t("users:sections.listTitle")}</CardTitle>
          <div className="grid gap-2 md:grid-cols-[1fr_auto_auto_auto_auto]">
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t("users:search.placeholder")}
            />
            <Button
              variant={statusFilter === "active" ? "default" : "outline"}
              onClick={() => setStatusFilter("active")}
            >
              {t("common:status.active")}
            </Button>
            <Button
              variant={statusFilter === "passive" ? "default" : "outline"}
              onClick={() => setStatusFilter("passive")}
            >
              {t("common:status.passive")}
            </Button>
            <Button
              variant={statusFilter === "all" ? "default" : "outline"}
              onClick={() => setStatusFilter("all")}
            >
              {t("common:status.all")}
            </Button>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
              {canCreate ? (
                <Button onClick={() => setShowAddUser(true)}>
                  {t("users:actions.addUser")}
                </Button>
              ) : null}
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {usersQuery.isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : null}

          {usersQuery.isError ? (
            <Alert variant="destructive">
              <AlertTitle>{t("users:errors.loadFailed")}</AlertTitle>
              <AlertDescription>
                {getErrorMessage(usersQuery.error, t("users:errors.loadFailed"))}
              </AlertDescription>
            </Alert>
          ) : null}

          {usersQuery.isSuccess && !users.length ? (
            <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">
              {t("users:empty.description")}
            </div>
          ) : null}

          {users.length ? (
            <div className="overflow-x-auto rounded-lg border">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/40 text-left">
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
                    <tr key={user.id} className="border-t align-top">
                      <td className="px-3 py-2">{user.displayName || "-"}</td>
                      <td className="px-3 py-2">{user.userName}</td>
                      <td className="px-3 py-2">{user.email || "-"}</td>
                      <td className="px-3 py-2">{user.nationalIdMasked || "-"}</td>
                      <td className="px-3 py-2">
                        <div className="flex flex-wrap gap-1">
                          {user.roles.length ? (
                            user.roles.map((role) => (
                              <span
                                key={`${user.id}-${role}`}
                                className="rounded-md bg-muted px-2 py-0.5 text-xs"
                              >
                                {role}
                              </span>
                            ))
                          ) : (
                            <span className="text-muted-foreground">-</span>
                          )}
                        </div>
                      </td>
                      <td className="px-3 py-2">
                        <span
                          className={
                            user.isActive
                              ? "rounded-md bg-emerald-100 px-2 py-0.5 text-xs text-emerald-700"
                              : "rounded-md bg-amber-100 px-2 py-0.5 text-xs text-amber-700"
                          }
                        >
                          {user.isActive
                            ? t("common:status.active")
                            : t("common:status.passive")}
                        </span>
                      </td>
                      <td className="px-3 py-2">
                        <DateTimeText value={user.lastLoginAt} />
                      </td>
                      <td className="px-3 py-2">
                        <div className="flex flex-wrap gap-1">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => setSelectedUserForDetail(user)}
                          >
                            {t("users:actions.detail")}
                          </Button>
                          {canUpdate ? (
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={updateUserStatusMutation.isPending}
                              onClick={() => handleToggleStatus(user)}
                            >
                              {user.isActive
                                ? t("users:actions.deactivate")
                                : t("users:actions.activate")}
                            </Button>
                          ) : null}
                          {canAssignRoles ? (
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => setSelectedUserForRoles(user)}
                            >
                              {t("users:actions.assignRoles")}
                            </Button>
                          ) : null}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </CardContent>
      </Card>

      {showAddUser ? (
        <AddUserDialog
          onClose={() => setShowAddUser(false)}
          onCreated={handleActionSuccess}
        />
      ) : null}

      {selectedUserForRoles ? (
        <AssignRolesDialog
          userId={selectedUserForRoles.id}
          currentRoleCodes={selectedUserForRoles.roles}
          onClose={() => setSelectedUserForRoles(null)}
          onSaved={handleActionSuccess}
        />
      ) : null}

      {selectedUserForDetail ? (
        <>
          <Separator />
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <h2 className="text-base font-medium">{t("users:detail.title")}</h2>
              <Button variant="ghost" onClick={() => setSelectedUserForDetail(null)}>
                {t("common:actions.close")}
              </Button>
            </div>
            {userDetailQuery.isLoading ? (
              <Skeleton className="h-36 w-full" />
            ) : userDetailQuery.isError ? (
              <Alert variant="destructive">
                <AlertTitle>{t("common:error")}</AlertTitle>
                <AlertDescription>
                  {getErrorMessage(userDetailQuery.error, t("common:error"))}
                </AlertDescription>
              </Alert>
            ) : userDetailQuery.data ? (
              <UserDetailDialog user={userDetailQuery.data} />
            ) : null}
          </div>
        </>
      ) : null}
    </section>
  );
}
