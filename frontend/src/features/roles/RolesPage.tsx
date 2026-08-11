import { useCallback, useMemo, useState } from "react";

import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { PageContainer } from "@/components/common/PageContainer";
import { PageHeader } from "@/components/common/PageHeader";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { createRoleColumns } from "@/features/roles/role-columns";
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
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { canAccess } from "@/lib/permissions";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";
import { PermissionCodes } from "@/lib/permission-codes";

type StatusFilter = "active" | "passive" | "all";
type TypeFilter = "all" | "system" | "custom";

export function RolesPage() {
  const { t } = useTranslation(["roles", "common"]);
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const canCreate = canAccess(currentUser, PermissionCodes.Roles.Create);
  const canUpdate = canAccess(currentUser, PermissionCodes.Roles.Update);
  const canAssignPermissions = canAccess(currentUser, PermissionCodes.Roles.AssignPermissions);
  const canViewPermissions = canAccess(currentUser, PermissionCodes.Permissions.View);

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");
  const [typeFilter, setTypeFilter] = useState<TypeFilter>("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [selectedRoleForDetail, setSelectedRoleForDetail] =
    useState<RoleListItem | null>(null);
  const [selectedRoleForEdit, setSelectedRoleForEdit] =
    useState<RoleListItem | null>(null);
  const [selectedRoleForPermissions, setSelectedRoleForPermissions] =
    useState<RoleListItem | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<RoleListItem | null>(null);

  const debouncedSearch = useDebouncedValue(search, 400);
  const normalizedSearch = debouncedSearch.trim();
  const effectiveSearch =
    normalizedSearch.length >= 3 ? normalizedSearch : undefined;

  const rolesQuery = useQuery({
    queryKey: ["roles", "list", effectiveSearch, statusFilter, typeFilter, pageNumber, pageSize],
    queryFn: () =>
      getRoles({
        search: effectiveSearch,
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
        pageNumber,
        pageSize,
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

  const handleToggleStatus = useCallback(
    (role: RoleListItem) => {
      if (role.isSystem || !canUpdate) return;
      setConfirmTarget(role);
    },
    [canUpdate],
  );

  const columns = useMemo(
    () =>
      createRoleColumns({
        t,
        canUpdate,
        canAssignPermissions,
        canViewPermissions,
        isStatusPending: updateStatusMutation.isPending,
        onDetail: setSelectedRoleForDetail,
        onEdit: setSelectedRoleForEdit,
        onToggleStatus: handleToggleStatus,
        onAssignPermissions: setSelectedRoleForPermissions,
      }),
    [t, canUpdate, canAssignPermissions, canViewPermissions, updateStatusMutation.isPending, handleToggleStatus],
  );

  const table = useServerDataTable({
    data: roles,
    columns,
    pageCount: rolesQuery.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  const activeFilterCount =
    (statusFilter !== "active" ? 1 : 0) + (typeFilter !== "all" ? 1 : 0);

  const handleRefresh = () => {
    rolesQuery.refetch();
    if (selectedRoleForDetail?.id) roleDetailQuery.refetch();
  };

  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPageNumber(1);
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

  if (rolesQuery.isError) {
    const routeState = createApiErrorRouteState(rolesQuery.error, {
      fromPath: "/roles",
      retryPath: "/roles",
      sourceLabel: t("roles:sections.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <PageContainer variant="fluid">
      <PageHeader
        title={t("roles:title")}
        description={t("roles:description")}
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
      />

      <div className="flex min-w-0 flex-col gap-4">
        <DataTableToolbar
          searchValue={search}
          onSearchChange={handleSearchChange}
          searchPlaceholder={t("roles:search.placeholder")}
          activeFilterCount={activeFilterCount}
          onClearFilters={() => {
            setStatusFilter("active");
            setTypeFilter("all");
            setPageNumber(1);
          }}
          activeFilters={[
            ...(statusFilter !== "active"
              ? [{
                  id: "status",
                  label: t("common:fields.status"),
                  value: t(`common:status.${statusFilter}`),
                  onRemove: () => {
                    setStatusFilter("active");
                    setPageNumber(1);
                  },
                }]
              : []),
            ...(typeFilter !== "all"
              ? [{
                  id: "type",
                  label: t("common:fields.type"),
                  value: t(`roles:type.${typeFilter}`),
                  onRemove: () => {
                    setTypeFilter("all");
                    setPageNumber(1);
                  },
                }]
              : []),
          ]}
          filterContent={
            <div className="space-y-3">
              <Select
                value={statusFilter}
                onChange={(event) => {
                  setStatusFilter(event.target.value as StatusFilter);
                  setPageNumber(1);
                }}
                aria-label={t("common:fields.status")}
              >
                <option value="active">{t("common:status.active")}</option>
                <option value="passive">{t("common:status.passive")}</option>
                <option value="all">{t("common:status.all")}</option>
              </Select>
              <Select
                value={typeFilter}
                onChange={(event) => {
                  setTypeFilter(event.target.value as TypeFilter);
                  setPageNumber(1);
                }}
                aria-label={t("common:fields.type")}
              >
                <option value="all">{t("common:status.all")}</option>
                <option value="system">{t("roles:type.system")}</option>
                <option value="custom">{t("roles:type.custom")}</option>
              </Select>
            </div>
          }
        />

        <DataTable
          table={table}
          isLoading={rolesQuery.isLoading}
          emptyMessage={t("roles:empty.title")}
          emptyDescription={t("roles:empty.description")}
          footer={
            rolesQuery.data && rolesQuery.data.totalCount > 0 ? (
              <DataTablePagination
                mode="server"
                pageNumber={rolesQuery.data.pageNumber}
                pageSize={rolesQuery.data.pageSize}
                totalCount={rolesQuery.data.totalCount}
                totalPages={rolesQuery.data.totalPages}
                onPageChange={setPageNumber}
                onPageSizeChange={(nextPageSize) => {
                  setPageSize(nextPageSize);
                  setPageNumber(1);
                }}
              />
            ) : null
          }
        />
      </div>

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
    </PageContainer>
  );
}
