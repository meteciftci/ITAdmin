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
