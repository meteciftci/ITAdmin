import type { ReactNode } from "react";

import { Skeleton } from "@/components/ui/skeleton";
import { getCurrentUser } from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";

type RequireAuthProps = {
  children: ReactNode;
};

export function RequireAuth({ children }: RequireAuthProps) {
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

  if (!accessToken) {
    return <Navigate to="/login" replace />;
  }

  if (meQuery.isError) {
    clearAuth();
    return <Navigate to="/login" replace />;
  }

  if (meQuery.isSuccess && !user) {
    setUser(meQuery.data);
  }

  if (meQuery.isLoading) {
    return (
      <div className="space-y-4 p-6">
        <Skeleton className="h-8 w-1/3" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  return <>{children}</>;
}
