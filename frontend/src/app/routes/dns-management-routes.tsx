import type { RouteObject } from "react-router-dom";
import { DnsComparisonPage, DnsServersPage, DnsZoneRecordsPage, DnsZonesPage } from "@/app/lazy-pages";
import { LazyRoute } from "@/app/route-helpers";
import { AppLayout } from "@/components/layout/AppLayout";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequireAnyPermission } from "@/features/auth/RequireAnyPermission";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { DnsManagementRedirectPage } from "@/features/dns-management/DnsManagementRedirectPage";
import { PermissionCodes } from "@/lib/permission-codes";

export const dnsManagementRoutes: RouteObject[] = [
  { path: "/dns-management", element: <RequireAuth><DnsManagementRedirectPage /></RequireAuth> },
  { path: "/dns-management/servers", element: <RequireAuth><RequirePermission permission={PermissionCodes.DnsManagement.Servers.View}><AppLayout><LazyRoute><DnsServersPage /></LazyRoute></AppLayout></RequirePermission></RequireAuth> },
  { path: "/dns-management/zones", element: <RequireAuth><RequireAnyPermission permissions={[PermissionCodes.DnsManagement.Zones.View, PermissionCodes.DnsManagement.Records.View]}><AppLayout><LazyRoute><DnsZonesPage /></LazyRoute></AppLayout></RequireAnyPermission></RequireAuth> },
  { path: "/dns-management/zones/:zoneSnapshotId/records", element: <RequireAuth><RequirePermission permission={PermissionCodes.DnsManagement.Records.View}><AppLayout><LazyRoute><DnsZoneRecordsPage /></LazyRoute></AppLayout></RequirePermission></RequireAuth> },
  { path: "/dns-management/comparison", element: <RequireAuth><RequirePermission permission={PermissionCodes.DnsManagement.Compare}><AppLayout><LazyRoute><DnsComparisonPage /></LazyRoute></AppLayout></RequirePermission></RequireAuth> },
];
