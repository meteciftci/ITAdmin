import { useCallback, useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, Navigate, useNavigate, useSearchParams } from "react-router-dom";
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
import { formatLicensedProductLabel } from "@/features/license-management/product-labels";
import {
  buildLicenseRequestsListPath,
  parseLicenseRequestsListStateFromUrl,
  type LicenseRequestsListState,
} from "@/features/license-management/license-request-list-query";
import {
  buildLicenseRequestDetailPath,
  buildLicenseRequestEditPath,
  LICENSE_REQUEST_CREATE_PATH,
} from "@/features/license-management/license-request-paths";
import { buildLicenseRequestReturnState } from "@/features/license-management/license-request-return-path";
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

function toListStatePath(state: LicenseRequestsListState): string {
  const path = buildLicenseRequestsListPath(state);
  const queryIndex = path.indexOf("?");
  return queryIndex >= 0 ? path.slice(queryIndex + 1) : "";
}

export function LicenseRequestsPage() {
  const { t, i18n } = useTranslation(["licenseManagement", "common"]);
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageRequests);
  const dateLocale = i18n.language.startsWith("tr") ? "tr" : "en";

  const listState = useMemo(
    () => parseLicenseRequestsListStateFromUrl(searchParams),
    [searchParams],
  );

  const listUrlKey = searchParams.toString();

  return (
    <LicenseRequestsListContent
      key={listUrlKey}
      listState={listState}
      setSearchParams={setSearchParams}
      canManage={canManage}
      dateLocale={dateLocale}
      t={t}
    />
  );
}

type LicenseRequestsListContentProps = {
  listState: LicenseRequestsListState;
  setSearchParams: ReturnType<typeof useSearchParams>[1];
  canManage: boolean;
  dateLocale: "tr" | "en";
  t: ReturnType<typeof useTranslation<["licenseManagement", "common"]>>["t"];
};

function LicenseRequestsListContent({
  listState,
  setSearchParams,
  canManage,
  dateLocale,
  t,
}: LicenseRequestsListContentProps) {
  const navigate = useNavigate();
  const [search, setSearch] = useState(listState.search);
  const debouncedSearch = useDebouncedValue(search, 400);

  const applyListState = useCallback((patch: Partial<LicenseRequestsListState>) => {
    const next = { ...listState, ...patch };
    setSearchParams(new URLSearchParams(toListStatePath(next)), { replace: true });
  }, [listState, setSearchParams]);

  useEffect(() => {
    const trimmed = debouncedSearch.trim();
    if (trimmed === listState.search) {
      return;
    }

    applyListState({ search: trimmed, pageNumber: 1 });
  }, [applyListState, debouncedSearch, listState.search]);

  const statusFilter = listState.status as RequestStatusFilter;
  const sourceFilter = listState.requestSource as RequestSourceFilter;
  const productFilter = listState.productId as ProductFilter;
  const dateRange = listState.dateRange;
  const pageNumber = listState.pageNumber;
  const pageSize = listState.pageSize;

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

  const currentListPath = useMemo(() => buildLicenseRequestsListPath(listState), [listState]);

  const columns = useMemo(
    () =>
      createLicenseRequestColumns({
        t,
        canManage,
        onDetail: (item) => navigate(buildLicenseRequestDetailPath(item.id)),
        onEdit: (item) =>
          navigate(buildLicenseRequestEditPath(item.id), {
            state: buildLicenseRequestReturnState(currentListPath),
          }),
        getRequestSourceLabel: resolveRequestSourceLabel,
        getRequestStatusLabel: resolveRequestStatusLabel,
      }),
    [t, canManage, resolveRequestSourceLabel, resolveRequestStatusLabel, navigate, currentListPath],
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
      retryPath: currentListPath,
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
            onSearchChange={setSearch}
            searchPlaceholder={t("licenseManagement:requests.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setSearch("");
              setSearchParams(new URLSearchParams(), { replace: true });
            }}
            filterContent={
              <div className="space-y-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.status")}</label>
                  <Select
                    value={statusFilter}
                    onChange={(event) => {
                      applyListState({
                        status: event.target.value as RequestStatusFilter,
                        pageNumber: 1,
                      });
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
                      applyListState({
                        requestSource: event.target.value as RequestSourceFilter,
                        pageNumber: 1,
                      });
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
                      applyListState({
                        productId: event.target.value as ProductFilter,
                        pageNumber: 1,
                      });
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {(productsQuery.data ?? []).map((product) => (
                      <option key={product.id} value={product.id}>
                        {formatLicensedProductLabel(product)}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:requests.fields.requestDate")}</label>
                  <DateRangePicker
                    value={dateRange}
                    onChange={(value: DateRange | undefined) => {
                      applyListState({ dateRange: value, pageNumber: 1 });
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
                    onPageChange={(nextPage) => applyListState({ pageNumber: nextPage })}
                    onPageSizeChange={(size) => {
                      applyListState({ pageSize: size, pageNumber: 1 });
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
