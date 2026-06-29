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
import { getLicenseProductCategoryById } from "@/features/license-management/api";
import { LicenseProductCategoryForm } from "@/features/license-management/components/LicenseProductCategoryForm";
import { buildLicenseCategoryDetailPath } from "@/features/license-management/license-category-detail-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

export function LicenseProductCategoryEditPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

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

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.categories.edit.title")}
        description={detailQuery.data?.name}
        actions={
          <Link
            to={buildLicenseCategoryDetailPath(id)}
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
        <EmptyState title={t("licenseManagement:pages.categories.detail.notFound")} />
      ) : null}
      {detailQuery.data ? (
        <SectionCard title={t("licenseManagement:pages.categories.edit.formTitle")}>
          <LicenseProductCategoryForm
            mode="edit"
            category={detailQuery.data}
            onCancel={() => navigate(buildLicenseCategoryDetailPath(id))}
            onSaved={() => {
              queryClient.invalidateQueries({ queryKey: ["license-management", "product-categories"] });
              toast.success(t("licenseManagement:messages.categoryUpdated"));
              navigate(buildLicenseCategoryDetailPath(id));
            }}
          />
        </SectionCard>
      ) : null}
    </section>
  );
}
