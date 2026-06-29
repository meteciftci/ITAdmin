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
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { useAuthStore } from "@/features/auth/auth-store";
import { getLicensePurchaseById } from "@/features/license-management/api";
import { LicenseDetailField } from "@/features/license-management/components/LicenseDetailField";
import { LicensePurchasePackagesSection } from "@/features/license-management/components/LicensePurchasePackagesSection";
import { getPurchaseStatusLabel, getPurchaseTypeLabel } from "@/features/license-management/enum-labels";
import { LICENSE_PURCHASES_LIST_PATH } from "@/features/license-management/license-purchases-list-path";
import { buildLicensePurchaseEditPath } from "@/features/license-management/license-purchase-detail-path";
import { isPurchaseFieldVisible } from "@/features/license-management/purchase-form-fields";
import type { LicensePurchaseType } from "@/features/license-management/types";
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

function showField(
  purchaseType: LicensePurchaseType,
  field: Parameters<typeof isPurchaseFieldVisible>[0],
): boolean {
  return isPurchaseFieldVisible(field, purchaseType);
}

export function LicensePurchaseDetailPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageAcquisitions);

  const detailQuery = useQuery({
    queryKey: ["license-management", "purchases", "detail", id],
    queryFn: () => getLicensePurchaseById(id!),
    enabled: Boolean(id),
  });

  const isNotFound =
    detailQuery.isError
    && detailQuery.error instanceof AxiosError
    && detailQuery.error.response?.status === 404;

  if (!id) {
    return (
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <EmptyState title={t("licenseManagement:pages.purchases.detail.notFound")} />
      </section>
    );
  }

  const purchase = detailQuery.data;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={purchase?.title ?? t("licenseManagement:pages.purchases.detail.title")}
        description={t("licenseManagement:pages.purchases.detail.description")}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Link to={LICENSE_PURCHASES_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
              {t("common:actions.back")}
            </Link>
            <Button type="button" variant="outline" onClick={() => detailQuery.refetch()} disabled={detailQuery.isFetching}>
              {t("common:actions.refresh")}
            </Button>
            {canManage && purchase ? (
              <Link to={buildLicensePurchaseEditPath(purchase.id)} className={cn(buttonVariants())}>
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
        <EmptyState title={t("licenseManagement:pages.purchases.detail.notFound")} />
      ) : null}
      {purchase ? (
        <>
          <SectionCard title={t("licenseManagement:pages.purchases.detail.summaryTitle")}>
            <div className="grid gap-4 md:grid-cols-2">
              <LicenseDetailField label={t("licenseManagement:table.purchaseTitle")} value={purchase.title} />
              <LicenseDetailField
                label={t("licenseManagement:table.purchaseType")}
                value={getPurchaseTypeLabel(t, purchase.purchaseType)}
              />
              <LicenseDetailField
                label={t("common:fields.status")}
                value={getPurchaseStatusLabel(t, purchase.status)}
              />
              <DateOnlyField label={t("licenseManagement:table.purchaseDate")} value={purchase.purchaseDate} />
              <LicenseDetailField
                label={t("licenseManagement:table.supplierCompany")}
                value={purchase.supplierCompanyName}
              />
              <LicenseDetailField
                label={t("licenseManagement:table.supportCompany")}
                value={purchase.supportCompanyName}
              />
              {showField(purchase.purchaseType, "contractNumber") ? (
                <LicenseDetailField
                  label={t("licenseManagement:table.contractNumber")}
                  value={purchase.contractNumber}
                />
              ) : null}
              {showField(purchase.purchaseType, "contractStartDate") ? (
                <DateOnlyField
                  label={t("licenseManagement:form.contractStartDate")}
                  value={purchase.contractStartDate}
                />
              ) : null}
              {showField(purchase.purchaseType, "contractEndDate") ? (
                <DateOnlyField
                  label={t("licenseManagement:form.contractEndDate")}
                  value={purchase.contractEndDate}
                />
              ) : null}
              {showField(purchase.purchaseType, "tenderNumber") ? (
                <LicenseDetailField label={t("licenseManagement:form.tenderNumber")} value={purchase.tenderNumber} />
              ) : null}
              {showField(purchase.purchaseType, "tenderDate") ? (
                <DateOnlyField label={t("licenseManagement:form.tenderDate")} value={purchase.tenderDate} />
              ) : null}
              {showField(purchase.purchaseType, "directPurchaseNumber") ? (
                <LicenseDetailField
                  label={t("licenseManagement:form.directPurchaseNumber")}
                  value={purchase.directPurchaseNumber}
                />
              ) : null}
              {showField(purchase.purchaseType, "dmoOrderNumber") ? (
                <LicenseDetailField label={t("licenseManagement:form.dmoOrderNumber")} value={purchase.dmoOrderNumber} />
              ) : null}
              {showField(purchase.purchaseType, "ebysNumber") ? (
                <LicenseDetailField label={t("licenseManagement:form.ebysNumber")} value={purchase.ebysNumber} />
              ) : null}
              {showField(purchase.purchaseType, "ebysDate") ? (
                <DateOnlyField label={t("licenseManagement:form.ebysDate")} value={purchase.ebysDate} />
              ) : null}
              {showField(purchase.purchaseType, "invoiceNumber") ? (
                <LicenseDetailField label={t("licenseManagement:form.invoiceNumber")} value={purchase.invoiceNumber} />
              ) : null}
              {showField(purchase.purchaseType, "invoiceDate") ? (
                <DateOnlyField label={t("licenseManagement:form.invoiceDate")} value={purchase.invoiceDate} />
              ) : null}
              {showField(purchase.purchaseType, "actualTotalCost") ? (
                <LicenseDetailField
                  label={t("licenseManagement:form.actualTotalCost")}
                  value={purchase.actualTotalCost != null ? String(purchase.actualTotalCost) : null}
                />
              ) : null}
              {showField(purchase.purchaseType, "currency") ? (
                <LicenseDetailField label={t("licenseManagement:form.currency")} value={purchase.currency} />
              ) : null}
              {showField(purchase.purchaseType, "vatIncluded") ? (
                <LicenseDetailField
                  label={t("licenseManagement:form.vatIncluded")}
                  value={
                    purchase.vatIncluded == null
                      ? "-"
                      : purchase.vatIncluded
                        ? t("licenseManagement:boolean.yes")
                        : t("licenseManagement:boolean.no")
                  }
                />
              ) : null}
              <LicenseDetailField
                label={t("licenseManagement:form.description")}
                value={purchase.description}
                valueClassName="whitespace-pre-wrap md:col-span-2"
              />
              <LicenseDetailField
                label={t("licenseManagement:form.notes")}
                value={purchase.notes}
                valueClassName="whitespace-pre-wrap md:col-span-2"
              />
              <LicenseDetailField label={t("licenseManagement:pages.detail.createdAt")}>
                <DateTimeText value={purchase.createdAt} />
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:pages.detail.createdBy")} value={purchase.createdBy} />
              <LicenseDetailField label={t("licenseManagement:pages.detail.updatedAt")}>
                <DateTimeText value={purchase.updatedAt} />
              </LicenseDetailField>
              <LicenseDetailField label={t("licenseManagement:pages.detail.updatedBy")} value={purchase.updatedBy} />
            </div>
          </SectionCard>
          <SectionCard>
            <LicensePurchasePackagesSection purchaseId={purchase.id} />
          </SectionCard>
        </>
      ) : null}
    </section>
  );
}
