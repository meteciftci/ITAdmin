/* eslint-disable react-refresh/only-export-components --
 * Router config: lazy() component declarations and a Suspense wrapper coexist with
 * the non-component `router` export. Fast refresh does not apply to this module.
 */
import { lazy, Suspense, type ReactNode } from "react";
import { createBrowserRouter, Navigate } from "react-router-dom";

import { RootRedirect } from "@/app/RootRedirect";
import { AppLayout } from "@/components/layout/AppLayout";
import { LoginPage } from "@/features/auth/LoginPage";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequireAnyPermission } from "@/features/auth/RequireAnyPermission";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { SetupRequiredPage } from "@/features/setup/SetupRequiredPage";
import { PermissionCodes } from "@/lib/permission-codes";

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
const NotificationOutboxPage = lazy(() =>
  import("@/features/notification-outbox/NotificationOutboxPage").then((module) => ({
    default: module.NotificationOutboxPage,
  })),
);
const NotificationSettingsRedirectPage = lazy(() =>
  import("@/features/notification-settings/NotificationSettingsRedirectPage").then((module) => ({
    default: module.NotificationSettingsRedirectPage,
  })),
);
const NotificationSettingsProvidersPage = lazy(() =>
  import("@/features/notification-settings/NotificationSettingsPage").then((module) => ({
    default: module.NotificationSettingsPage,
  })),
);
const NotificationSettingsTemplatesPage = lazy(() =>
  import("@/features/notification-settings/NotificationSettingsPage").then((module) => ({
    default: module.NotificationSettingsPage,
  })),
);
const NotificationTemplateFormPage = lazy(() =>
  import("@/features/notification-settings/NotificationTemplateFormPage").then((module) => ({
    default: module.NotificationTemplateFormPage,
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
const AdUserGroupsPage = lazy(() =>
  import("@/features/ad-management/AdUserGroupsPage").then((module) => ({
    default: module.AdUserGroupsPage,
  })),
);
const AdUserDetailPage = lazy(() =>
  import("@/features/ad-management/AdUserDetailPage").then((module) => ({
    default: module.AdUserDetailPage,
  })),
);
const AdEditUserPage = lazy(() =>
  import("@/features/ad-management/AdEditUserPage").then((module) => ({
    default: module.AdEditUserPage,
  })),
);
const AdMoveUserOuPage = lazy(() =>
  import("@/features/ad-management/AdMoveUserOuPage").then((module) => ({
    default: module.AdMoveUserOuPage,
  })),
);
const AdOperationLogsPage = lazy(() =>
  import("@/features/ad-management/AdOperationLogsPage").then((module) => ({
    default: module.AdOperationLogsPage,
  })),
);
const AdGroupsPage = lazy(() =>
  import("@/features/ad-management/AdGroupsPage").then((module) => ({
    default: module.AdGroupsPage,
  })),
);
const AdGroupDetailPage = lazy(() =>
  import("@/features/ad-management/AdGroupDetailPage").then((module) => ({
    default: module.AdGroupDetailPage,
  })),
);
const AdGroupCreatePage = lazy(() =>
  import("@/features/ad-management/AdGroupCreatePage").then((module) => ({
    default: module.AdGroupCreatePage,
  })),
);
const AdEditGroupPage = lazy(() =>
  import("@/features/ad-management/AdEditGroupPage").then((module) => ({
    default: module.AdEditGroupPage,
  })),
);
const AdMoveGroupOuPage = lazy(() =>
  import("@/features/ad-management/AdMoveGroupOuPage").then((module) => ({
    default: module.AdMoveGroupOuPage,
  })),
);
const AdComputersPage = lazy(() =>
  import("@/features/ad-management/AdComputersPage").then((module) => ({
    default: module.AdComputersPage,
  })),
);
const AdComputerDetailPage = lazy(() =>
  import("@/features/ad-management/AdComputerDetailPage").then((module) => ({
    default: module.AdComputerDetailPage,
  })),
);
const AdMoveComputerOuPage = lazy(() =>
  import("@/features/ad-management/AdMoveComputerOuPage").then((module) => ({
    default: module.AdMoveComputerOuPage,
  })),
);
const AdComputerGroupsPage = lazy(() =>
  import("@/features/ad-management/AdComputerGroupsPage").then((module) => ({
    default: module.AdComputerGroupsPage,
  })),
);
const AdOrganizationalUnitsPage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitsPage").then((module) => ({
    default: module.AdOrganizationalUnitsPage,
  })),
);
const AdOrganizationalUnitDetailPage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitDetailPage").then((module) => ({
    default: module.AdOrganizationalUnitDetailPage,
  })),
);
const AdOrganizationalUnitCreatePage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitCreatePage").then((module) => ({
    default: module.AdOrganizationalUnitCreatePage,
  })),
);
const AdOrganizationalUnitRenamePage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitRenamePage").then((module) => ({
    default: module.AdOrganizationalUnitRenamePage,
  })),
);
const AdOrganizationalUnitMovePage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitMovePage").then((module) => ({
    default: module.AdOrganizationalUnitMovePage,
  })),
);
const AdDeletedObjectsPage = lazy(() =>
  import("@/features/ad-management/AdDeletedObjectsPage").then((module) => ({
    default: module.AdDeletedObjectsPage,
  })),
);
const AdDeletedObjectDetailPage = lazy(() =>
  import("@/features/ad-management/AdDeletedObjectDetailPage").then((module) => ({
    default: module.AdDeletedObjectDetailPage,
  })),
);
const AdDeletedObjectRestorePage = lazy(() =>
  import("@/features/ad-management/AdDeletedObjectRestorePage").then((module) => ({
    default: module.AdDeletedObjectRestorePage,
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
    path: "/monitoring/module-logs/ad-operation-logs",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdOperationLogs.View}>
          <AppLayout>
            <LazyRoute>
              <AdOperationLogsPage />
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
        <RequireAnyPermission permissions={[PermissionCodes.AdManagement.Settings.View]}>
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
    path: "/ad-management/users",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Users.View}>
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
        <RequirePermission permission={PermissionCodes.AdManagement.Users.Create}>
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
    path: "/ad-management/users/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Users.View}>
          <AppLayout>
            <LazyRoute>
              <AdUserDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/users/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Users.Update}>
          <AppLayout>
            <LazyRoute>
              <AdEditUserPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/users/:id/groups",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Users.Groups.View}>
          <AppLayout>
            <LazyRoute>
              <AdUserGroupsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/users/:id/move-ou",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Users.MoveOu}>
          <AppLayout>
            <LazyRoute>
              <AdMoveUserOuPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/groups",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Groups.View}>
          <AppLayout>
            <LazyRoute>
              <AdGroupsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/groups/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Groups.Create}>
          <AppLayout>
            <LazyRoute>
              <AdGroupCreatePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/groups/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Groups.Update}>
          <AppLayout>
            <LazyRoute>
              <AdEditGroupPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/groups/:id/move-ou",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Groups.MoveOu}>
          <AppLayout>
            <LazyRoute>
              <AdMoveGroupOuPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/groups/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Groups.View}>
          <AppLayout>
            <LazyRoute>
              <AdGroupDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/computers",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Computers.View}>
          <AppLayout>
            <LazyRoute>
              <AdComputersPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/computers/:id/move-ou",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Computers.MoveOu}>
          <AppLayout>
            <LazyRoute>
              <AdMoveComputerOuPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/computers/:id/groups",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Computers.Groups.View}>
          <AppLayout>
            <LazyRoute>
              <AdComputerGroupsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/computers/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.Computers.View}>
          <AppLayout>
            <LazyRoute>
              <AdComputerDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/organizational-units",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.OrganizationalUnits.View}>
          <AppLayout>
            <LazyRoute>
              <AdOrganizationalUnitsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/organizational-units/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.OrganizationalUnits.Create}>
          <AppLayout>
            <LazyRoute>
              <AdOrganizationalUnitCreatePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/organizational-units/:id/rename",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.OrganizationalUnits.Update}>
          <AppLayout>
            <LazyRoute>
              <AdOrganizationalUnitRenamePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/organizational-units/:id/move",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.OrganizationalUnits.Move}>
          <AppLayout>
            <LazyRoute>
              <AdOrganizationalUnitMovePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/organizational-units/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.OrganizationalUnits.View}>
          <AppLayout>
            <LazyRoute>
              <AdOrganizationalUnitDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/deleted-objects",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.DeletedObjects.View}>
          <AppLayout>
            <LazyRoute>
              <AdDeletedObjectsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/deleted-objects/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.DeletedObjects.View}>
          <AppLayout>
            <LazyRoute>
              <AdDeletedObjectDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/ad-management/deleted-objects/:id/restore",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.AdManagement.DeletedObjects.Restore}>
          <AppLayout>
            <LazyRoute>
              <AdDeletedObjectRestorePage />
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
