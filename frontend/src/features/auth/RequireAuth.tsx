import type { ReactNode } from "react";

import axios from "axios";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { i18n, normalizeLanguage } from "@/app/i18n";
import { AppLayout } from "@/components/layout/AppLayout";
import { ServiceUnavailableState } from "@/components/common/ServiceUnavailableState";
import { Skeleton } from "@/components/ui/skeleton";
import { useAuthStore } from "@/features/auth/auth-store";
import { useBootstrapSession } from "@/features/auth/hooks/useBootstrapSession";
import { getReadinessStatus, getSyntheticReadinessForAxiosError } from "@/features/health/api";

type RequireAuthProps = {
  children: ReactNode;
};

type AuthBootstrapFailureProps = {
  error: unknown;
  isRetrying: boolean;
  onRetry: () => void;
};

function AuthBootstrapFailure({ error, isRetrying, onRetry }: AuthBootstrapFailureProps) {
  return (
    <ServiceUnavailableState
      readiness={getSyntheticReadinessForAxiosError(error)}
      isLoading={isRetrying}
      onRetry={onRetry}
    />
  );
}

function AuthLoadingState() {
  const { t } = useTranslation(["common"]);

  return (
    <div className="space-y-4 p-6">
      <p className="text-sm text-muted-foreground">{t("loading")}</p>
      <Skeleton className="h-8 w-1/3" />
      <Skeleton className="h-40 w-full" />
    </div>
  );
}

export function RequireAuth({ children }: RequireAuthProps) {
  const queryClient = useQueryClient();
  const bootstrap = useBootstrapSession();
  const user = useAuthStore((state) => state.user);
  const clearAuth = useAuthStore((state) => state.clearAuth);

  useEffect(() => {
    const value = user?.preferredLanguage;
    if (value) {
      void i18n.changeLanguage(normalizeLanguage(value));
    }
  }, [user?.preferredLanguage]);

  if (bootstrap.status === "pending") {
    return <AuthLoadingState />;
  }

  if (bootstrap.status === "error") {
    if (axios.isAxiosError(bootstrap.error) && bootstrap.error.response?.status === 401) {
      clearAuth();
      return <Navigate to="/login" replace />;
    }

    return (
      <AppLayout>
        <AuthBootstrapFailure
          error={bootstrap.error}
          isRetrying={false}
          onRetry={() => {
            void queryClient.invalidateQueries({ queryKey: ["auth", "bootstrap"] });
            void queryClient.fetchQuery({
              queryKey: ["health", "readiness"],
              queryFn: getReadinessStatus,
            });
          }}
        />
      </AppLayout>
    );
  }

  if (bootstrap.status === "unauthenticated") {
    return <Navigate to="/login" replace />;
  }

  if (!user) {
    return <AuthLoadingState />;
  }

  return <>{children}</>;
}
