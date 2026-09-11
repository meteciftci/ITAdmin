import { Navigate, type RouteObject } from "react-router-dom";
import { DnsServersPage } from "@/app/lazy-pages";
import { LazyRoute } from "@/app/route-helpers";
import { AppLayout } from "@/components/layout/AppLayout";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { PermissionCodes } from "@/lib/permission-codes";

export const dnsManagementRoutes: RouteObject[] = [
  { path: "/dns-management", element: <Navigate to="/dns-management/servers" replace /> },
  { path: "/dns-management/servers", element: <RequireAuth><RequirePermission permission={PermissionCodes.DnsManagement.Servers.View}><AppLayout><LazyRoute><DnsServersPage /></LazyRoute></AppLayout></RequirePermission></RequireAuth> },
];
