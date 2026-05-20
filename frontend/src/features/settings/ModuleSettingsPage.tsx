import { Link } from "react-router-dom";
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
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { cn } from "@/lib/utils";

export function ModuleSettingsPage() {
  const { t } = useTranslation(["settings", "common"]);
  const user = useAuthStore((state) => state.user);
  const canViewAdManagementSettings = canAccess(user, "AdManagement.Settings.View");

  const cards = (
    <>
      {canViewAdManagementSettings ? (
        <Card className="flex flex-col">
          <CardHeader>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <CardTitle className="text-lg">
                {t("settings:modulesHub.adManagement.title")}
              </CardTitle>
              <Badge variant="secondary">{t("settings:modulesHub.adManagement.badge")}</Badge>
            </div>
            <CardDescription>
              {t("settings:modulesHub.adManagement.description")}
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
