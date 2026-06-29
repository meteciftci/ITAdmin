import { useCallback, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { format } from "date-fns";
import type { DateRange } from "react-day-picker";

import { DateRangePicker } from "@/components/common/DateRangePicker";
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
import { getAllLicensedProducts, getLicenseRequests } from "@/features/license-management/api";
import {
  getRequestSourceLabel,
  getRequestStatusLabel,
  REQUEST_SOURCES,
  REQUEST_STATUSES,
} from "@/features/license-management/enum-labels";
import { createLicenseRequestColumns } from "@/features/license-management/license-columns";
import {
  buildLicenseRequestDetailPath,
  buildLicenseRequestEditPath,
  LICENSE_REQUEST_CREATE_PATH,
} from "@/features/license-management/license-request-paths";
import type { LicenseRequestSource, LicenseRequestStatus } from "@/features/license-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";
import { cn } from "@/lib/utils";

type RequestStatusFilter = "all" | LicenseRequestStatus;
type RequestSourceFilter = "all" | LicenseRequestSource;
type ProductFilter = "all" | string;

export function LicenseRequestsPage() {
  const { t, i18n } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageRequests);
  const dateLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<RequestStatusFilter>("all");
  const [sourceFilter, setSourceFilter] = useState<RequestSourceFilter>("all");
  const [productFilter, setProductFilter] = useState<ProductFilter>("all");
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const debouncedSearch = useDebouncedValue(search, 400);
  const effectiveSearch = debouncedSearch.trim().length >= 3 ? debouncedSearch.trim() : undefined;
  const requestDateFrom = dateRange?.from ? format(dateRange.from, "yyyy-MM-dd") : undefined;
  const requestDateTo = dateRange?.to ? format(dateRange.to, "yyyy-MM-dd") : undefined;
  const activeFilterCount =
    (statusFilter === "all" ? 0 : 1)
    + (sourceFilter === "all" ? 0 : 1)
    + (productFilter === "all" ? 0 : 1)
    + (dateRange?.from || dateRange?.to ? 1 : 0);

  const productsQuery = useQuery({
    queryKey: ["license-management", "products", "options"],
    queryFn: getAllLicensedProducts,
  });

  const listQuery = useQuery({
    queryKey: [
      "license-management",
      "requests",
      effectiveSearch,
      statusFilter,
      sourceFilter,
      productFilter,
      requestDateFrom,
      requestDateTo,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getLicenseRequests({
        search: effectiveSearch,
        status: statusFilter === "all" ? undefined : statusFilter,
        requestSource: sourceFilter === "all" ? undefined : sourceFilter,
        requestDateFrom,
        requestDateTo,
        productId: productFilter === "all" ? undefined : productFilter,
        pageNumber,
        pageSize,
      }),
  });

  const resolveRequestSourceLabel = useCallback(
    (value: string) => getRequestSourceLabel(t, value as LicenseRequestSource),
    [t],
  );

  const resolveRequestStatusLabel = useCallback(
    (value: string) => getRequestStatusLabel(t, value as LicenseRequestStatus),
    [t],
  );

  const columns = useMemo(
    () =>
      createLicenseRequestColumns({
        t,
        canManage,
        onDetail: (item) => navigate(buildLicenseRequestDetailPath(item.id)),
        onEdit: (item) => navigate(buildLicenseRequestEditPath(item.id)),
        getRequestSourceLabel: resolveRequestSourceLabel,
        getRequestStatusLabel: resolveRequestStatusLabel,
      }),
    [t, canManage, resolveRequestSourceLabel, resolveRequestStatusLabel, navigate],
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
      fromPath: "/license-management/requests",
      retryPath: "/license-management/requests",
      sourceLabel: t("licenseManagement:requests.listTitle"),
    });
    return <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />;
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("licenseManagement:requests.title")}
        description={t("licenseManagement:requests.description")}
      />
      <SectionCard title={t("licenseManagement:requests.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("licenseManagement:requests.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setStatusFilter("all");
              setSourceFilter("all");
              setProductFilter("all");
              setDateRange(undefined);
              setPageNumber(1);
            }}
            filterContent={
              <div className="space-y-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.status")}</label>
                  <Select
                    value={statusFilter}
                    onChange={(event) => {
                      setStatusFilter(event.target.value as RequestStatusFilter);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {REQUEST_STATUSES.map((status) => (
                      <option key={status} value={status}>
                        {getRequestStatusLabel(t, status)}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">
                    {t("licenseManagement:requests.fields.requestSource")}
                  </label>
                  <Select
                    value={sourceFilter}
                    onChange={(event) => {
                      setSourceFilter(event.target.value as RequestSourceFilter);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {REQUEST_SOURCES.map((source) => (
                      <option key={source} value={source}>
                        {getRequestSourceLabel(t, source)}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:requests.fields.product")}</label>
                  <Select
                    value={productFilter}
                    onChange={(event) => {
                      setProductFilter(event.target.value as ProductFilter);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {(productsQuery.data ?? []).map((product) => (
                      <option key={product.id} value={product.id}>
                        {product.name}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:requests.fields.requestDate")}</label>
                  <DateRangePicker
                    value={dateRange}
                    onChange={(value) => {
                      setDateRange(value);
                      setPageNumber(1);
                    }}
                    placeholder={t("common:dateRange.placeholder")}
                    clearLabel={t("common:dateRange.clear")}
                    locale={dateLocale}
                  />
                </div>
              </div>
            }
            actions={
              <>
                <Button variant="outline" onClick={() => listQuery.refetch()} disabled={listQuery.isFetching}>
                  {t("common:actions.refresh")}
                </Button>
                {canManage ? (
                  <Link to={LICENSE_REQUEST_CREATE_PATH} className={cn(buttonVariants())}>
                    {t("licenseManagement:requests.actions.create")}
                  </Link>
                ) : null}
              </>
            }
          />
          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && items.length === 0 ? (
            <EmptyState title={t("licenseManagement:requests.empty")} />
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
