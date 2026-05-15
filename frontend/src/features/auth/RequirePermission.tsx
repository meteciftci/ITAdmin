import type { ReactNode } from "react";

import { Navigate, useLocation } from "react-router-dom";

import { useAuthStore } from "@/features/auth/auth-store";
import { getErrorRoutePath } from "@/lib/route-error";
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
  const { t } = useTranslation(["common"]);
  const location = useLocation();
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const user = useAuthStore((state) => state.user);

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (!user) {
    return (
      <div className="p-6 text-sm text-muted-foreground">{t("common:loading")}</div>
    );
  }

  if (!canAccess(user, permission)) {
    return (
      <Navigate
        to={getErrorRoutePath("FORBIDDEN")}
        replace
        state={{
          code: "FORBIDDEN",
          kind: "forbidden" as const,
          status: 403,
          titleKey: "errors:api.forbidden.title",
          descriptionKey: "errors:api.forbidden.description",
          fromPath: location.pathname,
          retryPath: location.pathname,
        }}
      />
    );
  }

  return <>{children}</>;
}
