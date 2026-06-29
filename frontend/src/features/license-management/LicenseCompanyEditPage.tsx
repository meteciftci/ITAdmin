import { Link, useNavigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { getLicenseCompanyById } from "@/features/license-management/api";
import { LicenseCompanyForm } from "@/features/license-management/components/LicenseCompanyForm";
import { buildLicenseCompanyDetailPath } from "@/features/license-management/license-company-detail-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

export function LicenseCompanyEditPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

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

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.companies.edit.title")}
        description={detailQuery.data?.name}
        actions={
          <Link
            to={buildLicenseCompanyDetailPath(id)}
            className={cn(buttonVariants({ variant: "outline" }))}
          >
            {t("common:actions.back")}
          </Link>
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
      {detailQuery.data ? (
        <SectionCard title={t("licenseManagement:pages.companies.edit.formTitle")}>
          <LicenseCompanyForm
            mode="edit"
            company={detailQuery.data}
            onCancel={() => navigate(buildLicenseCompanyDetailPath(id))}
            onSaved={() => {
              queryClient.invalidateQueries({ queryKey: ["license-management", "companies"] });
              queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
              toast.success(t("licenseManagement:messages.companyUpdated"));
              navigate(buildLicenseCompanyDetailPath(id));
            }}
          />
        </SectionCard>
      ) : null}
    </section>
  );
}
