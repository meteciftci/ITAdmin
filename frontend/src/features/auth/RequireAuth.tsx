import type { ReactNode } from "react";

import axios from "axios";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { i18n, normalizeLanguage } from "@/app/i18n";
import { AppLayout } from "@/components/layout/AppLayout";
import { ServiceUnavailableState } from "@/components/common/ServiceUnavailableState";
import { Skeleton } from "@/components/ui/skeleton";
import { getCurrentUser } from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { getReadinessStatus, getSyntheticReadinessForAxiosError } from "@/features/health/api";

type RequireAuthProps = {
  children: ReactNode;
};

const isAuthSessionFailure = (error: unknown): boolean =>
  axios.isAxiosError(error) && error.response?.status === 401;

type AuthMeBootstrapFailureProps = {
  meError: unknown;
  isRetrying: boolean;
  onRetry: () => void;
};

function AuthMeBootstrapFailure({
  meError,
  isRetrying,
  onRetry,
}: AuthMeBootstrapFailureProps) {
  return (
    <ServiceUnavailableState
      readiness={getSyntheticReadinessForAxiosError(meError)}
      isLoading={isRetrying}
      onRetry={onRetry}
    />
  );
}

export function RequireAuth({ children }: RequireAuthProps) {
  const { t } = useTranslation(["common"]);
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const setUser = useAuthStore((state) => state.setUser);
  const clearAuth = useAuthStore((state) => state.clearAuth);

  const meQuery = useQuery({
    queryKey: ["auth", "me"],
    queryFn: getCurrentUser,
    enabled: Boolean(accessToken) && !user,
    staleTime: 5 * 60 * 1000,
  });

  useEffect(() => {
    if (meQuery.isSuccess && !user) {
      setUser(meQuery.data);
    }
  }, [meQuery.isSuccess, meQuery.data, user, setUser]);

  useEffect(() => {
    const value = user?.preferredLanguage ?? meQuery.data?.preferredLanguage;
    if (value) {
      void i18n.changeLanguage(normalizeLanguage(value));
    }
  }, [user?.preferredLanguage, meQuery.data?.preferredLanguage]);

  if (!accessToken) {
    return <Navigate to="/login" replace />;
  }

  if (meQuery.isError) {
    if (isAuthSessionFailure(meQuery.error)) {
      clearAuth();
      return <Navigate to="/login" replace />;
    }

    return (
      <AppLayout>
        <AuthMeBootstrapFailure
          meError={meQuery.error}
          isRetrying={meQuery.isFetching}
          onRetry={() => {
            void meQuery.refetch();
            void queryClient.fetchQuery({
              queryKey: ["health", "readiness"],
              queryFn: getReadinessStatus,
            });
          }}
        />
      </AppLayout>
    );
  }

  if (meQuery.isLoading) {
    return (
      <div className="space-y-4 p-6">
        <p className="text-sm text-muted-foreground">{t("loading")}</p>
        <Skeleton className="h-8 w-1/3" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  return <>{children}</>;
}
