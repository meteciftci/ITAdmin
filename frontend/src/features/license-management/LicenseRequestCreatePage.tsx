import { Link, Navigate, useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { useAuthStore } from "@/features/auth/auth-store";
import { LicenseRequestAdAccessGuard } from "@/features/license-management/components/LicenseRequestAdAccessGuard";
import { LicenseRequestForm } from "@/features/license-management/components/LicenseRequestForm";
import {
  buildLicenseRequestDetailPath,
  LICENSE_REQUESTS_LIST_PATH,
} from "@/features/license-management/license-request-paths";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

export function LicenseRequestCreatePage() {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageRequests);

  if (!canManage) {
    return <Navigate to={LICENSE_REQUESTS_LIST_PATH} replace />;
  }

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.requests.create.title")}
        description={t("licenseManagement:pages.requests.create.description")}
        actions={
          <Link to={LICENSE_REQUESTS_LIST_PATH} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("common:actions.back")}
          </Link>
        }
      />
      <SectionCard title={t("licenseManagement:pages.requests.create.formTitle")}>
        <LicenseRequestAdAccessGuard>
          <LicenseRequestForm
            mode="create"
            onCancel={() => navigate(LICENSE_REQUESTS_LIST_PATH)}
            onSaved={(requestId) => {
              queryClient.invalidateQueries({ queryKey: ["license-management", "requests"] });
              queryClient.invalidateQueries({ queryKey: ["license-management", "overview"] });
              toast.success(t("licenseManagement:messages.requestCreated"));
              navigate(buildLicenseRequestDetailPath(requestId));
            }}
          />
        </LicenseRequestAdAccessGuard>
      </SectionCard>
    </section>
  );
}
