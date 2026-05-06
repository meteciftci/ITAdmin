import type { ReactNode } from "react";

import { Navigate, useNavigate } from "react-router-dom";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
      <main className="flex items-center justify-center p-6">
        <Card className="w-full max-w-lg">
          <CardHeader>
            <CardTitle>{t("errors:forbidden.title")}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">
              {t("errors:forbidden.description")}
            </p>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" onClick={() => navigate(-1)}>
                {t("errors:goBack")}
              </Button>
              <Button onClick={() => navigate("/dashboard")}>
                {t("errors:goDashboard")}
              </Button>
            </div>
          </CardContent>
        </Card>
      </main>
    );
  }

  return <>{children}</>;
}
