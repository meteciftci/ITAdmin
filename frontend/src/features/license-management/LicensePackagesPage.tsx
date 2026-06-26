import { useCallback, useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import {
  DataTable,
  DataTablePagination,
  DataTableToolbar,
} from "@/components/common/data-table";
import { useServerDataTable } from "@/components/common/data-table-hooks";
import { DateTimeText } from "@/components/common/DateTimeText";
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
import { PackageFormDialog } from "@/features/license-management/components/PackageFormDialog";
import {
  getAllLicenseAcquisitions,
  getAllLicensedProducts,
  getLicensePackageById,
  getLicensePackages,
} from "@/features/license-management/api";
import {
  getLicenseTypeLabel,
  getPackageStatusLabel,
  maskLicenseKey,
  PACKAGE_STATUSES,
} from "@/features/license-management/enum-labels";
import { createLicensePackageColumns } from "@/features/license-management/license-columns";
import type {
  LicensePackageListItem,
  LicensePackageStatus,
  LicenseType,
} from "@/features/license-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

type PackageStatusFilter = "all" | LicensePackageStatus;

export function LicensePackagesPage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageAcquisitions);

  const [search, setSearch] = useState("");
  const [productIdFilter, setProductIdFilter] = useState("");
  const [acquisitionIdFilter, setAcquisitionIdFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState<PackageStatusFilter>("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [showCreate, setShowCreate] = useState(false);
  const [selectedEdit, setSelectedEdit] = useState<LicensePackageListItem | null>(null);
  const [selectedDetail, setSelectedDetail] = useState<LicensePackageListItem | null>(null);

  const debouncedSearch = useDebouncedValue(search, 400);
  const effectiveSearch = debouncedSearch.trim().length >= 3 ? debouncedSearch.trim() : undefined;
  const activeFilterCount =
    (productIdFilter ? 1 : 0) + (acquisitionIdFilter ? 1 : 0) + (statusFilter === "all" ? 0 : 1);

  const productsQuery = useQuery({
    queryKey: ["license-management", "products", "all"],
    queryFn: getAllLicensedProducts,
  });

  const acquisitionsQuery = useQuery({
    queryKey: ["license-management", "acquisitions", "all"],
    queryFn: getAllLicenseAcquisitions,
  });

  const listQuery = useQuery({
    queryKey: [
      "license-management",
      "packages",
      effectiveSearch,
      productIdFilter,
      acquisitionIdFilter,
      statusFilter,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getLicensePackages({
        search: effectiveSearch,
        productId: productIdFilter || undefined,
        acquisitionId: acquisitionIdFilter || undefined,
        status: statusFilter === "all" ? undefined : statusFilter,
        pageNumber,
        pageSize,
      }),
  });

  const detailQuery = useQuery({
    queryKey: ["license-management", "packages", "detail", selectedDetail?.id],
    queryFn: () => getLicensePackageById(selectedDetail!.id),
    enabled: Boolean(selectedDetail?.id),
  });

  const editDetailQuery = useQuery({
    queryKey: ["license-management", "packages", "edit", selectedEdit?.id],
    queryFn: () => getLicensePackageById(selectedEdit!.id),
    enabled: Boolean(selectedEdit?.id),
  });

  const resolveLicenseTypeLabel = useCallback(
    (value: string) => getLicenseTypeLabel(t, value as LicenseType),
    [t],
  );

  const resolvePackageStatusLabel = useCallback(
    (value: string) => getPackageStatusLabel(t, value as LicensePackageStatus),
    [t],
  );

  const columns = useMemo(
    () =>
      createLicensePackageColumns({
        t,
        canManage,
        onDetail: setSelectedDetail,
        onEdit: setSelectedEdit,
        getLicenseTypeLabel: resolveLicenseTypeLabel,
        getPackageStatusLabel: resolvePackageStatusLabel,
      }),
    [t, canManage, resolveLicenseTypeLabel, resolvePackageStatusLabel],
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
    queryClient.invalidateQueries({ queryKey: ["license-management", "packages"] });
    queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
    toast.success(
      selectedEdit
        ? t("licenseManagement:messages.packageUpdated")
        : t("licenseManagement:messages.packageCreated"),
    );
    setSelectedEdit(null);
    setShowCreate(false);
  }, [queryClient, selectedEdit, t]);

  if (listQuery.isError) {
    const routeState = createApiErrorRouteState(listQuery.error, {
      fromPath: "/license-management/packages",
      retryPath: "/license-management/packages",
      sourceLabel: t("licenseManagement:packages.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("licenseManagement:packages.title")}
        description={t("licenseManagement:packages.description")}
      />
      <SectionCard title={t("licenseManagement:packages.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("licenseManagement:packages.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setProductIdFilter("");
              setAcquisitionIdFilter("");
              setStatusFilter("all");
              setPageNumber(1);
            }}
            filterContent={
              <div className="space-y-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.product")}</label>
                  <Select
                    value={productIdFilter}
                    onChange={(e) => {
                      setProductIdFilter(e.target.value);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="">{t("common:status.all")}</option>
                    {(productsQuery.data ?? []).map((product) => (
                      <option key={product.id} value={product.id}>
                        {product.name}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.acquisition")}</label>
                  <Select
                    value={acquisitionIdFilter}
                    onChange={(e) => {
                      setAcquisitionIdFilter(e.target.value);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="">{t("common:status.all")}</option>
                    {(acquisitionsQuery.data ?? []).map((acquisition) => (
                      <option key={acquisition.id} value={acquisition.id}>
                        {acquisition.title}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.packageStatus")}</label>
                  <Select
                    value={statusFilter}
                    onChange={(e) => {
                      setStatusFilter(e.target.value as PackageStatusFilter);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {PACKAGE_STATUSES.map((status) => (
                      <option key={status} value={status}>
                        {getPackageStatusLabel(t, status)}
                      </option>
                    ))}
                  </Select>
                </div>
              </div>
            }
            actions={
              <>
                <Button variant="outline" onClick={() => listQuery.refetch()} disabled={listQuery.isFetching}>
                  {t("common:actions.refresh")}
                </Button>
                {canManage ? (
                  <Button onClick={() => setShowCreate(true)}>{t("licenseManagement:actions.addPackage")}</Button>
                ) : null}
              </>
            }
          />
          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && items.length === 0 ? (
            <EmptyState title={t("licenseManagement:packages.empty")} />
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

      <PackageFormDialog open={showCreate} mode="create" onClose={() => setShowCreate(false)} onSaved={handleSaved} />
      <PackageFormDialog
        open={Boolean(selectedEdit)}
        mode="edit"
        packageItem={editDetailQuery.data ?? null}
        onClose={() => setSelectedEdit(null)}
        onSaved={handleSaved}
      />

      <Dialog open={Boolean(selectedDetail)}>
        <DialogContent onOpenChange={(open) => !open && setSelectedDetail(null)}>
          <DialogHeader>
            <DialogTitle>
              {detailQuery.data
                ? `${detailQuery.data.productName} — ${detailQuery.data.acquisitionTitle}`
                : `${selectedDetail?.productName} — ${selectedDetail?.acquisitionTitle}`}
            </DialogTitle>
          </DialogHeader>
          <DialogBody className="space-y-2 text-sm">
            {detailQuery.isLoading ? <LoadingState /> : null}
            {detailQuery.data ? (
              <>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.licenseType")}:</span>{" "}
                  {getLicenseTypeLabel(t, detailQuery.data.licenseType)}
                </p>
                <p>
                  <span className="font-medium">{t("common:fields.status")}:</span>{" "}
                  {getPackageStatusLabel(t, detailQuery.data.status)}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.quantity")}:</span>{" "}
                  {detailQuery.data.quantity}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.usedQuantity")}:</span>{" "}
                  {detailQuery.data.usedQuantity}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.availableQuantity")}:</span>{" "}
                  {detailQuery.data.availableQuantity}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.startDate")}:</span>{" "}
                  {detailQuery.data.startDate ? (
                    <DateTimeText
                      value={detailQuery.data.startDate}
                      options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
                    />
                  ) : (
                    "-"
                  )}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.endDate")}:</span>{" "}
                  {detailQuery.data.endDate ? (
                    <DateTimeText
                      value={detailQuery.data.endDate}
                      options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
                    />
                  ) : (
                    "-"
                  )}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:form.serialNumber")}:</span>{" "}
                  {detailQuery.data.serialNumber ?? "-"}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:form.licenseKey")}:</span>{" "}
                  <span className="font-mono text-xs">{maskLicenseKey(detailQuery.data.licenseKey)}</span>
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:form.licenseAccountEmail")}:</span>{" "}
                  {detailQuery.data.licenseAccountEmail ?? "-"}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:form.licensePortalUrl")}:</span>{" "}
                  {detailQuery.data.licensePortalUrl ?? "-"}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:form.licenseNotes")}:</span>{" "}
                  {detailQuery.data.licenseNotes ?? "-"}
                </p>
              </>
            ) : null}
          </DialogBody>
        </DialogContent>
      </Dialog>
    </section>
  );
}
