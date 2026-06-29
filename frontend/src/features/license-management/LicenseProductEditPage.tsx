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
import { getLicensedProductById } from "@/features/license-management/api";
import { LicenseProductForm } from "@/features/license-management/components/LicenseProductForm";
import { buildLicenseProductDetailPath } from "@/features/license-management/license-product-detail-path";
import { cn } from "@/lib/utils";
import { getApiErrorMessage } from "@/lib/api-error";

export function LicenseProductEditPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

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

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.products.edit.title")}
        description={detailQuery.data?.name}
        actions={
          <Link to={buildLicenseProductDetailPath(id)} className={cn(buttonVariants({ variant: "outline" }))}>
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
        <EmptyState title={t("licenseManagement:pages.products.detail.notFound")} />
      ) : null}
      {detailQuery.data ? (
        <SectionCard title={t("licenseManagement:pages.products.edit.formTitle")}>
          <LicenseProductForm
            mode="edit"
            product={detailQuery.data}
            onCancel={() => navigate(buildLicenseProductDetailPath(id))}
            onSaved={() => {
              queryClient.invalidateQueries({ queryKey: ["license-management", "products"] });
              queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
              toast.success(t("licenseManagement:messages.productUpdated"));
              navigate(buildLicenseProductDetailPath(id));
            }}
          />
        </SectionCard>
      ) : null}
    </section>
  );
}
