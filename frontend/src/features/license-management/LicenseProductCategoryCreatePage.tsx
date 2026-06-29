import { Link, useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { LicenseProductCategoryForm } from "@/features/license-management/components/LicenseProductCategoryForm";
import { LICENSE_CATEGORIES_LIST_PATH } from "@/features/license-management/license-categories-list-path";
import { cn } from "@/lib/utils";

export function LicenseProductCategoryCreatePage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.categories.create.title")}
        description={t("licenseManagement:pages.categories.create.description")}
        actions={
          <Link to={LICENSE_CATEGORIES_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("common:actions.back")}
          </Link>
        }
      />
      <SectionCard title={t("licenseManagement:pages.categories.create.formTitle")}>
        <LicenseProductCategoryForm
          mode="create"
          onCancel={() => navigate(LICENSE_CATEGORIES_LIST_PATH)}
          onSaved={() => {
            queryClient.invalidateQueries({ queryKey: ["license-management", "product-categories"] });
            toast.success(t("licenseManagement:messages.categoryCreated"));
            navigate(LICENSE_CATEGORIES_LIST_PATH);
          }}
        />
      </SectionCard>
    </section>
  );
}
