import type { ReactNode } from "react";

import { Navigate, useNavigate } from "react-router-dom";

import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/features/auth/auth-store";
import { canAccess } from "@/lib/permissions";
import { useTranslation } from "react-i18next";

type RequirePermissionProps = {
  permission: string;
  children: ReactNode;
};

export function RequirePermission({
  permission,
  children,
}: RequirePermissionProps) {
  const { t } = useTranslation(["errors", "common"]);
  const navigate = useNavigate();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);

  if (!accessToken) {
    return <Navigate to="/login" replace />;
  }

  if (!user) {
    return (
      <div className="p-6 text-sm text-muted-foreground">{t("common:loading")}</div>
    );
  }

  if (!canAccess(user, permission)) {
    return (
      <main className="mx-auto flex w-full max-w-2xl items-center justify-center p-6">
        <SectionCard
          title={t("errors:forbidden.title")}
          description={t("errors:forbidden.description")}
        >
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => navigate(-1)}>
              {t("common:actions.back")}
            </Button>
            <Button onClick={() => navigate("/dashboard")}>
              {t("common:actions.goToDashboard")}
            </Button>
          </div>
        </SectionCard>
      </main>
    );
  }

  return <>{children}</>;
}
