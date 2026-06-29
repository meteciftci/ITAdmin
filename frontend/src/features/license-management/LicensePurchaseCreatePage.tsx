import { Link, useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { LicensePurchaseForm } from "@/features/license-management/components/LicensePurchaseForm";
import { LICENSE_PURCHASES_LIST_PATH } from "@/features/license-management/license-purchases-list-path";
import { cn } from "@/lib/utils";

export function LicensePurchaseCreatePage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.purchases.create.title")}
        description={t("licenseManagement:pages.purchases.create.description")}
        actions={
          <Link to={LICENSE_PURCHASES_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("common:actions.back")}
          </Link>
        }
      />
      <SectionCard title={t("licenseManagement:pages.purchases.create.formTitle")}>
        <LicensePurchaseForm
          mode="create"
          onCancel={() => navigate(LICENSE_PURCHASES_LIST_PATH)}
          onSaved={() => {
            queryClient.invalidateQueries({ queryKey: ["license-management", "purchases"] });
            queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
            toast.success(t("licenseManagement:messages.purchaseCreated"));
            navigate(LICENSE_PURCHASES_LIST_PATH);
          }}
        />
      </SectionCard>
    </section>
  );
}
