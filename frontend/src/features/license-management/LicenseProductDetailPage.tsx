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
import { getLicensedProductById } from "@/features/license-management/api";
import { LicenseDetailField } from "@/features/license-management/components/LicenseDetailField";
import { LICENSE_PRODUCTS_LIST_PATH } from "@/features/license-management/license-products-list-path";
import { buildLicenseProductEditPath } from "@/features/license-management/license-product-detail-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

export function LicenseProductDetailPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageCatalog);

  const detailQuery = useQuery({
    queryKey: ["license-management", "products", "detail", id],
    queryFn: () => getLicensedProductById(id!),
    enabled: Boolean(id),
  });

  const isNotFound =
    detailQuery.isError
    && detailQuery.error instanceof AxiosError
    && detailQuery.error.response?.status === 404;

  if (!id) {
    return (
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <EmptyState title={t("licenseManagement:pages.products.detail.notFound")} />
      </section>
    );
  }

  const product = detailQuery.data;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={product?.name ?? t("licenseManagement:pages.products.detail.title")}
        description={t("licenseManagement:pages.products.detail.description")}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Link to={LICENSE_PRODUCTS_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
              {t("common:actions.back")}
            </Link>
            <Button type="button" variant="outline" onClick={() => detailQuery.refetch()} disabled={detailQuery.isFetching}>
              {t("common:actions.refresh")}
            </Button>
            {canManage && product ? (
              <Link to={buildLicenseProductEditPath(product.id)} className={cn(buttonVariants())}>
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
        <EmptyState title={t("licenseManagement:pages.products.detail.notFound")} />
      ) : null}
      {product ? (
        <SectionCard title={t("licenseManagement:pages.products.detail.summaryTitle")}>
          <div className="grid gap-4 md:grid-cols-2">
            <LicenseDetailField label={t("licenseManagement:table.productName")} value={product.name} />
            <LicenseDetailField label={t("common:fields.status")}>
              <StatusBadge isActive={product.isActive} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:form.brand")} value={product.brand} />
            <LicenseDetailField label={t("licenseManagement:table.category")} value={product.categoryName} />
            <LicenseDetailField label={t("licenseManagement:form.description")} value={product.description} valueClassName="whitespace-pre-wrap md:col-span-2" />
            <LicenseDetailField label={t("licenseManagement:pages.detail.createdAt")}>
              <DateTimeText value={product.createdAt} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:pages.detail.createdBy")} value={product.createdBy} />
            <LicenseDetailField label={t("licenseManagement:pages.detail.updatedAt")}>
              <DateTimeText value={product.updatedAt} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:pages.detail.updatedBy")} value={product.updatedBy} />
          </div>
        </SectionCard>
      ) : null}
    </section>
  );
}
