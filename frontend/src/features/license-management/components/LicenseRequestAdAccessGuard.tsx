import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { BlockingStateCard } from "@/components/common/BlockingStateCard";
import { LoadingState } from "@/components/common/LoadingState";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  getDirectoryOrganizationalUnitLookupReadiness,
  getDirectoryUserLookupReadiness,
} from "@/features/license-management/api";
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

  const hasUserLookup = canAccess(user, PermissionCodes.Directory.Users.Lookup);
  const hasOuLookup = canAccess(user, PermissionCodes.Directory.OrganizationalUnits.Lookup);
  const hasLookupPermissions = hasUserLookup && hasOuLookup;

  const userReadinessQuery = useQuery({
    queryKey: ["license-management", "directory-user-lookup", "readiness"],
    queryFn: getDirectoryUserLookupReadiness,
    enabled: hasUserLookup,
  });

  const ouReadinessQuery = useQuery({
    queryKey: ["license-management", "directory-organizational-units", "readiness"],
    queryFn: getDirectoryOrganizationalUnitLookupReadiness,
    enabled: hasOuLookup,
  });

  if (!hasLookupPermissions) {
    return (
      <BlockingStateCard
        title={t("licenseManagement:requests.blocking.missingLookupPermissionTitle")}
        description={t("licenseManagement:requests.blocking.missingDirectoryLookupPermissionDescription")}
        variant="warning"
      />
    );
  }

  if (userReadinessQuery.isLoading || ouReadinessQuery.isLoading) {
    return <LoadingState />;
  }

  const userReady = userReadinessQuery.data?.isReady === true;
  const ouReady = ouReadinessQuery.data?.isReady === true;

  if (!userReady || !ouReady) {
    const description =
      userReadinessQuery.data?.message
      ?? ouReadinessQuery.data?.message
      ?? t("licenseManagement:requests.blocking.directoryConnectionDescription");
    const canViewAdSettings = canAccess(user, PermissionCodes.AdManagement.Settings.View);

    return (
      <BlockingStateCard
        title={t("licenseManagement:requests.blocking.directoryConnectionTitle")}
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
