import { Navigate, type RouteObject } from "react-router-dom";

import {
  AdManagementSettingsPage,
  ApplicationSettingsPage,
  LicenseManagementSettingsPage,
  ModuleSettingsPage,
  NotificationSettingsProvidersPage,
  NotificationSettingsRedirectPage,
  NotificationSettingsTemplatesPage,
  NotificationTemplateFormPage,
  SettingsRedirectPage,
} from "@/app/lazy-pages";
import { LazyRoute } from "@/app/route-helpers";
import { AppLayout } from "@/components/layout/AppLayout";
import { RequireAnyPermission } from "@/features/auth/RequireAnyPermission";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { PermissionCodes } from "@/lib/permission-codes";

export const settingsRoutes: RouteObject[] = [
  {
    path: "/settings",
    element: (
      <RequireAuth>
        <RequireAnyPermission
          permissions={[
            PermissionCodes.Settings.View,
            PermissionCodes.NotificationProviders.View,
            PermissionCodes.NotificationTemplates.View,
            PermissionCodes.AdManagement.Settings.View,
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
        <RequirePermission permission={PermissionCodes.Settings.View}>
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
        <Navigate to="/settings/notifications/providers" replace />
      </RequireAuth>
    ),
  },
  {
    path: "/settings/notification-templates",
    element: (
      <RequireAuth>
        <Navigate to="/settings/notifications/templates" replace />
      </RequireAuth>
    ),
  },
  {
    path: "/settings/notifications",
    element: (
      <RequireAuth>
        <RequireAnyPermission
          permissions={[PermissionCodes.NotificationProviders.View, PermissionCodes.NotificationTemplates.View]}
        >
          <AppLayout>
            <LazyRoute>
              <NotificationSettingsRedirectPage />
            </LazyRoute>
          </AppLayout>
        </RequireAnyPermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings/notifications/providers",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.NotificationProviders.View}>
          <AppLayout>
            <LazyRoute>
              <NotificationSettingsProvidersPage activeTab="providers" />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings/notifications/templates",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.NotificationTemplates.View}>
          <AppLayout>
            <LazyRoute>
              <NotificationSettingsTemplatesPage activeTab="templates" />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings/notifications/templates/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.NotificationTemplates.Update}>
          <AppLayout>
            <LazyRoute>
              <NotificationTemplateFormPage mode="create" />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/settings/notifications/templates/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.NotificationTemplates.Update}>
          <AppLayout>
            <LazyRoute>
              <NotificationTemplateFormPage mode="edit" />
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
        <RequireAnyPermission permissions={[
          PermissionCodes.AdManagement.Settings.View,
          PermissionCodes.LicenseManagement.ManageSettings,
        ]}>
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
        <RequirePermission permission={PermissionCodes.AdManagement.Settings.View}>
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
    path: "/settings/modules/license-management",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageSettings}>
          <AppLayout>
            <LazyRoute>
              <LicenseManagementSettingsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
];
