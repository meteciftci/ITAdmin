import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { BlockingStateCard } from "@/components/common/BlockingStateCard";
import { LoadingState } from "@/components/common/LoadingState";
import { buttonVariants } from "@/components/ui/button-variants";
import { useAdManagementModuleStatus } from "@/features/ad-management/hooks/useAdManagementModuleStatus";
import { isAdManagementConnectionReady } from "@/features/ad-management/is-ad-management-connection-ready";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

const AD_MANAGEMENT_SETTINGS_PATH = "/settings/modules/ad-management";

type Props = {
  children: ReactNode;
};

export function LicenseRequestAdAccessGuard({ children }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const user = useAuthStore((state) => state.user);
  const moduleStatus = useAdManagementModuleStatus();

  if (moduleStatus.isLoading) {
    return <LoadingState />;
  }

  if (!canAccess(user, PermissionCodes.Directory.Users.Lookup)) {
    return (
      <BlockingStateCard
        title={t("licenseManagement:requests.blocking.missingLookupPermissionTitle")}
        description={t("licenseManagement:requests.blocking.missingLookupPermissionDescription")}
        variant="warning"
      />
    );
  }

  if (!isAdManagementConnectionReady(moduleStatus.settings)) {
    return (
      <BlockingStateCard
        title={t("licenseManagement:requests.blocking.adConnectionTitle")}
        description={t("licenseManagement:requests.blocking.adConnectionDescription")}
        variant="warning"
        actions={
          <Link
            to={AD_MANAGEMENT_SETTINGS_PATH}
            className={cn(buttonVariants({ variant: "default" }))}
          >
            {t("licenseManagement:requests.blocking.goToAdSettings")}
          </Link>
        }
      />
    );
  }

  return <>{children}</>;
}
