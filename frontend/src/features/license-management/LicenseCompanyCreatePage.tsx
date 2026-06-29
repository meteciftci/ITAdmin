import { Link, useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { LicenseCompanyForm } from "@/features/license-management/components/LicenseCompanyForm";
import { LICENSE_COMPANIES_LIST_PATH } from "@/features/license-management/license-companies-list-path";
import { cn } from "@/lib/utils";

export function LicenseCompanyCreatePage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.companies.create.title")}
        description={t("licenseManagement:pages.companies.create.description")}
        actions={
          <Link to={LICENSE_COMPANIES_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("common:actions.back")}
          </Link>
        }
      />
      <SectionCard title={t("licenseManagement:pages.companies.create.formTitle")}>
        <LicenseCompanyForm
          mode="create"
          onCancel={() => navigate(LICENSE_COMPANIES_LIST_PATH)}
          onSaved={() => {
            queryClient.invalidateQueries({ queryKey: ["license-management", "companies"] });
            queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
            toast.success(t("licenseManagement:messages.companyCreated"));
            navigate(LICENSE_COMPANIES_LIST_PATH);
          }}
        />
      </SectionCard>
    </section>
  );
}
