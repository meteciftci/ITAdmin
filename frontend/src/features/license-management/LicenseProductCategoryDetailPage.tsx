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
import { getLicenseProductCategoryById } from "@/features/license-management/api";
import { LicenseDetailField } from "@/features/license-management/components/LicenseDetailField";
import { LICENSE_CATEGORIES_LIST_PATH } from "@/features/license-management/license-categories-list-path";
import { buildLicenseCategoryEditPath } from "@/features/license-management/license-category-detail-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

export function LicenseProductCategoryDetailPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageCatalog);

  const detailQuery = useQuery({
    queryKey: ["license-management", "product-categories", "detail", id],
    queryFn: () => getLicenseProductCategoryById(id!),
    enabled: Boolean(id),
  });

  const isNotFound =
    detailQuery.isError
    && detailQuery.error instanceof AxiosError
    && detailQuery.error.response?.status === 404;

  if (!id) {
    return (
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <EmptyState title={t("licenseManagement:pages.categories.detail.notFound")} />
      </section>
    );
  }

  const category = detailQuery.data;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={category?.name ?? t("licenseManagement:pages.categories.detail.title")}
        description={t("licenseManagement:pages.categories.detail.description")}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Link to={LICENSE_CATEGORIES_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
              {t("common:actions.back")}
            </Link>
            <Button type="button" variant="outline" onClick={() => detailQuery.refetch()} disabled={detailQuery.isFetching}>
              {t("common:actions.refresh")}
            </Button>
            {canManage && category ? (
              <Link to={buildLicenseCategoryEditPath(category.id)} className={cn(buttonVariants())}>
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
        <EmptyState title={t("licenseManagement:pages.categories.detail.notFound")} />
      ) : null}
      {category ? (
        <SectionCard title={t("licenseManagement:pages.categories.detail.summaryTitle")}>
          <div className="grid gap-4 md:grid-cols-2">
            <LicenseDetailField label={t("licenseManagement:form.categoryName")} value={category.name} />
            <LicenseDetailField label={t("common:fields.status")}>
              <StatusBadge isActive={category.isActive} />
            </LicenseDetailField>
            <LicenseDetailField
              label={t("licenseManagement:form.description")}
              value={category.description}
              valueClassName="whitespace-pre-wrap md:col-span-2"
            />
            <LicenseDetailField label={t("licenseManagement:pages.detail.createdAt")}>
              <DateTimeText value={category.createdAt} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:pages.detail.createdBy")} value={category.createdBy} />
            <LicenseDetailField label={t("licenseManagement:pages.detail.updatedAt")}>
              <DateTimeText value={category.updatedAt} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:pages.detail.updatedBy")} value={category.updatedBy} />
          </div>
        </SectionCard>
      ) : null}
    </section>
  );
}
