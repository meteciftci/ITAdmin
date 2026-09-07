import { useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { ColumnDef } from "@tanstack/react-table";

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
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { buildAdUserDetailPath } from "@/features/ad-management/ad-user-detail-path";
import { isGuidLike } from "@/features/ad-management/ad-user-detail-utils";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { useAuthStore } from "@/features/auth/auth-store";
import { getLicenseSeatAssignments } from "@/features/license-management/api";
import { LicenseAssignmentDialog } from "@/features/license-management/components/LicenseAssignmentDialog";
import { buildLicensePackageDetailPath } from "@/features/license-management/license-package-detail-path";
import type {
  LicenseSeatAssignmentListItem,
  LicenseSeatAssignmentStatus,
} from "@/features/license-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

const SEAT_STATUSES: LicenseSeatAssignmentStatus[] = ["Active", "Released", "Transferred"];

function statusVariant(status: LicenseSeatAssignmentStatus): "default" | "secondary" | "outline" {
  if (status === "Active") return "default";
  if (status === "Transferred") return "outline";
  return "secondary";
}

export function LicenseSeatAssignmentsPage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.FulfillRequests);
  const adStatus = useAdManagementModuleStatus();
  const adLinksEnabled = adStatus.isOperational;

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | LicenseSeatAssignmentStatus>("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [assignOpen, setAssignOpen] = useState(false);

  const debouncedSearch = useDebouncedValue(search, 400);
  const effectiveSearch = debouncedSearch.trim().length >= 3 ? debouncedSearch.trim() : undefined;
  const activeFilterCount = statusFilter === "all" ? 0 : 1;

  const listQuery = useQuery({
    queryKey: ["license-management", "seat-assignments", effectiveSearch, statusFilter, pageNumber, pageSize],
    queryFn: () =>
      getLicenseSeatAssignments({
        search: effectiveSearch,
        status: statusFilter === "all" ? undefined : statusFilter,
        pageNumber,
        pageSize,
      }),
  });

  const columns = useMemo<ColumnDef<LicenseSeatAssignmentListItem, unknown>[]>(
    () => [
      {
        accessorKey: "displayName",
        header: t("licenseManagement:seats.columns.person"),
        cell: ({ row }) => {
          const seat = row.original;
          return (
            <div>
              {adLinksEnabled && seat.adObjectId && isGuidLike(seat.adObjectId) ? (
                <Link
                  to={buildAdUserDetailPath(seat.adObjectId)}
                  className="font-medium text-primary underline-offset-2 hover:underline"
                >
                  {seat.displayName}
                </Link>
              ) : (
                <div className="font-medium">{seat.displayName}</div>
              )}
              {seat.nationalId ? (
                <div className="text-xs text-muted-foreground">{seat.nationalId}</div>
              ) : null}
            </div>
          );
        },
      },
      {
        accessorKey: "mail",
        header: t("licenseManagement:seats.columns.contact"),
        cell: ({ row }) => (
          <div>
            <div>{row.original.mail ?? "-"}</div>
            {row.original.department ? (
              <div className="text-xs text-muted-foreground">{row.original.department}</div>
            ) : null}
          </div>
        ),
      },
      {
        accessorKey: "productName",
        header: t("licenseManagement:seats.list.columns.product"),
        cell: ({ row }) => (
          <div>
            <div>{row.original.productName}</div>
            {row.original.productBrand ? (
              <div className="text-xs text-muted-foreground">{row.original.productBrand}</div>
            ) : null}
          </div>
        ),
      },
      {
        accessorKey: "purchaseTitle",
        header: t("licenseManagement:seats.list.columns.package"),
        cell: ({ row }) => (
          <Link
            to={buildLicensePackageDetailPath(row.original.packageId)}
            className="text-primary underline-offset-2 hover:underline"
          >
            {row.original.purchaseTitle}
          </Link>
        ),
      },
      {
        accessorKey: "assignedDate",
        header: t("licenseManagement:seats.columns.assignedDate"),
        cell: ({ row }) => (
          <DateTimeText
            value={row.original.assignedDate}
            options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
          />
        ),
      },
      {
        accessorKey: "status",
        header: t("common:fields.status"),
        cell: ({ row }) => (
          <Badge variant={statusVariant(row.original.status)}>
            {t(`licenseManagement:seats.status.${row.original.status}`)}
          </Badge>
        ),
      },
    ],
    [t, adLinksEnabled],
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
      fromPath: "/license-management/seat-assignments",
      retryPath: "/license-management/seat-assignments",
      sourceLabel: t("licenseManagement:seats.list.title"),
    });
    return <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />;
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("licenseManagement:seats.list.title")}
        description={t("licenseManagement:seats.list.description")}
      />
      <SectionCard title={t("licenseManagement:seats.list.cardTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("licenseManagement:seats.list.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setStatusFilter("all");
              setPageNumber(1);
            }}
            filterContent={
              <div className="space-y-2">
                <label className="text-sm font-medium">{t("licenseManagement:seats.list.filters.status")}</label>
                <Select
                  value={statusFilter}
                  onChange={(e) => {
                    setStatusFilter(e.target.value as "all" | LicenseSeatAssignmentStatus);
                    setPageNumber(1);
                  }}
                  className="w-full"
                >
                  <option value="all">{t("common:status.all")}</option>
                  {SEAT_STATUSES.map((status) => (
                    <option key={status} value={status}>
                      {t(`licenseManagement:seats.status.${status}`)}
                    </option>
                  ))}
                </Select>
              </div>
            }
            actions={
              <>
                <Button variant="outline" onClick={() => listQuery.refetch()} disabled={listQuery.isFetching}>
                  {t("common:actions.refresh")}
                </Button>
                {canManage ? (
                  <Button onClick={() => setAssignOpen(true)}>
                    {t("licenseManagement:seats.actions.assign")}
                  </Button>
                ) : null}
              </>
            }
          />
          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && items.length === 0 ? (
            <EmptyState title={t("licenseManagement:seats.list.empty")} />
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

      {assignOpen ? (
        <LicenseAssignmentDialog
          onClose={() => setAssignOpen(false)}
          onDone={() => {
            setAssignOpen(false);
            void queryClient.invalidateQueries({
              queryKey: ["license-management", "seat-assignments"],
            });
          }}
        />
      ) : null}
    </section>
  );
}
