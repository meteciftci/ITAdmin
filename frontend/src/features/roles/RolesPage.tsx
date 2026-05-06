import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AxiosError } from "axios";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { useAuthStore } from "@/features/auth/auth-store";
import {
  getRoleById,
  getRoles,
  updateRoleStatus,
} from "@/features/roles/api";
import { AssignPermissionsDialog } from "@/features/roles/AssignPermissionsDialog";
import { RoleDetailDialog } from "@/features/roles/RoleDetailDialog";
import { RoleFormDialog } from "@/features/roles/RoleFormDialog";
import type { RoleListItem } from "@/features/roles/types";
import { canAccess } from "@/lib/permissions";
import { useTranslation } from "react-i18next";

type StatusFilter = "active" | "passive" | "all";
type TypeFilter = "all" | "system" | "custom";
type ApiErrorPayload = { message?: string };

const getErrorMessage = (error: unknown, fallback: string): string => {
  const apiError = error as AxiosError<ApiErrorPayload>;
  return apiError.response?.data?.message ?? fallback;
};

export function RolesPage() {
  const { t } = useTranslation(["roles", "common"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const canCreate = canAccess(currentUser, "Roles.Create");
  const canUpdate = canAccess(currentUser, "Roles.Update");
  const canAssignPermissions = canAccess(currentUser, "Roles.AssignPermissions");
  const canViewPermissions = canAccess(currentUser, "Permissions.View");

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");
  const [typeFilter, setTypeFilter] = useState<TypeFilter>("all");
  const [alertMessage, setAlertMessage] = useState<string | null>(null);
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [selectedRoleForDetail, setSelectedRoleForDetail] =
    useState<RoleListItem | null>(null);
  const [selectedRoleForEdit, setSelectedRoleForEdit] =
    useState<RoleListItem | null>(null);
  const [selectedRoleForPermissions, setSelectedRoleForPermissions] =
    useState<RoleListItem | null>(null);

  const rolesQuery = useQuery({
    queryKey: ["roles", "list", search, statusFilter, typeFilter],
    queryFn: () =>
      getRoles({
        search: search.trim() || undefined,
        isActive:
          statusFilter === "all"
            ? undefined
            : statusFilter === "active"
              ? true
              : false,
        isSystem:
          typeFilter === "all"
            ? undefined
            : typeFilter === "system"
              ? true
              : false,
        pageNumber: 1,
        pageSize: 50,
      }),
  });

  const roleDetailQuery = useQuery({
    queryKey: ["roles", "detail", selectedRoleForDetail?.id],
    queryFn: () => getRoleById(selectedRoleForDetail!.id),
    enabled: Boolean(selectedRoleForDetail?.id),
  });

  const updateStatusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      updateRoleStatus(id, { isActive }),
    onSuccess: () => {
      setAlertMessage(null);
      queryClient.invalidateQueries({ queryKey: ["roles", "list"] });
      if (selectedRoleForDetail?.id) {
        queryClient.invalidateQueries({
          queryKey: ["roles", "detail", selectedRoleForDetail.id],
        });
      }
    },
    onError: (error) => {
      setAlertMessage(getErrorMessage(error, t("roles:messages.statusUpdateFailed")));
    },
  });

  const roles = useMemo(() => rolesQuery.data?.items ?? [], [rolesQuery.data]);

  const handleRefresh = () => {
    rolesQuery.refetch();
    if (selectedRoleForDetail?.id) roleDetailQuery.refetch();
  };

  const handleToggleStatus = (role: RoleListItem) => {
    if (role.isSystem || !canUpdate) return;
    const nextValue = !role.isActive;
    const confirmed = window.confirm(
      nextValue
        ? `${t("roles:actions.activate")} ${role.name}?`
        : `${t("roles:actions.deactivate")} ${role.name}?`,
    );
    if (!confirmed) return;
    updateStatusMutation.mutate({ id: role.id, isActive: nextValue });
  };

  const handleActionSuccess = () => {
    setAlertMessage(null);
    queryClient.invalidateQueries({ queryKey: ["roles", "list"] });
    if (selectedRoleForDetail?.id) {
      queryClient.invalidateQueries({
        queryKey: ["roles", "detail", selectedRoleForDetail.id],
      });
    }
  };

  return (
    <section className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("roles:title")}</h1>
        <p className="text-sm text-muted-foreground">
          {t("roles:description")}
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
          <CardTitle>{t("roles:sections.listTitle")}</CardTitle>
          <div className="grid gap-2 md:grid-cols-[1fr_150px_150px_auto]">
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t("roles:search.placeholder")}
            />
            <Select
              value={statusFilter}
              onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}
            >
              <option value="active">{t("common:status.active")}</option>
              <option value="passive">{t("common:status.passive")}</option>
              <option value="all">{t("common:status.all")}</option>
            </Select>
            <Select
              value={typeFilter}
              onChange={(event) => setTypeFilter(event.target.value as TypeFilter)}
            >
              <option value="all">{t("common:status.all")}</option>
              <option value="system">{t("roles:type.system")}</option>
              <option value="custom">{t("roles:type.custom")}</option>
            </Select>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={handleRefresh}>
                {t("common:actions.refresh")}
              </Button>
              {canCreate ? (
                <Button onClick={() => setShowCreateDialog(true)}>
                  {t("roles:actions.addRole")}
                </Button>
              ) : null}
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {rolesQuery.isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : null}

          {rolesQuery.isError ? (
            <Alert variant="destructive">
              <AlertTitle>{t("roles:errors.loadFailed")}</AlertTitle>
              <AlertDescription>
                {getErrorMessage(rolesQuery.error, t("roles:errors.loadFailed"))}
              </AlertDescription>
            </Alert>
          ) : null}

          {rolesQuery.isSuccess && !roles.length ? (
            <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">
              {t("roles:empty.description")}
            </div>
          ) : null}

          {roles.length ? (
            <div className="overflow-x-auto rounded-lg border">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/40 text-left">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t("roles:table.name")}</th>
                    <th className="px-3 py-2 font-medium">{t("roles:table.code")}</th>
                    <th className="px-3 py-2 font-medium">{t("roles:table.description")}</th>
                    <th className="px-3 py-2 font-medium">{t("roles:table.type")}</th>
                    <th className="px-3 py-2 font-medium">{t("roles:table.status")}</th>
                    <th className="px-3 py-2 font-medium">{t("roles:table.permissionCount")}</th>
                    <th className="px-3 py-2 font-medium">{t("roles:table.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {roles.map((role) => {
                    const isSystemRole = role.isSystem;
                    const canEditRole = canUpdate && !isSystemRole;
                    const canChangeStatus = canUpdate && !isSystemRole;
                    const canAssignRolePermissions =
                      canAssignPermissions && canViewPermissions && !isSystemRole;

                    return (
                      <tr key={role.id} className="border-t align-top">
                        <td className="px-3 py-2">{role.name}</td>
                        <td className="px-3 py-2">{role.code}</td>
                        <td className="max-w-70 px-3 py-2">
                          <span className="line-clamp-2">{role.description || "-"}</span>
                        </td>
                        <td className="px-3 py-2">
                          <Badge variant={isSystemRole ? "warning" : "secondary"}>
                            {isSystemRole ? t("roles:type.system") : t("roles:type.custom")}
                          </Badge>
                        </td>
                        <td className="px-3 py-2">
                          <Badge variant={role.isActive ? "success" : "outline"}>
                            {role.isActive ? t("common:status.active") : t("common:status.passive")}
                          </Badge>
                        </td>
                        <td className="px-3 py-2">
                          <Badge variant="outline">{role.permissionCount}</Badge>
                        </td>
                        <td className="px-3 py-2">
                          <div className="flex flex-wrap gap-1">
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => setSelectedRoleForDetail(role)}
                            >
                              {t("roles:actions.detail")}
                            </Button>
                            {canUpdate ? (
                              <Button
                                variant="outline"
                                size="sm"
                                disabled={!canEditRole}
                                onClick={() => setSelectedRoleForEdit(role)}
                              >
                                {t("roles:actions.edit")}
                              </Button>
                            ) : null}
                            {canUpdate ? (
                              <Button
                                variant="outline"
                                size="sm"
                                disabled={!canChangeStatus || updateStatusMutation.isPending}
                                onClick={() => handleToggleStatus(role)}
                              >
                                {role.isActive
                                  ? t("roles:actions.deactivate")
                                  : t("roles:actions.activate")}
                              </Button>
                            ) : null}
                            {canAssignPermissions && canViewPermissions ? (
                              <Button
                                variant="outline"
                                size="sm"
                                disabled={!canAssignRolePermissions}
                                onClick={() => setSelectedRoleForPermissions(role)}
                              >
                                {t("roles:actions.assignPermissions")}
                              </Button>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <RoleDetailDialog
        open={Boolean(selectedRoleForDetail)}
        role={roleDetailQuery.data ?? null}
        onClose={() => setSelectedRoleForDetail(null)}
      />

      <RoleFormDialog
        open={showCreateDialog}
        mode="create"
        onClose={() => setShowCreateDialog(false)}
        onSaved={handleActionSuccess}
      />

      <RoleFormDialog
        open={Boolean(selectedRoleForEdit)}
        mode="edit"
        role={selectedRoleForEdit}
        onClose={() => setSelectedRoleForEdit(null)}
        onSaved={handleActionSuccess}
      />

      <AssignPermissionsDialog
        open={Boolean(selectedRoleForPermissions)}
        role={selectedRoleForPermissions}
        onClose={() => setSelectedRoleForPermissions(null)}
        onSaved={handleActionSuccess}
      />
    </section>
  );
}
