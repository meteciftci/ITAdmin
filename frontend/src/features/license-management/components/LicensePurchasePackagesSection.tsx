import { useCallback, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { DataTable } from "@/components/common/data-table";
import { useClientDataTable } from "@/components/common/data-table-hooks";
import { EmptyState } from "@/components/common/EmptyState";
import { LoadingState } from "@/components/common/LoadingState";
import { buttonVariants } from "@/components/ui/button-variants";
import { getLicensePackages } from "@/features/license-management/api";
import {
  getLicenseTypeLabel,
  getPackageStatusLabel,
} from "@/features/license-management/enum-labels";
import { createLicensePackageColumns } from "@/features/license-management/license-columns";
import {
  buildLicensePackageCreatePath,
  buildLicensePackageDetailPath,
  buildLicensePackageEditPath,
} from "@/features/license-management/license-package-detail-path";
import { buildLicensePackagesListPath } from "@/features/license-management/license-packages-list-path";
import type { LicensePackageStatus, LicenseType } from "@/features/license-management/types";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

type Props = {
  purchaseId: string;
};

export function LicensePurchasePackagesSection({ purchaseId }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManagePurchases);

  const packagesQuery = useQuery({
    queryKey: ["license-management", "packages", "purchase-detail", purchaseId],
    queryFn: () =>
      getLicensePackages({
        purchaseId,
        pageNumber: 1,
        pageSize: 10,
      }),
    enabled: Boolean(purchaseId),
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
        showPurchaseColumn: false,
        onDetail: (item) => navigate(buildLicensePackageDetailPath(item.id)),
        onEdit: (item) => navigate(buildLicensePackageEditPath(item.id)),
        getLicenseTypeLabel: resolveLicenseTypeLabel,
        getPackageStatusLabel: resolvePackageStatusLabel,
      }),
    [t, canManage, resolveLicenseTypeLabel, resolvePackageStatusLabel, navigate],
  );

  const items = packagesQuery.data?.items ?? [];
  const table = useClientDataTable({
    data: items,
    columns,
    initialPageSize: 10,
    enablePagination: items.length > 10,
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-base font-semibold">
          {t("licenseManagement:pages.purchases.detail.linkedPackagesTitle")}
        </h3>
        <div className="flex flex-wrap items-center gap-2">
          <Link
            to={buildLicensePackagesListPath(purchaseId)}
            className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
          >
            {t("licenseManagement:pages.purchases.detail.viewAllPackages")}
          </Link>
          {canManage ? (
            <Link
              to={buildLicensePackageCreatePath(purchaseId)}
              className={cn(buttonVariants({ size: "sm" }))}
            >
              {t("licenseManagement:pages.purchases.detail.addPackage")}
            </Link>
          ) : null}
        </div>
      </div>

      {packagesQuery.isLoading ? <LoadingState /> : null}
      {!packagesQuery.isLoading && items.length === 0 ? (
        <EmptyState title={t("licenseManagement:pages.purchases.detail.linkedPackagesEmpty")} />
      ) : null}
      {items.length > 0 ? <DataTable table={table} /> : null}
    </div>
  );
}
