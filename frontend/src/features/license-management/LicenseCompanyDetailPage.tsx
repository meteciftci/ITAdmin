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
import { getLicenseCompanyById } from "@/features/license-management/api";
import { LicenseDetailField } from "@/features/license-management/components/LicenseDetailField";
import { LICENSE_COMPANIES_LIST_PATH } from "@/features/license-management/license-companies-list-path";
import { buildLicenseCompanyEditPath } from "@/features/license-management/license-company-detail-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

export function LicenseCompanyDetailPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageCatalog);

  const detailQuery = useQuery({
    queryKey: ["license-management", "companies", "detail", id],
    queryFn: () => getLicenseCompanyById(id!),
    enabled: Boolean(id),
  });

  const isNotFound =
    detailQuery.isError
    && detailQuery.error instanceof AxiosError
    && detailQuery.error.response?.status === 404;

  if (!id) {
    return (
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <EmptyState title={t("licenseManagement:pages.companies.detail.notFound")} />
      </section>
    );
  }

  const company = detailQuery.data;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={company?.name ?? t("licenseManagement:pages.companies.detail.title")}
        description={t("licenseManagement:pages.companies.detail.description")}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Link to={LICENSE_COMPANIES_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
              {t("common:actions.back")}
            </Link>
            <Button type="button" variant="outline" onClick={() => detailQuery.refetch()} disabled={detailQuery.isFetching}>
              {t("common:actions.refresh")}
            </Button>
            {canManage && company ? (
              <Link to={buildLicenseCompanyEditPath(company.id)} className={cn(buttonVariants())}>
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
        <EmptyState title={t("licenseManagement:pages.companies.detail.notFound")} />
      ) : null}
      {company ? (
        <SectionCard title={t("licenseManagement:pages.companies.detail.summaryTitle")}>
          <div className="grid gap-4 md:grid-cols-2">
            <LicenseDetailField label={t("licenseManagement:table.companyName")} value={company.name} />
            <LicenseDetailField label={t("common:fields.status")}>
              <StatusBadge isActive={company.isActive} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:table.email")} value={company.email} />
            <LicenseDetailField label={t("licenseManagement:table.phone")} value={company.phone} />
            <LicenseDetailField label={t("licenseManagement:form.taxNumber")} value={company.taxNumber} />
            <LicenseDetailField label={t("licenseManagement:form.website")} value={company.website} />
            <LicenseDetailField label={t("licenseManagement:form.address")} value={company.address} valueClassName="whitespace-pre-wrap" />
            <LicenseDetailField label={t("licenseManagement:form.supportEmail")} value={company.supportEmail} />
            <LicenseDetailField label={t("licenseManagement:form.supportPhone")} value={company.supportPhone} />
            <LicenseDetailField label={t("licenseManagement:form.contactPersonName")} value={company.contactPersonName} />
            <LicenseDetailField label={t("licenseManagement:form.contactPersonPhone")} value={company.contactPersonPhone} />
            <LicenseDetailField label={t("licenseManagement:form.contactPersonEmail")} value={company.contactPersonEmail} />
            <LicenseDetailField label={t("licenseManagement:form.notes")} value={company.notes} valueClassName="whitespace-pre-wrap md:col-span-2" />
            <LicenseDetailField label={t("licenseManagement:pages.detail.createdAt")}>
              <DateTimeText value={company.createdAt} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:pages.detail.createdBy")} value={company.createdBy} />
            <LicenseDetailField label={t("licenseManagement:pages.detail.updatedAt")}>
              <DateTimeText value={company.updatedAt} />
            </LicenseDetailField>
            <LicenseDetailField label={t("licenseManagement:pages.detail.updatedBy")} value={company.updatedBy} />
          </div>
        </SectionCard>
      ) : null}
    </section>
  );
}
