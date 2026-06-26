import { useCallback, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ProductFormDialog } from "@/features/license-management/components/ProductFormDialog";
import {
  getLicensedProductById,
  getLicensedProducts,
  updateLicensedProductStatus,
} from "@/features/license-management/api";
import { getLicenseTypeLabel } from "@/features/license-management/enum-labels";
import { createLicenseProductColumns } from "@/features/license-management/license-columns";
import type { LicensedProductListItem, LicenseType } from "@/features/license-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useAuthStore } from "@/features/auth/auth-store";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

type StatusFilter = "active" | "passive" | "all";

export function LicenseProductsPage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageCatalog);

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [showCreate, setShowCreate] = useState(false);
  const [selectedEdit, setSelectedEdit] = useState<LicensedProductListItem | null>(null);
  const [selectedDetail, setSelectedDetail] = useState<LicensedProductListItem | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<LicensedProductListItem | null>(null);

  const debouncedSearch = useDebouncedValue(search, 400);
  const effectiveSearch = debouncedSearch.trim().length >= 3 ? debouncedSearch.trim() : undefined;
  const activeFilterCount = statusFilter === "active" ? 0 : 1;

  const listQuery = useQuery({
    queryKey: ["license-management", "products", effectiveSearch, statusFilter, pageNumber, pageSize],
    queryFn: () =>
      getLicensedProducts({
        search: effectiveSearch,
        isActive: statusFilter === "all" ? undefined : statusFilter === "active",
        pageNumber,
        pageSize,
      }),
  });

  const detailQuery = useQuery({
    queryKey: ["license-management", "products", "detail", selectedDetail?.id],
    queryFn: () => getLicensedProductById(selectedDetail!.id),
    enabled: Boolean(selectedDetail?.id),
  });

  const editDetailQuery = useQuery({
    queryKey: ["license-management", "products", "edit", selectedEdit?.id],
    queryFn: () => getLicensedProductById(selectedEdit!.id),
    enabled: Boolean(selectedEdit?.id),
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      updateLicensedProductStatus(id, isActive),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["license-management", "products"] });
      queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
      toast.success(t("licenseManagement:messages.productStatusUpdated"));
      setConfirmTarget(null);
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, t("licenseManagement:messages.operationFailed")));
    },
  });

  const resolveLicenseTypeLabel = useCallback(
    (value: string | null) =>
      value ? getLicenseTypeLabel(t, value as LicenseType) : "-",
    [t],
  );

  const columns = useMemo(
    () =>
      createLicenseProductColumns({
        t,
        canManage,
        isStatusPending: statusMutation.isPending,
        onDetail: setSelectedDetail,
        onEdit: setSelectedEdit,
        onToggleStatus: setConfirmTarget,
        getLicenseTypeLabel: resolveLicenseTypeLabel,
      }),
    [t, canManage, statusMutation.isPending, resolveLicenseTypeLabel],
  );

  const items = listQuery.data?.items ?? [];
  const table = useServerDataTable({
    data: items,
    columns,
    pageCount: listQuery.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

  const handleSaved = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ["license-management", "products"] });
    queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
    toast.success(
      selectedEdit
        ? t("licenseManagement:messages.productUpdated")
        : t("licenseManagement:messages.productCreated"),
    );
    setSelectedEdit(null);
    setShowCreate(false);
  }, [queryClient, selectedEdit, t]);

  if (listQuery.isError) {
    const routeState = createApiErrorRouteState(listQuery.error, {
      fromPath: "/license-management/products",
      retryPath: "/license-management/products",
      sourceLabel: t("licenseManagement:products.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("licenseManagement:products.title")}
        description={t("licenseManagement:products.description")}
      />
      <SectionCard title={t("licenseManagement:products.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("licenseManagement:products.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setStatusFilter("active");
              setPageNumber(1);
            }}
            filterContent={
              <div className="space-y-2">
                <label className="text-sm font-medium">{t("licenseManagement:filters.status")}</label>
                <Select
                  value={statusFilter}
                  onChange={(e) => {
                    setStatusFilter(e.target.value as StatusFilter);
                    setPageNumber(1);
                  }}
                  className="w-full"
                >
                  <option value="active">{t("common:status.active")}</option>
                  <option value="passive">{t("common:status.passive")}</option>
                  <option value="all">{t("common:status.all")}</option>
                </Select>
              </div>
            }
            actions={
              <>
                <Button variant="outline" onClick={() => listQuery.refetch()} disabled={listQuery.isFetching}>
                  {t("common:actions.refresh")}
                </Button>
                {canManage ? (
                  <Button onClick={() => setShowCreate(true)}>{t("licenseManagement:actions.addProduct")}</Button>
                ) : null}
              </>
            }
          />
          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && items.length === 0 ? (
            <EmptyState title={t("licenseManagement:products.empty")} />
          ) : null}
          {items.length > 0 ? (
            <DataTable
              table={table}
              footer={
                listQuery.data && listQuery.data.totalCount > 0 ? (
                  <DataTablePagination
                    mode="server"
                    pageNumber={listQuery.data.pageNumber}
                    pageSize={listQuery.data.pageSize}
                    totalCount={listQuery.data.totalCount}
                    totalPages={listQuery.data.totalPages}
                    onPageChange={setPageNumber}
                    onPageSizeChange={(size) => {
                      setPageSize(size);
                      setPageNumber(1);
                    }}
                  />
                ) : null
              }
            />
          ) : null}
        </div>
      </SectionCard>

      <ProductFormDialog open={showCreate} mode="create" onClose={() => setShowCreate(false)} onSaved={handleSaved} />
      <ProductFormDialog
        open={Boolean(selectedEdit)}
        mode="edit"
        product={editDetailQuery.data ?? null}
        onClose={() => setSelectedEdit(null)}
        onSaved={handleSaved}
      />

      <Dialog open={Boolean(selectedDetail)}>
        <DialogContent onOpenChange={(open) => !open && setSelectedDetail(null)}>
          <DialogHeader>
            <DialogTitle>{detailQuery.data?.name ?? selectedDetail?.name}</DialogTitle>
          </DialogHeader>
          <DialogBody className="space-y-2 text-sm">
            {detailQuery.isLoading ? <LoadingState /> : null}
            {detailQuery.data ? (
              <>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.vendor")}:</span>{" "}
                  {detailQuery.data.vendorCompanyName ?? t("licenseManagement:form.noVendor")}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.category")}:</span>{" "}
                  {detailQuery.data.category ?? "-"}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.defaultLicenseType")}:</span>{" "}
                  {detailQuery.data.defaultLicenseType
                    ? getLicenseTypeLabel(t, detailQuery.data.defaultLicenseType)
                    : "-"}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:form.description")}:</span>{" "}
                  {detailQuery.data.description ?? "-"}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:form.notes")}:</span>{" "}
                  {detailQuery.data.notes ?? "-"}
                </p>
              </>
            ) : null}
          </DialogBody>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={Boolean(confirmTarget)}
        title={
          confirmTarget?.isActive
            ? t("licenseManagement:confirm.deactivateProductTitle")
            : t("licenseManagement:confirm.activateProductTitle")
        }
        description={
          confirmTarget?.isActive
            ? t("licenseManagement:confirm.deactivateProductDescription")
            : t("licenseManagement:confirm.activateProductDescription")
        }
        confirmText={t("common:actions.confirm")}
        cancelText={t("common:actions.cancel")}
        variant={confirmTarget?.isActive ? "danger" : "default"}
        isLoading={statusMutation.isPending}
        onOpenChange={(open) => !open && setConfirmTarget(null)}
        onConfirm={() => {
          if (!confirmTarget) return;
          statusMutation.mutate({ id: confirmTarget.id, isActive: !confirmTarget.isActive });
        }}
      />
    </section>
  );
}
