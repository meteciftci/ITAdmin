import { type RouteObject } from "react-router-dom";

import { RootRedirect } from "@/app/RootRedirect";
import {
  AuditLogsPage,
  ErrorPage,
  HomePage,
  NotFoundPage,
  NotificationOutboxPage,
  PermissionsPage,
  RolesPage,
  SecurityLogsPage,
  UsersPage,
} from "@/app/lazy-pages";
import { LazyRoute } from "@/app/route-helpers";
import { AppLayout } from "@/components/layout/AppLayout";
import { LoginPage } from "@/features/auth/LoginPage";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { SetupRequiredPage } from "@/features/setup/SetupRequiredPage";
import { PermissionCodes } from "@/lib/permission-codes";

export const coreRoutes: RouteObject[] = [
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
        <RequirePermission permission={PermissionCodes.AuditLogs.View}>
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
        <RequirePermission permission={PermissionCodes.SecurityLogs.View}>
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
    path: "/notification-outbox",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.NotificationOutbox.View}>
          <AppLayout>
            <LazyRoute>
              <NotificationOutboxPage />
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
        <RequirePermission permission={PermissionCodes.Permissions.View}>
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
        <RequirePermission permission={PermissionCodes.Roles.View}>
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
        <RequirePermission permission={PermissionCodes.Users.View}>
          <AppLayout>
            <LazyRoute>
              <UsersPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
];

export const notFoundRoute: RouteObject = {
  path: "*",
  element: (
    <LazyRoute>
      <NotFoundPage />
    </LazyRoute>
  ),
};
