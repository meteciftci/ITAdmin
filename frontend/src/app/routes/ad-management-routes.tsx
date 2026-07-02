import { type RouteObject } from "react-router-dom";

import {
  AdComputerDetailPage,
  AdComputerGroupsPage,
  AdComputersPage,
  AdCreateUserPage,
  AdDeletedObjectDetailPage,
  AdDeletedObjectRestorePage,
  AdDeletedObjectsPage,
  AdEditGroupPage,
  AdEditUserPage,
  AdGroupCreatePage,
  AdGroupDetailPage,
  AdGroupsPage,
  AdMoveComputerOuPage,
  AdMoveGroupOuPage,
  AdMoveUserOuPage,
  AdOperationLogsPage,
  AdOrganizationalUnitCreatePage,
  AdOrganizationalUnitDetailPage,
  AdOrganizationalUnitMovePage,
  AdOrganizationalUnitRenamePage,
  AdOrganizationalUnitsPage,
  AdUserDetailPage,
  AdUserGroupsPage,
  AdUsersPage,
} from "@/app/lazy-pages";
import { LazyRoute } from "@/app/route-helpers";
import { AppLayout } from "@/components/layout/AppLayout";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { PermissionCodes } from "@/lib/permission-codes";

export const adManagementRoutes: RouteObject[] = [
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
];
