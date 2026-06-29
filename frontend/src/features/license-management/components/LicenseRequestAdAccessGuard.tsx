import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { BlockingStateCard } from "@/components/common/BlockingStateCard";
import { LoadingState } from "@/components/common/LoadingState";
import { buttonVariants } from "@/components/ui/button-variants";
import { getDirectoryUserLookupReadiness } from "@/features/license-management/api";
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

  const readinessQuery = useQuery({
    queryKey: ["license-management", "directory-user-lookup", "readiness"],
    queryFn: getDirectoryUserLookupReadiness,
    enabled: canAccess(user, PermissionCodes.Directory.Users.Lookup),
  });

  if (!canAccess(user, PermissionCodes.Directory.Users.Lookup)) {
    return (
      <BlockingStateCard
        title={t("licenseManagement:requests.blocking.missingLookupPermissionTitle")}
        description={t("licenseManagement:requests.blocking.missingLookupPermissionDescription")}
        variant="warning"
      />
    );
  }

  if (readinessQuery.isLoading) {
    return <LoadingState />;
  }

  if (readinessQuery.isError || !readinessQuery.data?.isReady) {
    const description =
      readinessQuery.data?.message
      ?? t("licenseManagement:requests.blocking.adConnectionDescription");
    const canViewAdSettings = canAccess(user, PermissionCodes.AdManagement.Settings.View);

    return (
      <BlockingStateCard
        title={t("licenseManagement:requests.blocking.adConnectionTitle")}
        description={description}
        variant="warning"
        actions={
          canViewAdSettings ? (
            <Link
              to={AD_MANAGEMENT_SETTINGS_PATH}
              className={cn(buttonVariants({ variant: "default" }))}
            >
              {t("licenseManagement:requests.blocking.goToAdSettings")}
            </Link>
          ) : undefined
        }
      />
    );
  }

  return <>{children}</>;
}
