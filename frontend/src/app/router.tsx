/* eslint-disable react-refresh/only-export-components --
 * Router config: lazy() component declarations and a Suspense wrapper coexist with
 * the non-component `router` export. Fast refresh does not apply to this module.
 */
import { lazy, Suspense, type ReactNode } from "react";
import { createBrowserRouter } from "react-router-dom";

import { RootRedirect } from "@/app/RootRedirect";
import { AppLayout } from "@/components/layout/AppLayout";
import { LoginPage } from "@/features/auth/LoginPage";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { SetupRequiredPage } from "@/features/setup/SetupRequiredPage";

const HomePage = lazy(() =>
  import("@/features/home/HomePage").then((module) => ({ default: module.HomePage })),
);
const AuditLogsPage = lazy(() =>
  import("@/features/audit-logs/AuditLogsPage").then((module) => ({
    default: module.AuditLogsPage,
  })),
);
const SecurityLogsPage = lazy(() =>
  import("@/features/security-logs/SecurityLogsPage").then((module) => ({
    default: module.SecurityLogsPage,
  })),
);
const PermissionsPage = lazy(() =>
  import("@/features/permissions/PermissionsPage").then((module) => ({
    default: module.PermissionsPage,
  })),
);
const RolesPage = lazy(() =>
  import("@/features/roles/RolesPage").then((module) => ({ default: module.RolesPage })),
);
const UsersPage = lazy(() =>
  import("@/features/users/UsersPage").then((module) => ({ default: module.UsersPage })),
);
const SettingsPage = lazy(() =>
  import("@/features/settings/SettingsPage").then((module) => ({
    default: module.SettingsPage,
  })),
);
const ErrorPage = lazy(() =>
  import("@/pages/ErrorPage").then((module) => ({ default: module.ErrorPage })),
);
const NotFoundPage = lazy(() =>
  import("@/pages/NotFoundPage").then((module) => ({ default: module.NotFoundPage })),
);

function RouteFallback() {
  return (
    <div className="flex min-h-screen items-center justify-center text-sm text-muted-foreground">
      Loading...
    </div>
  );
}

function LazyRoute({ children }: { children: ReactNode }) {
  return <Suspense fallback={<RouteFallback />}>{children}</Suspense>;
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
    path: "/error/:code",
    element: (
      <RequireAuth>
        <AppLayout>
          <LazyRoute>
            <ErrorPage />
          </LazyRoute>
        </AppLayout>
      </RequireAuth>
    ),
  },
  {
    path: "/home",
    element: (
      <RequireAuth>
        <AppLayout>
          <LazyRoute>
            <HomePage />
          </LazyRoute>
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
            <LazyRoute>
              <AuditLogsPage />
            </LazyRoute>
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
            <LazyRoute>
              <SecurityLogsPage />
            </LazyRoute>
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
            <LazyRoute>
              <PermissionsPage />
            </LazyRoute>
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
            <LazyRoute>
              <RolesPage />
            </LazyRoute>
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
            <LazyRoute>
              <UsersPage />
            </LazyRoute>
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
            <LazyRoute>
              <SettingsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "*",
    element: (
      <LazyRoute>
        <NotFoundPage />
      </LazyRoute>
    ),
  },
]);
