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
import { AcquisitionFormDialog } from "@/features/license-management/components/AcquisitionFormDialog";
import { getLicenseAcquisitionById, getLicenseAcquisitions } from "@/features/license-management/api";
import {
  ACQUISITION_STATUSES,
  ACQUISITION_TYPES,
  getAcquisitionStatusLabel,
  getAcquisitionTypeLabel,
} from "@/features/license-management/enum-labels";
import { createLicenseAcquisitionColumns } from "@/features/license-management/license-columns";
import type {
  LicenseAcquisitionListItem,
  LicenseAcquisitionStatus,
  LicenseAcquisitionType,
} from "@/features/license-management/types";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

type AcquisitionTypeFilter = "all" | LicenseAcquisitionType;
type AcquisitionStatusFilter = "all" | LicenseAcquisitionStatus;

export function LicenseAcquisitionsPage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageAcquisitions);

  const [search, setSearch] = useState("");
  const [acquisitionTypeFilter, setAcquisitionTypeFilter] = useState<AcquisitionTypeFilter>("all");
  const [statusFilter, setStatusFilter] = useState<AcquisitionStatusFilter>("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [showCreate, setShowCreate] = useState(false);
  const [selectedEdit, setSelectedEdit] = useState<LicenseAcquisitionListItem | null>(null);
  const [selectedDetail, setSelectedDetail] = useState<LicenseAcquisitionListItem | null>(null);

  const debouncedSearch = useDebouncedValue(search, 400);
  const effectiveSearch = debouncedSearch.trim().length >= 3 ? debouncedSearch.trim() : undefined;
  const activeFilterCount =
    (acquisitionTypeFilter === "all" ? 0 : 1) + (statusFilter === "all" ? 0 : 1);

  const listQuery = useQuery({
    queryKey: [
      "license-management",
      "acquisitions",
      effectiveSearch,
      acquisitionTypeFilter,
      statusFilter,
      pageNumber,
      pageSize,
    ],
    queryFn: () =>
      getLicenseAcquisitions({
        search: effectiveSearch,
        acquisitionType: acquisitionTypeFilter === "all" ? undefined : acquisitionTypeFilter,
        status: statusFilter === "all" ? undefined : statusFilter,
        pageNumber,
        pageSize,
      }),
  });

  const detailQuery = useQuery({
    queryKey: ["license-management", "acquisitions", "detail", selectedDetail?.id],
    queryFn: () => getLicenseAcquisitionById(selectedDetail!.id),
    enabled: Boolean(selectedDetail?.id),
  });

  const editDetailQuery = useQuery({
    queryKey: ["license-management", "acquisitions", "edit", selectedEdit?.id],
    queryFn: () => getLicenseAcquisitionById(selectedEdit!.id),
    enabled: Boolean(selectedEdit?.id),
  });

  const resolveAcquisitionTypeLabel = useCallback(
    (value: string) => getAcquisitionTypeLabel(t, value as LicenseAcquisitionType),
    [t],
  );

  const resolveAcquisitionStatusLabel = useCallback(
    (value: string) => getAcquisitionStatusLabel(t, value as LicenseAcquisitionStatus),
    [t],
  );

  const columns = useMemo(
    () =>
      createLicenseAcquisitionColumns({
        t,
        canManage,
        onDetail: setSelectedDetail,
        onEdit: setSelectedEdit,
        getAcquisitionTypeLabel: resolveAcquisitionTypeLabel,
        getAcquisitionStatusLabel: resolveAcquisitionStatusLabel,
      }),
    [t, canManage, resolveAcquisitionTypeLabel, resolveAcquisitionStatusLabel],
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
    queryClient.invalidateQueries({ queryKey: ["license-management", "acquisitions"] });
    queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
    toast.success(
      selectedEdit
        ? t("licenseManagement:messages.acquisitionUpdated")
        : t("licenseManagement:messages.acquisitionCreated"),
    );
    setSelectedEdit(null);
    setShowCreate(false);
  }, [queryClient, selectedEdit, t]);

  if (listQuery.isError) {
    const routeState = createApiErrorRouteState(listQuery.error, {
      fromPath: "/license-management/acquisitions",
      retryPath: "/license-management/acquisitions",
      sourceLabel: t("licenseManagement:acquisitions.listTitle"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("licenseManagement:acquisitions.title")}
        description={t("licenseManagement:acquisitions.description")}
      />
      <SectionCard title={t("licenseManagement:acquisitions.listTitle")}>
        <div className="space-y-4">
          <DataTableToolbar
            searchValue={search}
            onSearchChange={(value) => {
              setSearch(value);
              setPageNumber(1);
            }}
            searchPlaceholder={t("licenseManagement:acquisitions.searchPlaceholder")}
            activeFilterCount={activeFilterCount}
            onClearFilters={() => {
              setAcquisitionTypeFilter("all");
              setStatusFilter("all");
              setPageNumber(1);
            }}
            filterContent={
              <div className="space-y-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.acquisitionType")}</label>
                  <Select
                    value={acquisitionTypeFilter}
                    onChange={(e) => {
                      setAcquisitionTypeFilter(e.target.value as AcquisitionTypeFilter);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {ACQUISITION_TYPES.map((type) => (
                      <option key={type} value={type}>
                        {getAcquisitionTypeLabel(t, type)}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t("licenseManagement:filters.status")}</label>
                  <Select
                    value={statusFilter}
                    onChange={(e) => {
                      setStatusFilter(e.target.value as AcquisitionStatusFilter);
                      setPageNumber(1);
                    }}
                    className="w-full"
                  >
                    <option value="all">{t("common:status.all")}</option>
                    {ACQUISITION_STATUSES.map((status) => (
                      <option key={status} value={status}>
                        {getAcquisitionStatusLabel(t, status)}
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
                  <Button onClick={() => setShowCreate(true)}>{t("licenseManagement:actions.addAcquisition")}</Button>
                ) : null}
              </>
            }
          />
          {listQuery.isLoading ? <LoadingState /> : null}
          {!listQuery.isLoading && items.length === 0 ? (
            <EmptyState title={t("licenseManagement:acquisitions.empty")} />
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

      <AcquisitionFormDialog
        open={showCreate}
        mode="create"
        onClose={() => setShowCreate(false)}
        onSaved={handleSaved}
      />
      <AcquisitionFormDialog
        open={Boolean(selectedEdit)}
        mode="edit"
        acquisition={editDetailQuery.data ?? null}
        onClose={() => setSelectedEdit(null)}
        onSaved={handleSaved}
      />

      <Dialog open={Boolean(selectedDetail)}>
        <DialogContent onOpenChange={(open) => !open && setSelectedDetail(null)}>
          <DialogHeader>
            <DialogTitle>{detailQuery.data?.title ?? selectedDetail?.title}</DialogTitle>
          </DialogHeader>
          <DialogBody className="space-y-2 text-sm">
            {detailQuery.isLoading ? <LoadingState /> : null}
            {detailQuery.data ? (
              <>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.acquisitionType")}:</span>{" "}
                  {getAcquisitionTypeLabel(t, detailQuery.data.acquisitionType)}
                </p>
                <p>
                  <span className="font-medium">{t("common:fields.status")}:</span>{" "}
                  {getAcquisitionStatusLabel(t, detailQuery.data.status)}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.acquisitionDate")}:</span>{" "}
                  {detailQuery.data.acquisitionDate ? (
                    <DateTimeText
                      value={detailQuery.data.acquisitionDate}
                      options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
                    />
                  ) : (
                    "-"
                  )}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.supplierCompany")}:</span>{" "}
                  {detailQuery.data.supplierCompanyName ?? "-"}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.supportCompany")}:</span>{" "}
                  {detailQuery.data.supportCompanyName ?? "-"}
                </p>
                <p>
                  <span className="font-medium">{t("licenseManagement:table.contractNumber")}:</span>{" "}
                  {detailQuery.data.contractNumber ?? "-"}
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
    </section>
  );
}
