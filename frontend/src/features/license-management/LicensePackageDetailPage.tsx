import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { useAuthStore } from "@/features/auth/auth-store";
import { getLicensePackageById } from "@/features/license-management/api";
import { LicenseDetailField } from "@/features/license-management/components/LicenseDetailField";
import { getLicenseTypeLabel, getPackageStatusLabel, maskLicenseKey } from "@/features/license-management/enum-labels";
import { LICENSE_PACKAGES_LIST_PATH } from "@/features/license-management/license-packages-list-path";
import { buildLicensePackageEditPath } from "@/features/license-management/license-package-detail-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

function DateOnlyField({ label, value }: { label: string; value: string | null }) {
  return (
    <LicenseDetailField label={label}>
      {value ? (
        <DateTimeText value={value} options={{ year: "numeric", month: "2-digit", day: "2-digit" }} />
      ) : (
        <span className="text-sm">-</span>
      )}
    </LicenseDetailField>
  );
}

export function LicensePackageDetailPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManagePurchases);
  const [showLicenseKey, setShowLicenseKey] = useState(false);

  const detailQuery = useQuery({
    queryKey: ["license-management", "packages", "detail", id],
    queryFn: () => getLicensePackageById(id!),
    enabled: Boolean(id),
  });

  const isNotFound =
    detailQuery.isError
    && detailQuery.error instanceof AxiosError
    && detailQuery.error.response?.status === 404;

  if (!id) {
    return (
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <EmptyState title={t("licenseManagement:pages.packages.detail.notFound")} />
      </section>
    );
  }

  const pkg = detailQuery.data;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={pkg?.productName ?? t("licenseManagement:pages.packages.detail.title")}
        description={pkg?.purchaseTitle}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Link to={LICENSE_PACKAGES_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
              {t("common:actions.back")}
            </Link>
            <Button type="button" variant="outline" onClick={() => detailQuery.refetch()} disabled={detailQuery.isFetching}>
              {t("common:actions.refresh")}
            </Button>
            {canManage && pkg ? (
              <Link to={buildLicensePackageEditPath(pkg.id)} className={cn(buttonVariants())}>
                {t("common:actions.edit")}
              </Link>
            ) : null}
          </div>
        }
      />
      {detailQuery.isLoading ? <LoadingState /> : null}
      {detailQuery.isError && !isNotFound ? (
        <ErrorState
          title={t("errors:generic.title")}
          description={getApiErrorMessage(detailQuery.error, t("errors:generic.description"))}
        />
      ) : null}
      {isNotFound ? (
        <EmptyState title={t("licenseManagement:pages.packages.detail.notFound")} />
      ) : null}
      {pkg ? (
        <SectionCard title={t("licenseManagement:pages.packages.detail.summaryTitle")}>
          <div className="grid gap-4 md:grid-cols-2">
            <LicenseDetailField label={t("licenseManagement:table.product")} value={pkg.productName} />
            <LicenseDetailField label={t("licenseManagement:table.purchase")} value={pkg.purchaseTitle} />
            <LicenseDetailField
              label={t("licenseManagement:table.licenseType")}
              value={getLicenseTypeLabel(t, pkg.licenseType)}
            />
            <LicenseDetailField label={t("common:fields.status")}>
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm">{getPackageStatusLabel(t, pkg.status)}</span>
                <StatusBadge isActive={pkg.isActive} />
              </div>
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:table.quantity")} value={String(pkg.quantity)} />
            <LicenseDetailField label={t("licenseManagement:table.usedQuantity")} value={String(pkg.usedQuantity)} />
            <LicenseDetailField label={t("licenseManagement:table.availableQuantity")} value={String(pkg.availableQuantity)} />
            <DateOnlyField label={t("licenseManagement:table.startDate")} value={pkg.startDate} />
            <DateOnlyField label={t("licenseManagement:table.endDate")} value={pkg.endDate} />
            <LicenseDetailField
              label={t("licenseManagement:table.isPerpetual")}
              value={pkg.isPerpetual ? t("licenseManagement:boolean.yes") : t("licenseManagement:boolean.no")}
            />
            <LicenseDetailField
              label={t("licenseManagement:table.renewalRequired")}
              value={pkg.renewalRequired ? t("licenseManagement:boolean.yes") : t("licenseManagement:boolean.no")}
            />
            <DateOnlyField label={t("licenseManagement:form.renewalDate")} value={pkg.renewalDate} />
            <LicenseDetailField label={t("licenseManagement:form.serialNumber")} value={pkg.serialNumber} />
            <LicenseDetailField label={t("licenseManagement:form.licenseKey")}>
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-mono text-sm">
                  {pkg.licenseKey
                    ? showLicenseKey
                      ? pkg.licenseKey
                      : maskLicenseKey(pkg.licenseKey)
                    : "-"}
                </span>
                {pkg.licenseKey ? (
                  <Button type="button" variant="outline" size="sm" onClick={() => setShowLicenseKey((prev) => !prev)}>
                    {showLicenseKey
                      ? t("licenseManagement:pages.packages.detail.hideLicenseKey")
                      : t("licenseManagement:pages.packages.detail.showLicenseKey")}
                  </Button>
                ) : null}
              </div>
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:form.licenseAccountEmail")} value={pkg.licenseAccountEmail} />
            <LicenseDetailField label={t("licenseManagement:form.licensePortalUrl")} value={pkg.licensePortalUrl} valueClassName="break-all" />
            <LicenseDetailField label={t("licenseManagement:form.licenseNotes")} value={pkg.licenseNotes} valueClassName="whitespace-pre-wrap md:col-span-2" />
            <LicenseDetailField label={t("licenseManagement:pages.detail.createdAt")}>
              <DateTimeText value={pkg.createdAt} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:pages.detail.createdBy")} value={pkg.createdBy} />
            <LicenseDetailField label={t("licenseManagement:pages.detail.updatedAt")}>
              <DateTimeText value={pkg.updatedAt} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:pages.detail.updatedBy")} value={pkg.updatedBy} />
          </div>
        </SectionCard>
      ) : null}
    </section>
  );
}
