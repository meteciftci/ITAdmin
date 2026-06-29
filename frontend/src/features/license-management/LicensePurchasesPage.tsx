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
import { getLicensePurchases } from "@/features/license-management/api";
import {
  getPurchaseStatusLabel,
  getPurchaseTypeLabel,
  PURCHASE_STATUSES,
  PURCHASE_TYPES,
} from "@/features/license-management/enum-labels";
import { createLicensePurchaseColumns } from "@/features/license-management/license-columns";
import {
  buildLicensePurchaseDetailPath,
  buildLicensePurchaseEditPath,
  LICENSE_PURCHASE_CREATE_PATH,
} from "@/features/license-management/license-purchase-detail-path";
import type {
  LicensePurchaseStatus,
  LicensePurchaseType,
} from "@/features/license-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { cn } from "@/lib/utils";

type PurchaseTypeFilter = "all" | LicensePurchaseType;
type PurchaseStatusFilter = "all" | LicensePurchaseStatus;

export function LicensePurchasesPage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManagePurchases);

  const [search, setSearch] = useState("");
  const [purchaseTypeFilter, setPurchaseTypeFilter] = useState<PurchaseTypeFilter>("all");
  const [statusFilter, setStatusFilter] = useState<PurchaseStatusFilter>("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const debouncedSearch = useDebouncedValue(search, 400);
  const effectiveSearch = debouncedSearch.trim().length >= 3 ? debouncedSearch.trim() : undefined;
  const activeFilterCount =
    (purchaseTypeFilter === "all" ? 0 : 1) + (statusFilter === "all" ? 0 : 1);

  const listQuery = useQuery({
    queryKey: [
      "license-management",
      "purchases",
      effectiveSearch,
      purchaseTypeFilter,
      statusFilter,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getLicensePurchases({
        search: effectiveSearch,
        purchaseType: purchaseTypeFilter === "all" ? undefined : purchaseTypeFilter,
        status: statusFilter === "all" ? undefined : statusFilter,
        pageNumber,
        pageSize,
      }),
  });

  const resolvePurchaseTypeLabel = useCallback(
    (value: string) => getPurchaseTypeLabel(t, value as LicensePurchaseType),
    [t],
  );

  const resolvePurchaseStatusLabel = useCallback(
    (value: string) => getPurchaseStatusLabel(t, value as LicensePurchaseStatus),
    [t],
  );

  const columns = useMemo(
    () =>
      createLicensePurchaseColumns({
        t,
        canManage,
        onDetail: (item) => navigate(buildLicensePurchaseDetailPath(item.id)),
        onEdit: (item) => navigate(buildLicensePurchaseEditPath(item.id)),
        getPurchaseTypeLabel: resolvePurchaseTypeLabel,
        getPurchaseStatusLabel: resolvePurchaseStatusLabel,
      }),
    [t, canManage, resolvePurchaseTypeLabel, resolvePurchaseStatusLabel, navigate],
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
      fromPath: "/license-management/purchases",
      retryPath: "/license-management/purchases",
      sourceLabel: t("licenseManagement:purchases.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("licenseManagement:purchases.title")}
        description={t("licenseManagement:purchases.description")}
      />
      <SectionCard title={t("licenseManagement:purchases.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("licenseManagement:purchases.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setPurchaseTypeFilter("all");
              setStatusFilter("all");
              setPageNumber(1);
            }}
            filterContent={
              <div className="space-y-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.purchaseType")}</label>
                  <Select
                    value={purchaseTypeFilter}
                    onChange={(e) => {
                      setPurchaseTypeFilter(e.target.value as PurchaseTypeFilter);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {PURCHASE_TYPES.map((type) => (
                      <option key={type} value={type}>
                        {getPurchaseTypeLabel(t, type)}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.status")}</label>
                  <Select
                    value={statusFilter}
                    onChange={(e) => {
                      setStatusFilter(e.target.value as PurchaseStatusFilter);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {PURCHASE_STATUSES.map((status) => (
                      <option key={status} value={status}>
                        {getPurchaseStatusLabel(t, status)}
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
                  <Link to={LICENSE_PURCHASE_CREATE_PATH} className={cn(buttonVariants())}>
                    {t("licenseManagement:actions.addPurchase")}
                  </Link>
                ) : null}
              </>
            }
          />
          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && items.length === 0 ? (
            <EmptyState title={t("licenseManagement:purchases.empty")} />
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
