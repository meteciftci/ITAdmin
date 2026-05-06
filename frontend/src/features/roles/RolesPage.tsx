import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DataToolbar } from "@/components/common/DataToolbar";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { RowActions } from "@/components/common/RowActions";
import { SectionCard } from "@/components/common/SectionCard";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { Select } from "@/components/ui/select";
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
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

type StatusFilter = "active" | "passive" | "all";
type TypeFilter = "all" | "system" | "custom";

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
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [selectedRoleForDetail, setSelectedRoleForDetail] =
    useState<RoleListItem | null>(null);
  const [selectedRoleForEdit, setSelectedRoleForEdit] =
    useState<RoleListItem | null>(null);
  const [selectedRoleForPermissions, setSelectedRoleForPermissions] =
    useState<RoleListItem | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<RoleListItem | null>(null);

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
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["roles", "list"] });
      if (selectedRoleForDetail?.id) {
        queryClient.invalidateQueries({
          queryKey: ["roles", "detail", selectedRoleForDetail.id],
        });
      }
      toast.success(
        variables.isActive
          ? t("roles:messages.roleActivated")
          : t("roles:messages.roleDeactivated"),
      );
      setConfirmTarget(null);
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("roles:messages.statusUpdateFailed")));
    },
  });

  const roles = useMemo(() => rolesQuery.data?.items ?? [], [rolesQuery.data]);

  const handleRefresh = () => {
    rolesQuery.refetch();
    if (selectedRoleForDetail?.id) roleDetailQuery.refetch();
  };

  const handleToggleStatus = (role: RoleListItem) => {
    if (role.isSystem || !canUpdate) return;
    setConfirmTarget(role);
  };

  const handleActionSuccess = (message?: string) => {
    queryClient.invalidateQueries({ queryKey: ["roles", "list"] });
    if (selectedRoleForDetail?.id) {
      queryClient.invalidateQueries({
        queryKey: ["roles", "detail", selectedRoleForDetail.id],
      });
    }
    if (message) {
      toast.success(message);
    }
  };

  return (
    <section className="space-y-4">
      <PageHeader title={t("roles:title")} description={t("roles:description")} />
      <SectionCard title={t("roles:sections.listTitle")}>
        <div className="space-y-4">
          <DataToolbar
            searchValue={search}
            onSearchChange={setSearch}
            searchPlaceholder={t("roles:search.placeholder")}
            actions={
              <>
                <Button variant="outline" onClick={handleRefresh}>
                  {t("common:actions.refresh")}
                </Button>
                {canCreate ? (
                  <Button onClick={() => setShowCreateDialog(true)}>
                    {t("roles:actions.addRole")}
                  </Button>
                ) : null}
              </>
            }
          >
            <div className="flex flex-wrap items-center gap-2">
              <Select
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}
                className="w-full sm:w-40"
              >
                <option value="active">{t("common:status.active")}</option>
                <option value="passive">{t("common:status.passive")}</option>
                <option value="all">{t("common:status.all")}</option>
              </Select>
              <Select
                value={typeFilter}
                onChange={(event) => setTypeFilter(event.target.value as TypeFilter)}
                className="w-full sm:w-40"
              >
                <option value="all">{t("common:status.all")}</option>
                <option value="system">{t("roles:type.system")}</option>
                <option value="custom">{t("roles:type.custom")}</option>
              </Select>
            </div>
          </DataToolbar>

          {rolesQuery.isLoading ? <LoadingState /> : null}

          {rolesQuery.isError ? (
            <ErrorState
              title={t("roles:errors.loadFailed")}
              description={getApiErrorMessage(rolesQuery.error, t("roles:errors.loadFailed"))}
              retry={
                <Button variant="outline" onClick={handleRefresh}>
                  {t("common:actions.refresh")}
                </Button>
              }
            />
          ) : null}

          {rolesQuery.isSuccess && !roles.length ? (
            <EmptyState
              title={t("roles:empty.title")}
              description={t("roles:empty.description")}
            />
          ) : null}

          {roles.length ? (
            <div className="overflow-x-auto rounded-lg border bg-card">
              <table className="min-w-full text-sm">
                <thead className="bg-muted/50 text-left">
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
                      <tr key={role.id} className="border-t align-top hover:bg-muted/20">
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
                          <StatusBadge isActive={role.isActive} />
                        </td>
                        <td className="px-3 py-2">
                          <Badge variant="outline">{role.permissionCount}</Badge>
                        </td>
                        <td className="px-3 py-2">
                          <RowActions>
                            <DropdownMenuLabel>{t("common:actions.actions")}</DropdownMenuLabel>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem onClick={() => setSelectedRoleForDetail(role)}>
                              {t("roles:actions.detail")}
                            </DropdownMenuItem>
                            {canUpdate && canEditRole ? (
                              <DropdownMenuItem onClick={() => setSelectedRoleForEdit(role)}>
                                {t("roles:actions.edit")}
                              </DropdownMenuItem>
                            ) : null}
                            {canUpdate && canChangeStatus ? (
                              <DropdownMenuItem
                                disabled={updateStatusMutation.isPending}
                                onClick={() => handleToggleStatus(role)}
                              >
                                {role.isActive
                                  ? t("roles:actions.deactivate")
                                  : t("roles:actions.activate")}
                              </DropdownMenuItem>
                            ) : null}
                            {canAssignPermissions && canViewPermissions && canAssignRolePermissions ? (
                              <DropdownMenuItem
                                onClick={() => setSelectedRoleForPermissions(role)}
                              >
                                {t("roles:actions.assignPermissions")}
                              </DropdownMenuItem>
                            ) : null}
                          </RowActions>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : null}
        </div>
      </SectionCard>

      <RoleDetailDialog
        open={Boolean(selectedRoleForDetail)}
        role={roleDetailQuery.data ?? null}
        onOpenChange={(open) => {
          if (!open) setSelectedRoleForDetail(null);
        }}
      />

      <RoleFormDialog
        open={showCreateDialog}
        mode="create"
        onClose={() => setShowCreateDialog(false)}
        onSaved={() => handleActionSuccess(t("roles:messages.roleCreated"))}
      />

      <RoleFormDialog
        open={Boolean(selectedRoleForEdit)}
        mode="edit"
        role={selectedRoleForEdit}
        onClose={() => setSelectedRoleForEdit(null)}
        onSaved={() => handleActionSuccess(t("roles:messages.roleUpdated"))}
      />

      <AssignPermissionsDialog
        open={Boolean(selectedRoleForPermissions)}
        role={selectedRoleForPermissions}
        onClose={() => setSelectedRoleForPermissions(null)}
        onSaved={() => handleActionSuccess(t("roles:messages.permissionsUpdated"))}
      />

      <ConfirmDialog
        open={Boolean(confirmTarget)}
        title={
          confirmTarget?.isActive
            ? t("roles:confirm.deactivateTitle")
            : t("roles:confirm.activateTitle")
        }
        description={
          confirmTarget?.isActive
            ? t("roles:confirm.deactivateDescription", { name: confirmTarget?.name ?? "" })
            : t("roles:confirm.activateDescription", { name: confirmTarget?.name ?? "" })
        }
        confirmText={t("common:actions.confirm")}
        cancelText={t("common:actions.cancel")}
        variant="danger"
        isLoading={updateStatusMutation.isPending}
        onOpenChange={(open) => !open && setConfirmTarget(null)}
        onConfirm={() => {
          if (!confirmTarget) return;
          updateStatusMutation.mutate({
            id: confirmTarget.id,
            isActive: !confirmTarget.isActive,
          });
        }}
      />
    </section>
  );
}
