import { createBrowserRouter } from "react-router-dom";

import { RootRedirect } from "@/app/RootRedirect";
import { AppLayout } from "@/components/layout/AppLayout";
import { LoginPage } from "@/features/auth/LoginPage";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { AuditLogsPage } from "@/features/audit-logs/AuditLogsPage";
import { DashboardPage } from "@/features/dashboard/DashboardPage";
import { PermissionsPage } from "@/features/permissions/PermissionsPage";
import { RolesPage } from "@/features/roles/RolesPage";
import { SecurityLogsPage } from "@/features/security-logs/SecurityLogsPage";
import { SetupRequiredPage } from "@/features/setup/SetupRequiredPage";
import { SettingsPage } from "@/features/settings/SettingsPage";
import { UsersPage } from "@/features/users/UsersPage";
import { ErrorPage } from "@/pages/ErrorPage";
import { NotFoundPage } from "@/pages/NotFoundPage";

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
    path: "/error/:code",
    element: (
      <RequireAuth>
        <AppLayout>
          <ErrorPage />
        </AppLayout>
      </RequireAuth>
    ),
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
    path: "/audit-logs",
    element: (
      <RequireAuth>
        <RequirePermission permission="AuditLogs.View">
          <AppLayout>
            <AuditLogsPage />
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/security-logs",
    element: (
      <RequireAuth>
        <RequirePermission permission="SecurityLogs.View">
          <AppLayout>
            <SecurityLogsPage />
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/permissions",
    element: (
      <RequireAuth>
        <RequirePermission permission="Permissions.View">
          <AppLayout>
            <PermissionsPage />
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/roles",
    element: (
      <RequireAuth>
        <RequirePermission permission="Roles.View">
          <AppLayout>
            <RolesPage />
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/users",
    element: (
      <RequireAuth>
        <RequirePermission permission="Users.View">
          <AppLayout>
            <UsersPage />
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings",
    element: (
      <RequireAuth>
        <RequirePermission permission="Settings.View">
          <AppLayout>
            <SettingsPage />
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "*",
    element: <NotFoundPage />,
  },
]);
