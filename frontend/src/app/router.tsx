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
import { RequireAnyPermission } from "@/features/auth/RequireAnyPermission";
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
const SettingsRedirectPage = lazy(() =>
  import("@/features/settings/SettingsRedirectPage").then((module) => ({
    default: module.SettingsRedirectPage,
  })),
);
const ApplicationSettingsPage = lazy(() =>
  import("@/features/settings/ApplicationSettingsPage").then((module) => ({
    default: module.ApplicationSettingsPage,
  })),
);
const ModuleSettingsPage = lazy(() =>
  import("@/features/settings/ModuleSettingsPage").then((module) => ({
    default: module.ModuleSettingsPage,
  })),
);
const NotificationProvidersPage = lazy(() =>
  import("@/features/notification-providers/NotificationProvidersPage").then((module) => ({
    default: module.NotificationProvidersPage,
  })),
);
const AdManagementSettingsPage = lazy(() =>
  import("@/features/ad-management/AdManagementSettingsPage").then((module) => ({
    default: module.AdManagementSettingsPage,
  })),
);
const AdUsersPage = lazy(() =>
  import("@/features/ad-management/AdUsersPage").then((module) => ({
    default: module.AdUsersPage,
  })),
);
const AdCreateUserPage = lazy(() =>
  import("@/features/ad-management/AdCreateUserPage").then((module) => ({
    default: module.AdCreateUserPage,
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
        <RequireAnyPermission
          permissions={[
            "Settings.View",
            "NotificationProviders.View",
            "AdManagement.Settings.View",
          ]}
        >
          <AppLayout>
            <LazyRoute>
              <SettingsRedirectPage />
            </LazyRoute>
          </AppLayout>
        </RequireAnyPermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings/application",
    element: (
      <RequireAuth>
        <RequirePermission permission="Settings.View">
          <AppLayout>
            <LazyRoute>
              <ApplicationSettingsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings/notification-providers",
    element: (
      <RequireAuth>
        <RequirePermission permission="NotificationProviders.View">
          <AppLayout>
            <LazyRoute>
              <NotificationProvidersPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings/modules",
    element: (
      <RequireAuth>
        <RequireAnyPermission permissions={["AdManagement.Settings.View"]}>
          <AppLayout>
            <LazyRoute>
              <ModuleSettingsPage />
            </LazyRoute>
          </AppLayout>
        </RequireAnyPermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings/modules/ad-management",
    element: (
      <RequireAuth>
        <RequirePermission permission="AdManagement.Settings.View">
          <AppLayout>
            <LazyRoute>
              <AdManagementSettingsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/users",
    element: (
      <RequireAuth>
        <RequirePermission permission="AdManagement.Users.View">
          <AppLayout>
            <LazyRoute>
              <AdUsersPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/users/create",
    element: (
      <RequireAuth>
        <RequirePermission permission="AdManagement.Users.Create">
          <AppLayout>
            <LazyRoute>
              <AdCreateUserPage />
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
