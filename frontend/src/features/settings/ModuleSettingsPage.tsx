import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { PageHeader } from "@/components/common/PageHeader";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  AD_MANAGEMENT_SETTINGS_QUERY_KEY,
  getAdManagementSettings,
} from "@/features/ad-management/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

type AdManagementCardStatus = "loading" | "error" | "notConfigured" | "disabled" | "active";

function resolveAdManagementCardStatus(
  isLoading: boolean,
  isError: boolean,
  isConfigured: boolean,
  isEnabled: boolean,
): AdManagementCardStatus {
  if (isLoading) {
    return "loading";
  }

  if (isError) {
    return "error";
  }

  if (!isConfigured) {
    return "notConfigured";
  }

  if (!isEnabled) {
    return "disabled";
  }

  return "active";
}

export function ModuleSettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const user = useAuthStore((state) => state.user);
  const canViewAdManagementSettings = canAccess(user, "AdManagement.Settings.View");

  const adManagementSettingsQuery = useQuery({
    queryKey: AD_MANAGEMENT_SETTINGS_QUERY_KEY,
    queryFn: getAdManagementSettings,
    enabled: canViewAdManagementSettings,
    staleTime: 60_000,
  });

  const cardStatus = resolveAdManagementCardStatus(
    adManagementSettingsQuery.isLoading,
    adManagementSettingsQuery.isError,
    adManagementSettingsQuery.data?.isConfigured ?? false,
    adManagementSettingsQuery.data?.isEnabled ?? false,
  );

  const badgeVariant =
    cardStatus === "active"
      ? "default"
      : cardStatus === "disabled"
        ? "secondary"
        : "outline";

  const cards = (
    <>
      {canViewAdManagementSettings ? (
        <Card className="flex flex-col">
          <CardHeader>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <CardTitle className="text-lg">
                {t("settings:modulesHub.adManagement.title")}
              </CardTitle>
              {cardStatus === "loading" ? (
                <Skeleton className="h-5 w-24" />
              ) : (
                <Badge variant={badgeVariant}>
                  {t(`settings:modulesHub.adManagement.status.${cardStatus}.badge`)}
                </Badge>
              )}
            </div>
            <CardDescription>
              {cardStatus === "loading" ? (
                <Skeleton className="h-4 w-full max-w-md" />
              ) : (
                t(`settings:modulesHub.adManagement.status.${cardStatus}.description`)
              )}
            </CardDescription>
          </CardHeader>
          <CardContent className="flex-1" />
          <CardFooter>
            <Link
              to="/settings/modules/ad-management"
              className={cn(buttonVariants({ variant: "default" }), "w-full sm:w-auto")}
            >
              {t("settings:modulesHub.adManagement.openSettings")}
            </Link>
          </CardFooter>
        </Card>
      ) : null}
    </>
  );

  const hasAnyCard = canViewAdManagementSettings;

  return (
    <section className="space-y-6">
      <PageHeader
        title={t("settings:pages.modules.title")}
        description={t("settings:pages.modules.description")}
      />

      {!hasAnyCard ? (
        <EmptyState title={t("settings:modulesHub.emptyTitle")} />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{cards}</div>
      )}
    </section>
  );
}
