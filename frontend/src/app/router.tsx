import { useQuery } from "@tanstack/react-query";
import { Navigate, createBrowserRouter } from "react-router-dom";

import { AppLayout } from "@/components/layout/AppLayout";
import { LoginPage } from "@/features/auth/LoginPage";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { useAuthStore } from "@/features/auth/auth-store";
import { DashboardPage } from "@/features/dashboard/DashboardPage";
import { getSetupStatus } from "@/features/setup/api";
import { SetupRequiredPage } from "@/features/setup/SetupRequiredPage";
import { NotFoundPage } from "@/pages/NotFoundPage";

function RootRedirect() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const setupQuery = useQuery({
    queryKey: ["setup", "status"],
    queryFn: getSetupStatus,
  });

  if (setupQuery.isLoading) {
    return <div className="p-6 text-sm text-muted-foreground">Loading...</div>;
  }

  if (setupQuery.data?.isSetupRequired) {
    return <Navigate to="/setup" replace />;
  }

  if (!accessToken) {
    return <Navigate to="/login" replace />;
  }

  return <Navigate to="/dashboard" replace />;
}

export const router = createBrowserRouter([
  {
    path: "/",
    element: <RootRedirect />,
  },
  {
    path: "/login",
    element: <LoginPage />,
  },
  {
    path: "/setup",
    element: <SetupRequiredPage />,
  },
  {
    path: "/dashboard",
    element: (
      <RequireAuth>
        <AppLayout>
          <DashboardPage />
        </AppLayout>
      </RequireAuth>
    ),
  },
  {
    path: "*",
    element: <NotFoundPage />,
  },
]);
