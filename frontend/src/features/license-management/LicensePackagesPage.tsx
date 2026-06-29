import { useCallback, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

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
import { buttonVariants } from "@/components/ui/button-variants";
import { Select } from "@/components/ui/select";
import {
  getAllLicensePurchases,
  getAllLicensedProducts,
  getLicensePackages,
} from "@/features/license-management/api";
import {
  getLicenseTypeLabel,
  getPackageStatusLabel,
  PACKAGE_STATUSES,
} from "@/features/license-management/enum-labels";
import { createLicensePackageColumns } from "@/features/license-management/license-columns";
import {
  buildLicensePackageDetailPath,
  buildLicensePackageEditPath,
  LICENSE_PACKAGE_CREATE_PATH,
} from "@/features/license-management/license-package-detail-path";
import type {
  LicensePackageStatus,
  LicenseType,
} from "@/features/license-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { cn } from "@/lib/utils";

type PackageStatusFilter = "all" | LicensePackageStatus;

export function LicensePackagesPage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageAcquisitions);

  const [search, setSearch] = useState("");
  const [productIdFilter, setProductIdFilter] = useState("");
  const [purchaseIdFilter, setPurchaseIdFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState<PackageStatusFilter>("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const debouncedSearch = useDebouncedValue(search, 400);
  const effectiveSearch = debouncedSearch.trim().length >= 3 ? debouncedSearch.trim() : undefined;
  const activeFilterCount =
    (productIdFilter ? 1 : 0) + (purchaseIdFilter ? 1 : 0) + (statusFilter === "all" ? 0 : 1);

  const productsQuery = useQuery({
    queryKey: ["license-management", "products", "all"],
    queryFn: getAllLicensedProducts,
  });

  const purchasesQuery = useQuery({
    queryKey: ["license-management", "purchases", "all"],
    queryFn: getAllLicensePurchases,
  });

  const listQuery = useQuery({
    queryKey: [
      "license-management",
      "packages",
      effectiveSearch,
      productIdFilter,
      purchaseIdFilter,
      statusFilter,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getLicensePackages({
        search: effectiveSearch,
        productId: productIdFilter || undefined,
        purchaseId: purchaseIdFilter || undefined,
        status: statusFilter === "all" ? undefined : statusFilter,
        pageNumber,
        pageSize,
      }),
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
        onDetail: (item) => navigate(buildLicensePackageDetailPath(item.id)),
        onEdit: (item) => navigate(buildLicensePackageEditPath(item.id)),
        getLicenseTypeLabel: resolveLicenseTypeLabel,
        getPackageStatusLabel: resolvePackageStatusLabel,
      }),
    [t, canManage, resolveLicenseTypeLabel, resolvePackageStatusLabel, navigate],
  );

  const items = listQuery.data?.items ?? [];
  const table = useServerDataTable({
    data: items,
    columns,
    pageCount: listQuery.data?.totalPages ?? 0,
    pageIndex: pageNumber - 1,
    pageSize,
  });

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
              setPurchaseIdFilter("");
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
                  <label className="text-sm font-medium">{t("licenseManagement:filters.purchase")}</label>
                  <Select
                    value={purchaseIdFilter}
                    onChange={(e) => {
                      setPurchaseIdFilter(e.target.value);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="">{t("common:status.all")}</option>
                    {(purchasesQuery.data ?? []).map((purchase) => (
                      <option key={purchase.id} value={purchase.id}>
                        {purchase.title}
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
                  <Link to={LICENSE_PACKAGE_CREATE_PATH} className={cn(buttonVariants())}>
                    {t("licenseManagement:actions.addPackage")}
                  </Link>
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
    </section>
  );
}
