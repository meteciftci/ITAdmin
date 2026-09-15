import { Navigate } from "react-router-dom";
import type { RouteObject } from "react-router-dom";
import {
  DnsComparisonPage,
  DnsNetworkConfigurationPage,
  DnsOperationLogsPage,
  DnsPoliciesPage,
  DnsScavengingPage,
  DnsZoneDelegationsPage,
  DnsZoneRecordsPage,
  DnsZonesPage,
  DnsZoneTransfersPage,
  DnssecManagementPage,
} from "@/app/lazy-pages";
import { LazyRoute } from "@/app/route-helpers";
import { AppLayout } from "@/components/layout/AppLayout";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequireAnyPermission } from "@/features/auth/RequireAnyPermission";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { DnsManagementRedirectPage } from "@/features/dns-management/DnsManagementRedirectPage";
import {
  DnsAdvancedManagementPageShell,
  DnsAdvancedManagementRedirectPage,
} from "@/features/dns-management/DnsNavigationTabs";
import { PermissionCodes } from "@/lib/permission-codes";

export const dnsManagementRoutes: RouteObject[] = [
  {
    path: "/dns-management",
    element: (
      <RequireAuth>
        <DnsManagementRedirectPage />
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/servers",
    element: (
      <RequireAuth>
        <Navigate to="/settings/modules/dns-management/servers" replace />
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/server-settings",
    element: (
      <RequireAuth>
        <Navigate
          to="/settings/modules/dns-management/server-settings"
          replace
        />
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/manage",
    element: (
      <RequireAuth>
        <DnsAdvancedManagementRedirectPage />
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/policies",
    element: (
      <RequireAuth>
        <RequirePermission
          permission={PermissionCodes.DnsManagement.ManagePolicies}
        >
          <DnsAdvancedManagementPageShell>
            <DnsPoliciesPage />
          </DnsAdvancedManagementPageShell>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/dnssec",
    element: (
      <RequireAuth>
        <RequirePermission
          permission={PermissionCodes.DnsManagement.ManageDnssec}
        >
          <DnsAdvancedManagementPageShell>
            <DnssecManagementPage />
          </DnsAdvancedManagementPageShell>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/scavenging",
    element: (
      <RequireAuth>
        <RequirePermission
          permission={PermissionCodes.DnsManagement.ManageScavenging}
        >
          <DnsAdvancedManagementPageShell>
            <DnsScavengingPage />
          </DnsAdvancedManagementPageShell>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/network-configuration",
    element: (
      <RequireAuth>
        <RequirePermission
          permission={PermissionCodes.DnsManagement.ManageNetworkConfiguration}
        >
          <DnsAdvancedManagementPageShell>
            <DnsNetworkConfigurationPage />
          </DnsAdvancedManagementPageShell>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/zone-transfers",
    element: (
      <RequireAuth>
        <RequirePermission
          permission={PermissionCodes.DnsManagement.ManageZoneTransfers}
        >
          <DnsAdvancedManagementPageShell>
            <DnsZoneTransfersPage />
          </DnsAdvancedManagementPageShell>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/zone-delegations",
    element: (
      <RequireAuth>
        <RequirePermission
          permission={PermissionCodes.DnsManagement.ManageZoneDelegations}
        >
          <DnsAdvancedManagementPageShell>
            <DnsZoneDelegationsPage />
          </DnsAdvancedManagementPageShell>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/zones",
    element: (
      <RequireAuth>
        <RequireAnyPermission
          permissions={[
            PermissionCodes.DnsManagement.Zones.View,
            PermissionCodes.DnsManagement.Records.View,
          ]}
        >
          <AppLayout>
            <LazyRoute>
              <DnsZonesPage />
            </LazyRoute>
          </AppLayout>
        </RequireAnyPermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/zones/:zoneSnapshotId/records",
    element: (
      <RequireAuth>
        <RequirePermission
          permission={PermissionCodes.DnsManagement.Records.View}
        >
          <AppLayout>
            <LazyRoute>
              <DnsZoneRecordsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/comparison",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.DnsManagement.Compare}>
          <AppLayout>
            <LazyRoute>
              <DnsComparisonPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/monitoring/module-logs/dns-operation-logs",
    element: (
      <RequireAuth>
        <RequirePermission
          permission={PermissionCodes.DnsManagement.ViewOperationLogs}
        >
          <AppLayout>
            <LazyRoute>
              <DnsOperationLogsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/dns-management/operation-logs",
    element: (
      <RequireAuth>
        <Navigate to="/monitoring/module-logs/dns-operation-logs" replace />
      </RequireAuth>
    ),
  },
];
