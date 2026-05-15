import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { useAuthStore } from "@/features/auth/auth-store";
import { getSetupStatus } from "@/features/setup/api";

export function RootRedirect() {
  const { t } = useTranslation(["common"]);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const setupQuery = useQuery({
    queryKey: ["setup", "status"],
    queryFn: getSetupStatus,
  });

  if (setupQuery.isLoading) {
    return <div className="p-6 text-sm text-muted-foreground">{t("loading")}</div>;
  }

  if (setupQuery.data?.isSetupRequired) {
    return <Navigate to="/setup" replace />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <Navigate to="/home" replace />;
}
