import { Link, useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { LicenseProductForm } from "@/features/license-management/components/LicenseProductForm";
import { LICENSE_PRODUCTS_LIST_PATH } from "@/features/license-management/license-products-list-path";
import { cn } from "@/lib/utils";

export function LicenseProductCreatePage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.products.create.title")}
        description={t("licenseManagement:pages.products.create.description")}
        actions={
          <Link to={LICENSE_PRODUCTS_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("common:actions.back")}
          </Link>
        }
      />
      <SectionCard title={t("licenseManagement:pages.products.create.formTitle")}>
        <LicenseProductForm
          mode="create"
          onCancel={() => navigate(LICENSE_PRODUCTS_LIST_PATH)}
          onSaved={() => {
            queryClient.invalidateQueries({ queryKey: ["license-management", "products"] });
            queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
            toast.success(t("licenseManagement:messages.productCreated"));
            navigate(LICENSE_PRODUCTS_LIST_PATH);
          }}
        />
      </SectionCard>
    </section>
  );
}
