import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { LicensePackageForm } from "@/features/license-management/components/LicensePackageForm";
import { buildLicensePurchaseDetailPath } from "@/features/license-management/license-purchase-detail-path";
import {
  LICENSE_PACKAGES_LIST_PATH,
} from "@/features/license-management/license-packages-list-path";
import { cn } from "@/lib/utils";

function isGuidLike(value: string | null): boolean {
  if (!value) {
    return false;
  }

  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

export function LicensePackageCreatePage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const purchaseIdParam = searchParams.get("purchaseId");
  const initialPurchaseId = isGuidLike(purchaseIdParam) ? purchaseIdParam : null;
  const backPath = initialPurchaseId
    ? buildLicensePurchaseDetailPath(initialPurchaseId)
    : LICENSE_PACKAGES_LIST_PATH;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.packages.create.title")}
        description={t("licenseManagement:pages.packages.create.description")}
        actions={
          <Link to={backPath} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("common:actions.back")}
          </Link>
        }
      />
      <SectionCard title={t("licenseManagement:pages.packages.create.formTitle")}>
        <LicensePackageForm
          mode="create"
          initialPurchaseId={initialPurchaseId}
          onCancel={() => navigate(backPath)}
          onSaved={() => {
            queryClient.invalidateQueries({ queryKey: ["license-management", "packages"] });
            queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
            if (initialPurchaseId) {
              queryClient.invalidateQueries({
                queryKey: ["license-management", "packages", "purchase-detail", initialPurchaseId],
              });
            }
            toast.success(t("licenseManagement:messages.packageCreated"));
            navigate(
              initialPurchaseId
                ? buildLicensePurchaseDetailPath(initialPurchaseId)
                : LICENSE_PACKAGES_LIST_PATH,
            );
          }}
        />
      </SectionCard>
    </section>
  );
}
