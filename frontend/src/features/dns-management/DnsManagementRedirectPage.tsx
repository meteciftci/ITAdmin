import { Navigate, useLocation } from "react-router-dom";

import { useAuthStore } from "@/features/auth/auth-store";
import { PermissionCodes } from "@/lib/permission-codes";
import { canAccess } from "@/lib/permissions";
import { getErrorRoutePath } from "@/lib/route-error";

export function DnsManagementRedirectPage() {
  const location = useLocation();
  const user = useAuthStore((state) => state.user);

  if (canAccess(user, PermissionCodes.DnsManagement.Servers.View)) {
    return <Navigate to="/dns-management/servers" replace />;
  }

  if (
    canAccess(user, PermissionCodes.DnsManagement.Zones.View) ||
    canAccess(user, PermissionCodes.DnsManagement.Records.View)
  ) {
    return <Navigate to="/dns-management/zones" replace />;
  }

  if (canAccess(user, PermissionCodes.DnsManagement.Compare)) {
    return <Navigate to="/dns-management/comparison" replace />;
  }

  if (canAccess(user, PermissionCodes.DnsManagement.ManagePolicies)) {
    return <Navigate to="/dns-management/policies" replace />;
  }

  if (canAccess(user, PermissionCodes.DnsManagement.ManageDnssec)) {
    return <Navigate to="/dns-management/dnssec" replace />;
  }

  if (canAccess(user, PermissionCodes.DnsManagement.ManageScavenging)) {
    return <Navigate to="/dns-management/scavenging" replace />;
  }

  if (canAccess(user, PermissionCodes.DnsManagement.ManageNetworkConfiguration)) {
    return <Navigate to="/dns-management/network-configuration" replace />;
  }

  if (canAccess(user, PermissionCodes.DnsManagement.ManageZoneTransfers)) {
    return <Navigate to="/dns-management/zone-transfers" replace />;
  }

  if (canAccess(user, PermissionCodes.DnsManagement.ManageZoneDelegations)) {
    return <Navigate to="/dns-management/zone-delegations" replace />;
  }

  if (
    canAccess(user, PermissionCodes.DnsManagement.ManageServerSettings) ||
    canAccess(user, PermissionCodes.DnsManagement.ClearCache)
  ) {
    return <Navigate to="/dns-management/server-settings" replace />;
  }

  if (canAccess(user, PermissionCodes.DnsManagement.ViewOperationLogs)) {
    return <Navigate to="/dns-management/operation-logs" replace />;
  }

  return (
    <Navigate
      to={getErrorRoutePath("FORBIDDEN")}
      replace
      state={{
        code: "FORBIDDEN",
        kind: "forbidden" as const,
        status: 403,
        titleKey: "errors:api.forbidden.title",
        descriptionKey: "errors:api.forbidden.description",
        fromPath: location.pathname,
        retryPath: location.pathname,
      }}
    />
  );
}
